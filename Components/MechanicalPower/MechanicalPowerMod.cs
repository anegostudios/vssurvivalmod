using ProtoBuf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

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

    public class MechanicalPowerMod : ModSystem
    {
        public MechNetworkRenderer Renderer;
        
        ICoreClientAPI capi;
        ICoreServerAPI sapi;
        IClientNetworkChannel clientNwChannel;
        IServerNetworkChannel serverNwChannel;

        public ICoreAPI Api;

        MechPowerData data = new MechPowerData();

        long nextPropagationId = 1;

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
                api.World.RegisterGameTickListener(OnClientGameTick, 20);
                clientNwChannel =
                    ((ICoreClientAPI)api).Network.RegisterChannel("vsmechnetwork")
                    .RegisterMessageType(typeof(MechNetworkPacket))
                    .RegisterMessageType(typeof(NetworkRemovedPacket))
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
                ;
            }
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

        public long GetNextPropagationId()
        {
            return nextPropagationId++;
        }

        protected void OnClientGameTick(float dt)
        {
            data.tickNumber++;

            foreach (MechanicalNetwork network in data.networksById.Values)
            {
                network.ClientTick(data.tickNumber);
            }
        }

        protected void OnServerGameTick(float dt)
        {
            data.tickNumber++;

            foreach (MechanicalNetwork network in data.networksById.Values)
            {
                if (network.fullyLoaded)
                {
                    network.ServerTick(data.tickNumber);
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

        internal void OnNodeRemoved(IMechanicalPowerNode device)
        {
            if (Api.Side == EnumAppSide.Client) return;

            if (device.Network != null)
            {
                RebuildNetwork(device.Network);
            }
        }

        public void RebuildNetwork(MechanicalNetwork network)
        {
            network.Valid = false;
            DeleteNetwork(network);

            var nnodes = network.nodes.Values.ToArray();

            foreach (var nnode in nnodes)
            {
                nnode.LeaveNetwork();
            }

            foreach (var nnode in nnodes)
            {
                if (nnode.OutFacingForNetworkDiscovery != null)
                {
                    MechanicalNetwork newnetwork = nnode.CreateJoinAndDiscoverNetwork(nnode.OutFacingForNetworkDiscovery);
                    newnetwork.Speed = network.Speed;
                    newnetwork.Angle = network.Angle;
                    newnetwork.TotalAvailableTorque = network.TotalAvailableTorque;
                    newnetwork.NetworkResistance = network.NetworkResistance;
                    newnetwork.broadcastData();
                }
            }
        }

        public void RemoveDeviceForRender(IMechanicalPowerNode device)
        {
            Renderer?.RemoveDevice(device);
        }

        public void AddDeviceForRender(IMechanicalPowerNode device)
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
        private void Event_ChunkDirty(Vec3i chunkCoord, IWorldChunk chunk, EnumChunkDirtyReason reason)
        {
            if (allNetworksFullyLoaded || reason == EnumChunkDirtyReason.MarkedDirty) return;

            allNetworksFullyLoaded = true;
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


        public MechanicalNetwork CreateNetwork(IMechanicalPowerNode powerProducerNode)
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
