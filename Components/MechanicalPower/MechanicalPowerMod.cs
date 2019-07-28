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
    // Concept
    // All directly connected mechanical power blocks that convey or produce torque in the same direction and the same speed are one mechanical network
    // If the direction or speed changes from anywhere along the linked blocks, a "mechanical network bridge" is installed
    // which does the speed/torque translations between both networks

    public class MechanicalPowerMod : ModSystem
    {
        public MechNetworkRenderer Renderer;
        
        ICoreClientAPI capi;
        ICoreServerAPI sapi;
        IClientNetworkChannel clientNwChannel;
        IServerNetworkChannel serverNwChannel;

        Dictionary<long, MechanicalNetwork> networksById = new Dictionary<long, MechanicalNetwork>();
        long nextNetworkId = 1;
        long tickNumber = 0;

        public override bool ShouldLoad(EnumAppSide side)
        {
            return true;
        }

        public override void Start(ICoreAPI api)
        {
            base.Start(api);

            if (api.World is IClientWorldAccessor)
            {
                api.World.RegisterGameTickListener(OnClientGameTick, 20);
                clientNwChannel =
                    ((ICoreClientAPI)api).Network.RegisterChannel("vsmechnetwork")
                    .RegisterMessageType(typeof(MechNetworkPacket))
                    .SetMessageHandler<MechNetworkPacket>(OnPacket)
                ;

            }
            else
            {
                api.World.RegisterGameTickListener(OnServerGameTick, 20);
                serverNwChannel =
                    ((ICoreServerAPI)api).Network.RegisterChannel("vsmechnetwork")
                    .RegisterMessageType(typeof(MechNetworkPacket))
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
        }

        protected void OnClientGameTick(float dt)
        {
            tickNumber++;

            foreach (MechanicalNetwork network in networksById.Values)
            {
                network.ClientTick(tickNumber);
            }
        }

        protected void OnServerGameTick(float dt)
        {
            tickNumber++;

            foreach (MechanicalNetwork network in networksById.Values)
            {
                network.ServerTick(tickNumber);
            }
        }


        protected void OnPacket(MechNetworkPacket networkMessage)
        {
            bool isNew = !networksById.ContainsKey(networkMessage.networkId);

            MechanicalNetwork network = GetOrCreateNetwork(networkMessage.networkId);
            network.UpdateFromPacket(networkMessage, isNew);
        }

        public void broadcastNetwork(MechNetworkPacket packet)
        {
            serverNwChannel.BroadcastPacket(packet);
        }


        private void Event_GameWorldSave()
        {
            sapi.WorldManager.SaveGame.StoreData("mechNetworks", SerializerUtil.Serialize(networksById));
        }

        private void Event_SaveGameLoaded()
        {
            byte[] data = sapi.WorldManager.SaveGame.GetData("mechNetworks");
            if (data != null)
            {
                networksById = SerializerUtil.Deserialize<Dictionary<long, MechanicalNetwork>>(data);
            } else {
                networksById = new Dictionary<long, MechanicalNetwork>();
            }

            foreach (var val in networksById)
            {
                val.Value.mechanicalPowerMod = this;
            }
        }

        private void onLoaded()
        {
            Renderer = new MechNetworkRenderer(capi, this);
        }

        public void RemoveDeviceForRender(IMechanicalPowerNode device)
        {
            Renderer?.RemoveDevice(device);
        }

        public void AddDeviceForRender(IMechanicalPowerNode device)
        {
            Renderer?.AddDevice(device);
        }
        

        public MechanicalNetwork GetOrCreateNetwork(long networkId)
        {
            MechanicalNetwork mw;
            if (!networksById.TryGetValue(networkId, out mw))
            {
                networksById[networkId] = mw = new MechanicalNetwork(this, networkId);
            }

            return mw;
        }

        public MechanicalNetwork CreateNetwork()
        {
            MechanicalNetwork nw = new MechanicalNetwork(this, nextNetworkId);
            networksById[nextNetworkId] = nw;
            nextNetworkId++;

            return nw;
        }




        public void loadNetworks(ITreeAttribute networks)
        {
            networksById.Clear();

            if (networks == null) return;

            foreach (var val in networks)
            {
                ITreeAttribute attr = (ITreeAttribute)val.Value;
                MechanicalNetwork network = new MechanicalNetwork(this, attr.GetInt("networkId"));

                networksById[network.networkId] = network;
                network.ReadFromTreeAttribute(attr);
            }
        }

        public ITreeAttribute saveNetworks()
        {
            ITreeAttribute networks = new TreeAttribute();

            foreach (var var in networksById)
            {
                ITreeAttribute tree = new TreeAttribute();

                var.Value.WriteToTreeAttribute(tree);
                networks[var.Key + ""] = tree;
            }

            return networks;
        }


    }
}
