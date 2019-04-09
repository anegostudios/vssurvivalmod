using System.Collections.Generic;
using System.Linq;
using ProtoBuf;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.Datastructures;

namespace Vintagestory.GameContent.Mechanics
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class MechNetworkPacket {
        public long networkId;
        public float totalAvailableTorque;
        public float totalResistance;
        public float speed;
        public int direction;
        public float angle;

        public int firstNodeX;
        public int firstNodeY;
        public int firstNodeZ;
    }

    public class MechNetworkManagerRegistry
    {
        public static Dictionary<IWorldAccessor, MechNetworkManager> ManagersByWorld;


    }

    /// <summary>
    /// Handles mechanical networks on either side (client or sever)
    /// </summary>
    public class MechNetworkManager
    {
        public ICoreAPI api;
        Dictionary<long, MechanicalNetworkOld> networksById = new Dictionary<long, MechanicalNetworkOld>();
        private int propagationId = 0;
        //private long lastWorldTime = 0;
        long tickNumber = 0;

        IClientNetworkChannel clientNwChannel;
        IServerNetworkChannel serverNwChannel;

        public MechNetworkManager(ICoreAPI api)
        {
            this.api = api;            

            if (api.World is IClientWorldAccessor)
            {
                api.World.RegisterGameTickListener(OnClientGameTick, 20);
                clientNwChannel = 
                    ((ICoreClientAPI)api).Network.RegisterChannel("vsmechnetwork")
                    .RegisterMessageType(typeof(MechNetworkPacket))
                    .SetMessageHandler<MechNetworkPacket>(OnPacket)
                ;
                
            } else 
            {
                api.World.RegisterGameTickListener(OnServerGameTick, 20);
                serverNwChannel = 
                    ((ICoreServerAPI)api).Network.RegisterChannel("vsmechnetwork")
                    .RegisterMessageType(typeof(MechNetworkPacket))
                ;
            }

        }


        private void OnPacket(MechNetworkPacket networkMessage)
        {
            bool isNew = networksById.ContainsKey(networkMessage.networkId);

            MechanicalNetworkOld network = getOrCreateNetwork(networkMessage.networkId);
            network.updateFromPacket(networkMessage, isNew);
        }

        internal void broadcastNetwork(MechNetworkPacket packet)
        {
            serverNwChannel.BroadcastPacket(packet);
        }





        private void OnClientGameTick(float dt)
        {
            tickNumber++;

            foreach (MechanicalNetworkOld network in networksById.Values)
            {
                network.ClientTick(tickNumber);
            }
        }

        private void OnServerGameTick(float dt)
        {
            tickNumber++;

            foreach (MechanicalNetworkOld network in networksById.Values)
            {
                network.ServerTick(tickNumber);
            }
        }


        

        public void loadNetworks(ITreeAttribute networks)
        {
            //System.out.println("load networks for dimension " + world.provider.getDimensionId() + ", list: " + networks);
            networksById.Clear();

            if (networks == null) return;

            foreach (var val in networks)
            {
                ITreeAttribute attr = (ITreeAttribute)val.Value;
                MechanicalNetworkOld network = new MechanicalNetworkOld(this, attr.GetInt("networkId"));

                networksById[network.networkId] = network;
                network.readFromTreeAttribute(attr);
                network.rediscoverNetwork(api.World);
            }
        }

        public ITreeAttribute saveNetworks()
        {
            ITreeAttribute networks = new TreeAttribute();

            foreach (var var in networksById)
            {
                ITreeAttribute tree = new TreeAttribute();

                var.Value.writeToTreeAttribute(tree);
                networks[var.Key + ""] = tree;
            }

            return networks;
        }



        private long findUniqueId()
        {
            return networksById.Keys.Aggregate((a, b) => a > b ? a : b) + 1;
        }

        public int getUniquePropagationId()
        {
            return ++propagationId;
        }


        public MechanicalNetworkOld getNetworkById(long id)
        {
            MechanicalNetworkOld network = null;
            networksById.TryGetValue(id, out network);
            return network;
        }

        public MechanicalNetworkOld createAndRegisterNetwork(IMechanicalPowerNetworkNode originatorNode)
        {
            MechanicalNetworkOld network = createAndRegisterNetwork();
            network.register(originatorNode);
            network.firstPowerNode = originatorNode.getPosition();

            return network;
        }


        public MechanicalNetworkOld getOrCreateNetwork(long networkid)
        {
            MechanicalNetworkOld network = getNetworkById(networkid);
            if (network == null)
            {
                //System.out.println("from get instantiated new network with id " + networkid);
                network = new MechanicalNetworkOld(this, networkid);
                networksById[networkid] = network;
            }
            return network;
        }

        public MechanicalNetworkOld createAndRegisterNetwork()
        {
            long networkId = findUniqueId();
            //System.out.println(world.isRemote + " from create instantiated new network with id " + networkId);

            MechanicalNetworkOld network = new MechanicalNetworkOld(this, networkId);

            networksById[networkId] = network;

            return network;
        }


        public void unloadNetworks()
        {
            networksById.Clear();
        }

        public void discardNetwork(MechanicalNetworkOld network)
        {
            networksById.Remove(network.networkId);
        }

    }
}
