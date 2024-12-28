using ProtoBuf;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Vintagestory.GameContent.Mechanics
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class MechPowerData
    {
        public Dictionary<long, MechanicalNetwork> networksById = new Dictionary<long, MechanicalNetwork>();
        public long nextNetworkId = 1;
        public long tickNumber = 0;
    }


    // Concept
    // All directly connected mechanical power blocks that convey or produce torque in the same direction and the same speed are one mechanical network
    // If the direction or speed changes from anywhere along the linked blocks, a "mechanical network bridge" is installed
    // which does the speed/torque translations between both networks

    // Concept 2
    // - ✔ Only power producers trigger the creation of a mechanical network. axles, gears and querns are "inert" when it comes to acquiring a mech network
    // - ✔ Block entities forget their associated networkid upon unloading
    // - ✔ Block entities in a network that got unloaded announce it to their mech network. Mechnetworks fullyLoaded flag turns to false! 
    // - ✔ Whenever a power producer is placed or loaded that is not adjacent to an existing mech network we create a new mechanical network 
    // - ✔ Mech network creation: Trigger a network discovery process to all directly connected mech power blocks

    // - Problem: Mech networks will "restart" upon savegame reload.
    // - Fix: Remember the speed of power producers and have it assigned to the mech network?

    // - ✔ Problem: During load of a power producer a discovery process is triggered before other block entities have been initalized
    // - ✔ Fix: Pass on the api during the discovery process, as all blockentities of the same chunk do have CreateBehaviors and FromTreeAttribtues already done and there's nothing critical done in Initialize()

    // - ✔ During creation of a mech network, the network remembers a list of all chunks that contains components
    // - ✔ When a discovery process has been initiated and a stored mech network exists, test if all required chunks are loaded. If not, add a load event listener to the first not loaded chunk, do the test again, rinse repeat

    // Concept 3
    // - Breaking of a mechnetwork block that had >=2 connections: 
    //   1. Get the power producers from the original network
    //   2. Delete the network
    //   3. Get/Create a new network for every producer, using the network discovery process

    public class MechanicalPowerMod : ModSystem, IRenderer
    {
        public MechNetworkRenderer Renderer;
        
        ICoreClientAPI capi;
        ICoreServerAPI sapi;
        IClientNetworkChannel clientNwChannel;
        IServerNetworkChannel serverNwChannel;

        public ICoreAPI Api;

        MechPowerData data = new MechPowerData();

        public override bool ShouldLoad(EnumAppSide side)
        {
            return true;
        }

        public override void Start(ICoreAPI api)
        {
            base.Start(api);

            this.Api = api;

            if (api.World is IClientWorldAccessor)
            {
                (api as ICoreClientAPI).Event.RegisterRenderer(this, EnumRenderStage.Before, "mechanicalpowertick");

                clientNwChannel =
                    ((ICoreClientAPI)api).Network.RegisterChannel("vsmechnetwork")
                    .RegisterMessageType(typeof(MechNetworkPacket))
                    .RegisterMessageType(typeof(NetworkRemovedPacket))
                    .RegisterMessageType(typeof(MechClientRequestPacket))
                    .SetMessageHandler<MechNetworkPacket>(OnPacket)
                    .SetMessageHandler<NetworkRemovedPacket>(OnNetworkRemovePacket)
                ;

            }
            else
            {
                api.World.RegisterGameTickListener(OnServerGameTick, 20);
                serverNwChannel =
                    ((ICoreServerAPI)api).Network.RegisterChannel("vsmechnetwork")
                    .RegisterMessageType(typeof(MechNetworkPacket))
                    .RegisterMessageType(typeof(NetworkRemovedPacket))
                    .RegisterMessageType(typeof(MechClientRequestPacket))
                    .SetMessageHandler<MechClientRequestPacket>(OnClientRequestPacket)
                ;
            }
        }

        public long getTickNumber()
        {
            return data.tickNumber;
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            base.StartClientSide(api);
            this.capi = api;

            api.Event.BlockTexturesLoaded += onLoaded;
            api.Event.LeaveWorld += () =>
            {
                Renderer?.Dispose();
            };
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            this.sapi = api;
            base.StartServerSide(api);

            api.Event.SaveGameLoaded += Event_SaveGameLoaded;
            api.Event.GameWorldSave += Event_GameWorldSave;
            api.Event.ChunkDirty += Event_ChunkDirty;
        }

        protected void OnServerGameTick(float dt)
        {
            data.tickNumber++;

            List<MechanicalNetwork> clone = data.networksById.Values.ToList();
            foreach (MechanicalNetwork network in clone)
            {
                if (network.fullyLoaded && network.nodes.Count > 0)
                {
                    network.ServerTick(dt, data.tickNumber);
                }
            }   
        }


        protected void OnPacket(MechNetworkPacket networkMessage)
        {
            bool isNew = !data.networksById.ContainsKey(networkMessage.networkId);

            MechanicalNetwork network = GetOrCreateNetwork(networkMessage.networkId);
            network.UpdateFromPacket(networkMessage, isNew);
        }

        protected void OnNetworkRemovePacket(NetworkRemovedPacket networkMessage)
        {
            data.networksById.Remove(networkMessage.networkId);
        }

        protected void OnClientRequestPacket(IServerPlayer player, MechClientRequestPacket networkMessage)
        {
            if (data.networksById.TryGetValue(networkMessage.networkId, out MechanicalNetwork nw))
            {
                nw.SendBlocksUpdateToClient(player);
            }
        }

        public void broadcastNetwork(MechNetworkPacket packet)
        {
            serverNwChannel.BroadcastPacket(packet);
        }

        private void Event_GameWorldSave()
        {
            //sapi.WorldManager.SaveGame.StoreData("mechPowerData", SerializerUtil.Serialize(data));
        }

        private void Event_SaveGameLoaded()
        {
            this.data = new MechPowerData();

            /*byte[] data = sapi.WorldManager.SaveGame.GetData("mechPowerData");
            if (data != null)
            {
                this.data = SerializerUtil.Deserialize<MechPowerData>(data);
            } else {
                this.data = new MechPowerData();
            }

            foreach (var val in this.data.networksById)
            {
                val.Value.Init(this);
            }*/
        }

        private void onLoaded()
        {
            Renderer = new MechNetworkRenderer(capi, this);
        }

        internal void OnNodeRemoved(IMechanicalPowerDevice device)
        {
            if (device.Network != null)
            {
                RebuildNetwork(device.Network, device);
            }
        }

        public void RebuildNetwork(MechanicalNetwork network, IMechanicalPowerDevice nowRemovedNode = null)
        {
            network.Valid = false;

            if (Api.Side == EnumAppSide.Server) DeleteNetwork(network);

            if (network.nodes.Values.Count == 0)
            {
                // This case shouldn't happen, but it does occasionally, should get debugged eventually, until then, it makes no sense to spam the log files with this
                //if (Api.Side == EnumAppSide.Server) Api.Logger.Notification("Mech. Network with id " + network.networkId + " had zero nodes?");
                return;
            }

            var nnodes = network.nodes.Values.ToArray();

            foreach (var nnode in nnodes)
            {
                nnode.LeaveNetwork();
            }

            foreach (var nnode in nnodes)
            {
                if (!(nnode is IMechanicalPowerDevice)) continue;
                IMechanicalPowerDevice newnode = Api.World.BlockAccessor.GetBlock((nnode as IMechanicalPowerDevice).Position).GetInterface<IMechanicalPowerDevice>(Api.World, (nnode as IMechanicalPowerDevice).Position);
                if (newnode == null) continue;
                BlockFacing oldTurnDir = newnode.GetPropagationDirection();

                if (newnode.OutFacingForNetworkDiscovery != null && (nowRemovedNode == null || newnode.Position != nowRemovedNode.Position))
                {
                    MechanicalNetwork newnetwork = newnode.CreateJoinAndDiscoverNetwork(newnode.OutFacingForNetworkDiscovery);
                    bool reversed = newnode.GetPropagationDirection() == oldTurnDir.Opposite;
                    newnetwork.Speed = reversed ? -network.Speed : network.Speed;
                    newnetwork.AngleRad = network.AngleRad;
                    newnetwork.TotalAvailableTorque = reversed ? -network.TotalAvailableTorque : network.TotalAvailableTorque;
                    newnetwork.NetworkResistance = network.NetworkResistance;
                    if (Api.Side == EnumAppSide.Server) newnetwork.broadcastData();
                }
            }
        }

        public void RemoveDeviceForRender(IMechanicalPowerRenderable device)
        {
            Renderer?.RemoveDevice(device);
        }

        public void AddDeviceForRender(IMechanicalPowerRenderable device)
        {
            Renderer?.AddDevice(device);
        }

        public override void Dispose()
        {
            base.Dispose();
            Renderer?.Dispose();
        }

        public MechanicalNetwork GetOrCreateNetwork(long networkId)
        {
            MechanicalNetwork mw;
            
            if (!data.networksById.TryGetValue(networkId, out mw))
            {
                data.networksById[networkId] = mw = new MechanicalNetwork(this, networkId);
            }

            testFullyLoaded(mw);

            return mw;
        }

        public void testFullyLoaded(MechanicalNetwork mw)
        {
            if (Api.Side != EnumAppSide.Server || mw.fullyLoaded) return;

            mw.fullyLoaded = mw.testFullyLoaded(Api);
            allNetworksFullyLoaded &= mw.fullyLoaded;
        }


        bool allNetworksFullyLoaded = true;
        List<MechanicalNetwork> nowFullyLoaded = new List<MechanicalNetwork>();

        public double RenderOrder => 0;
        public int RenderRange => 9999;

        private void Event_ChunkDirty(Vec3i chunkCoord, IWorldChunk chunk, EnumChunkDirtyReason reason)
        {
            if (allNetworksFullyLoaded || reason == EnumChunkDirtyReason.MarkedDirty) return;

            allNetworksFullyLoaded = true;
            nowFullyLoaded.Clear();

            foreach (var network in data.networksById.Values)
            {
                if (network.fullyLoaded) continue;
                allNetworksFullyLoaded = false;

                if (network.inChunks.ContainsKey(chunkCoord)) {
                    testFullyLoaded(network);
                    if (network.fullyLoaded)
                    {
                        nowFullyLoaded.Add(network);
                    }
                }
            }

            for (int i = 0; i < nowFullyLoaded.Count; i++)
            {
                RebuildNetwork(nowFullyLoaded[i]);
            }
        }


        public MechanicalNetwork CreateNetwork(IMechanicalPowerDevice powerProducerNode)
        {
            MechanicalNetwork nw = new MechanicalNetwork(this, data.nextNetworkId);
            nw.fullyLoaded = true;

            data.networksById[data.nextNetworkId] = nw;
            data.nextNetworkId++;

            return nw;
        }

        public void DeleteNetwork(MechanicalNetwork network)
        {
            data.networksById.Remove(network.networkId);
            serverNwChannel.BroadcastPacket<NetworkRemovedPacket>(new NetworkRemovedPacket() { networkId = network.networkId });
        }

        public void SendNetworkBlocksUpdateRequestToServer(long networkId)
        {
            clientNwChannel.SendPacket<MechClientRequestPacket>(new MechClientRequestPacket() { networkId = networkId });
        }

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            if (capi.IsGamePaused) return;

            foreach (MechanicalNetwork network in data.networksById.Values)
            {
                network.ClientTick(deltaTime);
            }
        }




        /* public void loadNetworks(ITreeAttribute networks)
         {
             data.networksById.Clear();

             if (networks == null) return;

             foreach (var val in networks)
             {
                 ITreeAttribute attr = (ITreeAttribute)val.Value;
                 MechanicalNetwork network = new MechanicalNetwork(this, attr.GetInt("networkId"));

                 data.networksById[network.networkId] = network;
                 network.ReadFromTreeAttribute(attr);
             }
         }

         public ITreeAttribute saveNetworks()
         {
             ITreeAttribute networks = new TreeAttribute();

             foreach (var var in data.networksById)
             {
                 ITreeAttribute tree = new TreeAttribute();

                 var.Value.WriteToTreeAttribute(tree);
                 networks[var.Key + ""] = tree;
             }

             return networks;
         }*/


    }
}
