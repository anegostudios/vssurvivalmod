using ProtoBuf;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

#nullable disable

namespace Vintagestory.GameContent.Mechanics
{
    [ProtoContract]
    public class MechPowerData
    {
        [ProtoMember(1)]
        public Dictionary<long, MechanicalNetwork> networksById = new Dictionary<long, MechanicalNetwork>();
        [ProtoMember(2), DefaultValue(1)]
        public long nextNetworkId = 1;
        [ProtoMember(3)]
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
        readonly Dictionary<BlockPos, BEBehaviorMPBase> devicesByPosition = [];
        readonly Dictionary<string, MechanicalMaterialPalette> materialPalettes = [];
        SimpleParticleProperties smokeParticles;

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
            api.Event.ReloadTextures += () =>
            {
                foreach (MechanicalMaterialPalette palette in materialPalettes.Values) palette.Redraw(capi);
            };
            api.Event.LeaveWorld += () =>
            {
                Renderer?.Dispose();
            };
        }

        // Blocks whose material textures resolve to the same images, such as each orientation of one part, share a palette.
        public MechanicalMaterialPalette GetOrCreateMaterialPalette(string signature, CompositeTexture defaultTexture, IReadOnlyList<KeyValuePair<string, CompositeTexture>> materialTextures)
        {
            if (!materialPalettes.TryGetValue(signature, out MechanicalMaterialPalette palette))
            {
                materialPalettes[signature] = palette = new(capi, defaultTexture, materialTextures);
            }
            return palette;
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            this.sapi = api;
            base.StartServerSide(api);

            api.Event.SaveGameLoaded += Event_SaveGameLoaded;
            api.Event.ChunkDirty += Event_ChunkDirty;

            smokeParticles = new SimpleParticleProperties(
                2, 2,
                ColorUtil.ToRgba(150, 40, 40, 40),
                new Vec3d(),
                new Vec3d(1, 0, 1),
                new Vec3f(-1 / 32f, 0.1f, -1 / 32f),
                new Vec3f(1 / 32f, 0.1f, 1 / 32f),
                2f,
                -0.025f / 4,
                0.2f,
                1f,
                EnumParticleModel.Quad
            );

            smokeParticles.SizeEvolve = new EvolvingNatFloat(EnumTransformFunction.LINEAR, -0.25f);
            smokeParticles.SelfPropelled = true;
            smokeParticles.AddPos.Set(1, 0, 1);

            smokeParticles.MinQuantity = 0.25f;
            smokeParticles.OpacityEvolve = EvolvingNatFloat.create(EnumTransformFunction.QUADRATIC, -15);
            smokeParticles.AddQuantity = 0;
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

                    if ((data.tickNumber % 10) == 0)
                    {
                        var speed = network.Speed;
                        foreach (IMechanicalPowerNode powerNode in network.nodes.Values)
                        {
                            var nodespeed = Math.Abs(powerNode is ILinkageProducer producer
                                ? producer.LinkageSpeed
                                : speed * (powerNode is BEBehaviorMPBase behavior ? behavior.EffectiveGearedRatio : powerNode.GearedRatio));
                            if (nodespeed > 4.5)
                            {
                                var pos = powerNode.GetPosition();
                                smokeParticles.MinPos.Set(pos.X, pos.Y, pos.Z);
                                smokeParticles.AddPos.Set(1, 1, 1);
                                sapi.World.SpawnParticles(smokeParticles);
                                if (nodespeed > 5.5)
                                {
                                    powerNode.OverheatValue = Math.Min(1.1f, powerNode.OverheatValue + (nodespeed - 2.5f) / 10f);
                                }
                            } else
                            {
                                powerNode.OverheatValue = Math.Max(0, powerNode.OverheatValue + (nodespeed - 2.5f) / 10f);
                            }

                            if (powerNode.OverheatValue > 1 && sapi.World.Rand.NextDouble() < 0.03)
                            {
                                var pos = powerNode.GetPosition();
                                // Disabled until stable
                                // sapi.World.BlockAccessor.GetBlock(pos).GetBEBehavior<BEBehaviorBurning>(pos)?.OnFirePlaced(pos, pos, null, false);
                            }
                        }
                    }

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

        private void Event_SaveGameLoaded()
        {
            this.data = new MechPowerData();
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
            RebuildNetworks([network], nowRemovedNode);
        }

        public void RebuildNetworks(List<MechanicalNetwork> networks, IMechanicalPowerDevice nowRemovedNode = null)
        {
            var oldNetworks = networks.Where(network => network.Valid).Distinct().ToArray();
            var nodes = oldNetworks.SelectMany(network => network.nodes.Values).Distinct().ToArray();
            List<(IMechanicalPowerDevice Node, float Speed, bool Powered, int Depth, MechPowerPath Path, double Angle)> starts = [];
            HashSet<BlockPos> startPositions = [];
            var linkageSystem = Api.ModLoader.GetModSystem<ModSystemLinkage>();

            if (Api.Side == EnumAppSide.Server)
            {
                foreach (var node in nodes)
                {
                    IMechanicalPowerDevice seed;
                    if (node is ILinkageProducer producer)
                    {
                        if (linkageSystem.GetEndpoint(producer.LinkagePosition) is not ILinkageProducer loadedProducer) continue;
                        seed = linkageSystem.GetNetworkSeed(loadedProducer);
                    }
                    else
                    {
                        if (node is not IMechanicalPowerDevice oldDevice) continue;
                        seed = Api.World.BlockAccessor.GetBlock(oldDevice.Position).GetInterface<IMechanicalPowerDevice>(Api.World, oldDevice.Position);
                        if (seed == null) continue;
                    }
                    if (seed == null || seed is BEBehaviorMPBase device && device.disconnected
                        || seed.Position.Equals(nowRemovedNode?.Position) || !startPositions.Add(seed.Position))
                    {
                        continue;
                    }

                    LinkageAngleTransform transform = seed is ILinkageProducer crank ? crank.LinkageAngleTransform : (seed as BEBehaviorMPBase)?.AngleTransform;
                    if (seed.OutFacingForNetworkDiscovery == null && seed is not ILinkageProducer)
                    {
                        if (seed is BEBehaviorMPBase passive && seed.Network != null)
                        {
                            MechPowerPath path = passive.GetNetworkDiscoveryPath();
                            float passiveSpeed = seed.Network.Speed;
                            double passiveAngle = seed.Network.TotalAngleRad;
                            if (transform != null)
                            {
                                passiveSpeed = passive.CurrentSpeed;
                                passiveAngle = passive.UnreversedAngleRad;
                                path.gearingRatio = 1;
                                path.AngleTransform = null;
                            }
                            starts.Add((seed, passiveSpeed, false, transform?.Depth ?? 0, path, passiveAngle));
                        }
                        continue;
                    }
                    float speed = seed is ILinkageProducer source ? source.LinkageSpeed
                        : (seed.Network?.Speed ?? 0) * seed.GearedRatio * (transform?.GetSpeedFactor(seed.Network?.TotalAngleRad ?? 0) ?? 1);
                    starts.Add((seed, speed * (seed.IsRotationReversed() ? -1 : 1), seed.Network?.HasPowerProducer == true, transform?.Depth ?? 0, null, 0));
                }
            }

            foreach (var network in oldNetworks)
            {
                network.Valid = false;
                if (Api.Side == EnumAppSide.Server) DeleteNetwork(network);
            }
            foreach (var node in nodes) node.LeaveNetwork();

            List<MechanicalNetwork> rebuiltNetworks = [];
            // This will start upstream of rocking shafts so discovery never tries to turn a rocker through a full circle.
            foreach (var start in starts.OrderByDescending(start => start.Powered).ThenBy(start => start.Path != null).ThenBy(start => start.Depth))
            {
                IMechanicalPowerDevice seed = start.Node;
                if (seed.Network != null) continue;

                long firstNewNetworkId = data.nextNetworkId;
                MechanicalNetwork newNetwork;
                if (start.Path != null)
                {
                    newNetwork = ((BEBehaviorMPBase)seed).CreateJoinAndDiscoverNetwork(start.Path, start.Angle);
                }
                else
                {
                    BlockFacing facing = seed is ILinkageProducer producer ? producer.GetLinkageFacing(linkageSystem.GetLinkagePoint(producer)) : seed.OutFacingForNetworkDiscovery;
                    newNetwork = seed.CreateJoinAndDiscoverNetwork(facing);
                }
                if (newNetwork == null || newNetwork.networkId < firstNewNetworkId) continue;

                LinkageAngleTransform transform = seed is ILinkageProducer crank ? crank.LinkageAngleTransform : (seed as BEBehaviorMPBase)?.AngleTransform;
                float signedRatio = seed.GearedRatio * (transform?.GetSpeedFactor(newNetwork.TotalAngleRad) ?? 1) * (seed.IsRotationReversed() ? -1 : 1);
                newNetwork.Speed = start.Path != null ? start.Speed : start.Speed / signedRatio;
                rebuiltNetworks.Add(newNetwork);
            }
            foreach (var network in rebuiltNetworks)
            {
                if (network.Valid) network.broadcastData();
            }
        }

        public void RemoveDeviceForRender(IMechanicalPowerRenderable device)
        {
            if (Api.Side == EnumAppSide.Client
                && device is BEBehaviorMPBase behavior
                && devicesByPosition.TryGetValue(behavior.Position, out BEBehaviorMPBase current)
                && current == behavior)
            {
                devicesByPosition.Remove(behavior.Position);
            }
            Renderer?.RemoveDevice(device);
        }

        public void AddDeviceForRender(IMechanicalPowerRenderable device)
        {
            if (Api.Side == EnumAppSide.Client && device is BEBehaviorMPBase behavior) devicesByPosition[behavior.Position] = behavior;
            Renderer?.AddDevice(device);
        }

        public BEBehaviorMPBase GetDevice(BlockPos pos)
        {
            devicesByPosition.TryGetValue(pos, out BEBehaviorMPBase behavior);
            return behavior;
        }

        public override void Dispose()
        {
            base.Dispose();
            devicesByPosition.Clear();
            Renderer?.Dispose();
        }

        public MechanicalNetwork GetOrCreateNetwork(long networkId)
        {

            if (!data.networksById.TryGetValue(networkId, out MechanicalNetwork mw))
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
            network.ClearLoadTopology();
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


    }
}
