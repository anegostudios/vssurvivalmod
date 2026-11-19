using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent.Mechanics
{
    public class MechPowerPath {
        public BlockFacing OutFacing;
        public bool invert;
        public float gearingRatio;
        private readonly BlockPos fromPos;
        public BlockFacing turnDir;
        public BlockFacing spurGearFace;   // Disc face when several spur meshes share OutFacing.
        public LinkageAngleTransform AngleTransform;
        public BEBehaviorMPBase LoadFrom;   // Who spread this path, so the receiver can connect to it for load

        public BlockPos FromPos => fromPos;

        public MechPowerPath()
        {
        }

        /// <summary>
        /// Specify fromPos where it matters (e.g. when SetPropagationDir using a direction taken from a neighbour
        /// </summary>
        public MechPowerPath(BlockFacing facing, float gearingRatioHere, BlockPos fromPos = null, bool inverted = false)
        {
            this.OutFacing = facing;
            this.invert = inverted;
            this.gearingRatio = gearingRatioHere;
            this.fromPos = fromPos;
        }

        public MechPowerPath(BlockFacing facing, float gearingRatioHere, BlockPos fromPos, bool inverted, LinkageAngleTransform angleTransform, BlockFacing turnDir = null, BlockFacing spurGearFace = null) : this(facing, gearingRatioHere, fromPos, inverted)
        {
            AngleTransform = angleTransform;
            this.turnDir = turnDir;
            this.spurGearFace = spurGearFace;
        }

        public BlockFacing NetworkDir()
        {
            return turnDir ?? (this.invert ? OutFacing.Opposite : OutFacing);
        }

        public bool IsInvertedTowards(BlockPos testPos)
        {
            return fromPos == null ? invert : fromPos.AddCopy(this.NetworkDir()) != testPos;
        }

        public MechPowerPath PropagatedClone(BlockFacing outfacing, bool inverted = false, BlockFacing turnDir = null, BlockPos fromPos = null)
        {
            return new MechPowerPath(outfacing, gearingRatio, fromPos, inverted, AngleTransform, turnDir);
        }
    }

    public abstract class BEBehaviorMPBase : BlockEntityBehavior, IMechanicalPowerDevice
    {
        // One network speed unit is five radians per second.
        protected const double rpmPerSpeedUnit = 5 * 60 / Math.Tau;

#if DEBUG
        /// <summary>
        /// Set to true to debug Mechanical Power network discovery etc..  The #if DEBUG is just in case: ensures this debugging log spam can never make it into a release build by accident.
        /// </summary>
        protected static readonly bool DEBUG = false;
#else
        protected static readonly bool DEBUG = false;
#endif

        protected MechanicalPowerMod manager;
        protected MechanicalNetwork network;

        // A quantity-one copy of the stack this part was placed from.
        ItemStack placedStack;
        Dictionary<string, Vec2f> materialUvOffsets;

        SoundAttributes? turnSound;
        float turnSoundChanceMultiplier = 1 / 3f;

        public Vec4f lightRbs = new Vec4f();

        public virtual BlockPos Position { get { return Blockentity.Pos; } }
        public BlockPos GetPosition() { return Position; }
        public virtual Vec4f LightRgba { get { return lightRbs; } }

        /// <summary>
        /// Return null to not use a mechanical power instanced renderer for this block. When you change the shape, the renderer is also updated to reflect that change
        /// </summary>
        private CompositeShape shape = null;
        public virtual CompositeShape Shape
        {
            get { return shape; }
            set
            {
                CompositeShape prev = Shape;
                if (prev != null && manager != null && prev != value)
                {
                    manager.RemoveDeviceForRender(this);
                    this.shape = value;
                    manager.AddDeviceForRender(this);
                }
                else
                {
                    this.shape = value;
                }
            }
        }

        public virtual int[] AxisSign { get; protected set; }

        public long NetworkId { get; set; }

        public MechanicalNetwork Network => network;
        public virtual BlockFacing OutFacingForNetworkDiscovery { get; protected set; } = null;

        protected BlockFacing propagationDir = BlockFacing.NORTH;
        private float gearedRatio = 1.0f;
        LinkageAngleTransform angleTransform;
        public float GearedRatio { get { return gearedRatio; } set { gearedRatio = value;} }
        public LinkageAngleTransform AngleTransform => angleTransform;
        public float OverheatValue { get; set; }
        public virtual float GetGearedRatio(BlockFacing face) { return gearedRatio; }

        protected double lastKnownAngleRad = 0;
        public bool disconnected = false;

        public virtual float AngleRad
        {
            get
            {
                lastKnownAngleRad = UnwrappedAngleRad;
                return (float)GameMath.Mod(lastKnownAngleRad, Math.Tau);
            }
        }

        double UnwrappedAngleRad => IsRotationReversed() ? -UnreversedAngleRad : UnreversedAngleRad;

        public float EffectiveGearedRatio => gearedRatio * (angleTransform?.GetSpeedFactor(network?.TotalAngleRad ?? 0) ?? 1);
        public float CurrentSpeed => network?.Speed * EffectiveGearedRatio ?? 0;

        /// <summary>
        /// Shaft angle in radians before the facing's direction reversal, including complete turns.
        /// </summary>
        public double UnreversedAngleRad
        {
            get
            {
                if (network == null) return IsRotationReversed() ? -lastKnownAngleRad : lastKnownAngleRad;
                if (angleTransform == null) return network.TotalAngleRad * gearedRatio;

                double solvedAngle = angleTransform.GetAngle(network.TotalAngleRad, gearedRatio, IsRotationReversed());
                return IsRotationReversed() ? -solvedAngle : solvedAngle;
            }
        }

        public virtual bool IsRotationReversed()
        {
            if (propagationDir == null) return false;

            return propagationDir == BlockFacing.DOWN || propagationDir == BlockFacing.EAST || propagationDir == BlockFacing.SOUTH;
        }

        public virtual bool IsInvertedNetworkFor(BlockPos pos)
        {
            if (propagationDir == null || pos == null) return false;
            BlockPos testPos = this.Position.AddCopy(propagationDir);
            return !testPos.Equals(pos);
        }

        public BEBehaviorMPBase(BlockEntity blockentity) : base(blockentity)
        {
        }


        public override void Initialize(ICoreAPI api, JsonObject properties)
        {
            base.Initialize(api, properties);

            Shape = GetShape();

            manager = Api.ModLoader.GetModSystem<MechanicalPowerMod>();

            if (Api.World.Side == EnumAppSide.Client)
            {
                lightRbs = Api.World.BlockAccessor.GetLightRGBs(Blockentity.Pos);
                if (NetworkId > 0)
                {
                    JoinNetwork(manager.GetOrCreateNetwork(NetworkId));
                }

                updateMaterialUvOffsets();

                JsonObject mechanicalPower = Block.Attributes?["mechanicalPower"];
                turnSound = mechanicalPower?["turnSound"].AsObject<SoundAttributes?>(null, Block.Code.Domain, false);
                if (turnSound != null)
                {
                    turnSoundChanceMultiplier = mechanicalPower!["turnSoundChanceMultiplier"].AsFloat(turnSoundChanceMultiplier);
                    Blockentity.RegisterGameTickListener(onTurnSoundTick, 1000);
                }
            }

            manager.AddDeviceForRender(this);

            AxisSign = new int[3] { 0, 0, 1 };
            SetOrientations();

            if (api.Side == EnumAppSide.Server && OutFacingForNetworkDiscovery != null)
            {
                CreateJoinAndDiscoverNetwork(OutFacingForNetworkDiscovery);
            }
        }

        void onTurnSoundTick(float dt)
        {
            // Network speed rather than CurrentSpeed for gearing.
            float speed = network?.Speed ?? 0;
            if (Api.World.Rand.NextDouble() >= speed * turnSoundChanceMultiplier) return;

            SoundAttributes sound = turnSound.Value;
            var spitch = sound.Pitch.ToNatFloat();
            spitch.avg += speed;
            sound.Pitch = spitch;
            Api.World.PlaySoundAt(sound, Position, 0);
        }

        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            base.OnBlockPlaced(byItemStack);
            if (byItemStack == null || (Block as BlockMPBase)?.UsesMaterialAttributes != true) return;

            placedStack = byItemStack.Clone();
            placedStack.StackSize = 1;
            updateMaterialUvOffsets();
            Blockentity.MarkDirty(true);
        }

        public ItemStack GetPlacedStack() => placedStack?.Collectible == null ? null : placedStack.Clone();

        protected ItemStack PlacedStack => placedStack;

        public virtual bool TryGetMaterialUvOffset(string textureCode, Vec2f offset)
        {
            if (materialUvOffsets?.TryGetValue(textureCode, out Vec2f materialOffset) == true)
            {
                offset.X = materialOffset.X;
                offset.Y = materialOffset.Y;
                return true;
            }

            offset.X = 0;
            offset.Y = 0;
            return false;
        }

        void updateMaterialUvOffsets()
        {
            if (Api is not ICoreClientAPI capi || Block is not BlockMPBase materialBlock || !materialBlock.UsesMaterialAttributes) return;

            materialUvOffsets ??= [];
            foreach (string textureCode in materialBlock.MaterialTextureCodes)
            {
                if (!materialUvOffsets.TryGetValue(textureCode, out Vec2f offset)) materialUvOffsets[textureCode] = offset = new Vec2f();
                materialBlock.GetMaterialUvOffset(capi, placedStack, textureCode, offset);
            }
        }

        protected virtual CompositeShape GetShape()
        {
            return Block.Shape;
        }

        public virtual void SetOrientations()
        {

        }

        public virtual void WasPlaced(BlockFacing connectedOnFacing)
        {
            // Skip this if already called CreateJoinAndDiscoverNetwork in Initialize()
            if ((Api.Side == EnumAppSide.Client || OutFacingForNetworkDiscovery == null) && connectedOnFacing != null)
            {
                if (!tryConnect(connectedOnFacing))
                {
                    if (DEBUG) Api.Logger.Notification("Was placed fail connect 2nd: " + connectedOnFacing + " at " + Position);
                }
                else if (DEBUG) Api.Logger.Notification("Was placed connected 1st: " + connectedOnFacing + " at " + Position);
            }
        }

        public bool tryConnect(BlockFacing toFacing)
        {
            return tryConnect(toFacing, out _);
        }

        public bool tryConnect(BlockFacing toFacing, out Vec3i unloadedChunkPos)
        {
            return tryConnectToNetwork(toFacing, out unloadedChunkPos);
        }

        protected virtual bool tryConnectToNetwork(BlockFacing toFacing, out Vec3i unloadedChunkPos)
        {
            MechanicalNetwork newNetwork;
            unloadedChunkPos = null;

            if (Api == null) return false;

            BlockPos pos = Position.AddCopy(toFacing);
            IMechanicalPowerBlock connectedToBlock = Api.World.BlockAccessor.GetBlock(pos) as IMechanicalPowerBlock;
            if (DEBUG) Api.Logger.Notification("tryConnect at " + this.Position + " towards " + toFacing + " " + pos);

            if (connectedToBlock == null || !fitsConnector(connectedToBlock) || !connectedToBlock.HasMechPowerConnectorAt(Api.World, pos, toFacing.Opposite, Block as BlockMPBase)) return false;
            BEBehaviorMPBase node = Api.World.BlockAccessor.GetBlockEntity(pos)?.GetBehavior<BEBehaviorMPBase>();
            newNetwork = connectedToBlock.GetNetwork(Api.World, pos);
            if (newNetwork != null)
            {
                if (!node.isNetworkConnectedAt(toFacing.Opposite)) return false;
                connectedToBlock.DidConnectAt(Api.World, pos, toFacing.Opposite);  //do this first to set the new Angled Gear block correctly prior to getting propagation direction
                float ratio = node.GetGearedRatio(toFacing);
                if (ratio == 0) return false;
                if (network?.HasPowerProducer == true && !newNetwork.HasPowerProducer && node.AngleTransform == null)
                {
                    return node.tryConnect(toFacing.Opposite, out unloadedChunkPos);
                }

                MechPowerPath curPath = new MechPowerPath(toFacing, ratio, pos, !node.IsPropagationDirection(Position, toFacing));
                curPath.turnDir = node.GetPropagatingTurnDir(toFacing.Opposite);
                curPath.AngleTransform = node.AngleTransform;

                JoinNetwork(newNetwork, curPath);
                newNetwork.ConnectLoad(node, this);
                MechPowerPath[] paths = GetMechPowerExits(curPath);

                for (int i = 0; i < paths.Length; i++)
                {
                    if (paths[i].OutFacing == toFacing) continue;
                    if (DEBUG) Api.Logger.Notification("== spreading path " + (paths[i].invert ? "-" : "") + paths[i].OutFacing + "  " + paths[i].gearingRatio);
                    BlockPos exitPos = Position.AddCopy(paths[i].OutFacing);

                    bool chunkLoaded = spreadTo(Api, newNetwork, exitPos, paths[i], out unloadedChunkPos);
                    if (!chunkLoaded)
                    {
                        return false;
                    }
                }

                return true;
            }
            else
            {
                if (this.network != null)
                {
                    if (node != null) return node.tryConnect(toFacing.Opposite, out unloadedChunkPos);
                }
                else if (node is BEBehaviorMPSpurGear spurGear && spurGear.ExternalSpurFaceCount > 1)
                {
                    spurGear.RefreshPowerConnections();
                    return this.network != null;
                }
            }

            return false;
        }

        protected virtual bool isNetworkConnectedAt(BlockFacing face)
        {
            return network != null;
        }

        bool fitsConnector(IMechanicalPowerBlock other)
        {
            return (Block as BlockMPBase)?.HasCompatibleConnector(other as BlockMPBase) != false;
        }

        public virtual void JoinNetwork(MechanicalNetwork network)
        {
            if (this.network != null && this.network != network)
            {
                LeaveNetwork();
            }

            if (this.network == null)
            {
                this.network = network;
                network?.Join(this);
            }

            if (network == null) NetworkId = 0;
            else
            {
                NetworkId = network.networkId;
            }

            Blockentity.MarkDirty();
        }

        public void JoinNetwork(MechanicalNetwork network, MechPowerPath path)
        {
            if (this.network != null && this.network != network) LeaveNetwork();
            SetPropagationDirection(path);
            JoinNetwork(network);
        }

        public virtual void LeaveNetwork()
        {
            if (DEBUG) Api.Logger.Notification("Leaving network " + NetworkId + " at " + this.Position);
            lastKnownAngleRad = UnwrappedAngleRad;
            network?.Leave(this);
            network = null;
            NetworkId = 0;
            angleTransform = null;
            GearingConflict = false;
            Blockentity.MarkDirty();
        }

        public List<MechanicalNetwork> GetConnectedNetworks()
        {
            List<MechanicalNetwork> networks = [];
            if (Network != null) networks.Add(Network);

            IBlockAccessor blockAccessor = Api.World.BlockAccessor;
            BlockMPBase ownBlock = Block as BlockMPBase;
            BlockPos pos = Position.Copy();
            foreach (BlockFacing face in BlockFacing.ALLFACES)
            {
                face.IterateThruFacingOffsets(pos);
                if (blockAccessor.GetBlock(pos) is not IMechanicalPowerBlock block
                    || ownBlock?.HasMechPowerConnectorAt(Api.World, Position, face, block as BlockMPBase) != true
                    || !block.HasMechPowerConnectorAt(Api.World, pos, face.Opposite, ownBlock)) continue;

                MechanicalNetwork network = blockAccessor.GetBlockEntity(pos)?.GetBehavior<BEBehaviorMPBase>()?.Network;
                if (network != null) networks.AddIfNotPresent(network);
            }

            return networks;
        }

        public void RebuildNetworks(List<MechanicalNetwork> networks, MechanicalNetwork removedFromNetwork = null)
        {
            manager?.RebuildNetworks(networks, removedFromNetwork == null ? null : this);
        }

        public override void OnBlockBroken(IPlayer byPlayer = null)
        {
            this.disconnected = true;
        }

        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();
            if (network != null)
            {
                manager.OnNodeRemoved(this);
            }

            LeaveNetwork();
            manager.RemoveDeviceForRender(this);
        }

        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();
            network?.DidUnload(this);
            manager?.RemoveDeviceForRender(this);
        }


        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            lightRbs = Api.World.BlockAccessor.GetLightRGBs(Blockentity.Pos);

            return true;
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);

            if (network == null && worldAccessForResolve.Side == EnumAppSide.Server)
            {
                propagationDir = BlockFacing.ALLFACES[tree.GetInt("turnDirFromFacing")];
                gearedRatio = tree.GetFloat("g", gearedRatio);
            }

            long nowNetworkId = tree.GetLong("networkid");
            if (worldAccessForResolve.Side == EnumAppSide.Client)
            {
                bool networkChanged = NetworkId != nowNetworkId;
                if (networkChanged && network != null) LeaveNetwork();

                propagationDir = BlockFacing.ALLFACES[tree.GetInt("turnDirFromFacing")];
                gearedRatio = tree.GetFloat("g");
                angleTransform = LinkageAngleTransform.FromTreeAttributes(tree.GetTreeAttribute("linkageAngleTransform"));

                if (networkChanged)
                {
                    NetworkId = nowNetworkId;
                    if (NetworkId != 0 && manager != null)
                    {
                        JoinNetwork(manager.GetOrCreateNetwork(NetworkId));
                    }
                }
            }

            if (network == null) lastKnownAngleRad = tree.GetDecimal("angleRad", lastKnownAngleRad);

            placedStack = tree.GetItemstack("mechanicalMaterialStack");
            placedStack?.ResolveBlockOrItem(worldAccessForResolve);
            updateMaterialUvOffsets();

            SetOrientations();
            updateShape(worldAccessForResolve);
        }

        protected virtual void updateShape(IWorldAccessor worldForResolve)
        {
            Shape = GetShape();
        }


        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetLong("networkid", NetworkId);
            tree.SetInt("turnDirFromFacing", propagationDir.Index);
            tree.SetFloat("g", gearedRatio);
            tree.SetDouble("angleRad", UnwrappedAngleRad);
            if (angleTransform == null)
            {
                tree.RemoveAttribute("linkageAngleTransform");
            }
            else
            {
                ITreeAttribute transformTree = new TreeAttribute();
                angleTransform.ToTreeAttributes(transformTree);
                tree["linkageAngleTransform"] = transformTree;
            }

            if (placedStack == null) tree.RemoveAttribute("mechanicalMaterialStack");
            else tree.SetItemstack("mechanicalMaterialStack", placedStack);
        }

        public override void OnLoadCollectibleMappings(IWorldAccessor worldForNewMappings, Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping, int schematicSeed, bool resolveImports)
        {
            base.OnLoadCollectibleMappings(worldForNewMappings, oldBlockIdMapping, oldItemIdMapping, schematicSeed, resolveImports);
            if (placedStack == null) return;

            if (!placedStack.FixMapping(oldBlockIdMapping, oldItemIdMapping, worldForNewMappings))
            {
                placedStack = null;
            }
            else
            {
                placedStack.Collectible.OnLoadCollectibleMappings(worldForNewMappings, new DummySlot(placedStack), oldBlockIdMapping, oldItemIdMapping, resolveImports);
            }
            updateMaterialUvOffsets();
        }

        public override void OnStoreCollectibleMappings(Dictionary<int, AssetLocation> blockIdMapping, Dictionary<int, AssetLocation> itemIdMapping)
        {
            base.OnStoreCollectibleMappings(blockIdMapping, itemIdMapping);
            placedStack?.Collectible?.OnStoreCollectibleMappings(Api.World, new DummySlot(placedStack), blockIdMapping, itemIdMapping);
        }

        protected virtual float? getSpeedForDisplay(IPlayer forPlayer) => CurrentSpeed;

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder sb)
        {
            float? speed = getSpeedForDisplay(forPlayer);
            if (speed.HasValue) sb.AppendLine(Lang.Get("mechanical-speed-rpm", Math.Abs(speed.Value) * rpmPerSpeedUnit));

            if (Api.World.EntityDebugMode)
            {
                float effectiveRatio = EffectiveGearedRatio;
                float inverseRatio = Math.Abs(effectiveRatio) > 0.000001f ? 1 / effectiveRatio : 0;
                sb.AppendLine(string.Format("<font color='#ccc'>networkid: {0}  turnDir: {1}  {2}  {3:G3}</font>", NetworkId, propagationDir, network?.TurnDir.ToString(), gearedRatio));
                sb.AppendLine(string.Format("<font color='#ccc'>speed: {0:G4}  avail torque: {1:G4}  torque sum: {2:G4}  resist sum: {3:G4}</font>", network?.Speed * effectiveRatio, network?.TotalAvailableTorque * inverseRatio, network?.NetworkTorque * inverseRatio, network?.NetworkResistance * inverseRatio));
                sb.AppendLine(string.Format("<font color='#ccc'>local load: {0:G4}</font>", network?.GetLoad(this)));
            }
        }

        public virtual BlockFacing GetPropagationDirection()
        {
            return propagationDir;
        }

        public virtual BlockFacing GetPropagationDirectionInput()
        {
            return propagationDir;
        }

        public virtual bool IsPropagationDirection(BlockPos fromPos, BlockFacing test)
        {
            return propagationDir == test;
        }

        public virtual void SetPropagationDirection(MechPowerPath path)
        {
            BlockFacing turnDir = path.NetworkDir();
            if (this.propagationDir == turnDir.Opposite && this.network != null)
            {
                if (!network.DirectionHasReversed) network.TurnDir = network.TurnDir == EnumRotDirection.Clockwise ? EnumRotDirection.Counterclockwise : EnumRotDirection.Clockwise;
                network.DirectionHasReversed = true;
            }
            this.propagationDir = turnDir;
            this.GearedRatio = path.gearingRatio;
            angleTransform = path.AngleTransform;
            if (DEBUG) Api.Logger.Notification("setting dir " + this.propagationDir + " " + this.Position);
        }

        public bool GearingConflict;

        public bool HasGearingConflict(MechPowerPath path)
        {
            float expectedRatio = GetGearedRatio(path.OutFacing);
            return !isPropagationDirectionConsistent(path)
                   || Math.Abs(path.gearingRatio - expectedRatio) > 0.01f * Math.Max(1f, Math.Abs(expectedRatio));
        }

        protected virtual bool isPropagationDirectionConsistent(MechPowerPath path)
        {
            return propagationDir == path.NetworkDir();
        }

        public virtual float GetTorque(long tick, float speed, out float resistance)
        {
            resistance = GetResistance() + (GearingConflict ? 1000000f : 0f);
            return 0f;
        }

        public abstract float GetResistance();

        public virtual float GetTorqueResponse(float speed, float nextSpeed) => 0f;

        public virtual float GetRotationalInertia() => (Block as BlockMPBase)?.RotationalInertia ?? 1f;

        public virtual void DestroyJoin(BlockPos pos)
        {
        }


#region Network Discovery

        public virtual MechPowerPath GetNetworkDiscoveryPath()
        {
            return new MechPowerPath(GetPropagationDirectionInput(), gearedRatio, null, false, angleTransform);
        }

        public virtual MechanicalNetwork CreateJoinAndDiscoverNetwork(MechPowerPath path, double retainedAngle)
        {
            MechanicalNetwork newNetwork = manager.CreateNetwork(this);
            newNetwork.TotalAngleRad = retainedAngle;
            JoinNetwork(newNetwork, path);

            List<MechPowerPath> paths = new(GetMechPowerExits(path));
            BlockFacing inputFace = path.OutFacing.Opposite;
            if (!paths.Exists(exit => exit.OutFacing == inputFace))
            {
                paths.Add(path.PropagatedClone(inputFace, !path.invert, path.NetworkDir()));
            }

            BlockPos exitPos = Position.Copy();
            foreach (MechPowerPath exit in paths)
            {
                exitPos.Set(Position).Add(exit.OutFacing);
                BlockMPBase neighbour = Api.World.BlockAccessor.GetBlock(exitPos) as BlockMPBase;
                if (neighbour != null && (Block as IMechanicalPowerBlock)?.HasMechPowerConnectorAt(Api.World, Position, exit.OutFacing, neighbour) != true) continue;
                if (spreadTo(Api, newNetwork, exitPos, exit, out Vec3i missingChunkPos)) continue;

                newNetwork.AwaitChunkThenDiscover(missingChunkPos);
                manager.testFullyLoaded(newNetwork);
                break;
            }

            return newNetwork;
        }


        public virtual MechanicalNetwork CreateJoinAndDiscoverNetwork(BlockFacing powerOutFacing)
        {
            BlockPos neibPos = Position.AddCopy(powerOutFacing);
            IMechanicalPowerBlock neibMechBlock = null;
            neibMechBlock = Api.World.BlockAccessor.GetBlock(neibPos) as IMechanicalPowerBlock;
            if (neibMechBlock != null && (!fitsConnector(neibMechBlock) || !neibMechBlock.HasMechPowerConnectorAt(Api.World, neibPos, powerOutFacing.Opposite, Block as BlockMPBase))) neibMechBlock = null;    // neibMechBlock might be an axle or angled gears pointing the wrong way, so with no available connector -> not on the same network

            MechanicalNetwork neibNetwork = neibMechBlock == null ? null : neibMechBlock.GetNetwork(Api.World, neibPos);

            if (neibNetwork == null || !neibNetwork.Valid)
            {
                MechanicalNetwork newNetwork = this.network;
                bool createdNetwork = newNetwork == null;
                double retainedAngle = UnwrappedAngleRad;
                if (createdNetwork)
                {
                    SetPropagationDirection(new MechPowerPath(powerOutFacing, 1));
                    newNetwork = manager.CreateNetwork(this);
                    newNetwork.TotalAngleRad = IsRotationReversed() ? -retainedAngle : retainedAngle;
                    JoinNetwork(newNetwork);
                    if (DEBUG) Api.Logger.Notification("===setting inturn at " + Position + " " + powerOutFacing);
                }

                MechPowerPath path = new(GetPropagationDirection(), gearedRatio, null, false, angleTransform);
                bool chunksLoaded = spreadTo(Api, newNetwork, neibPos, path, out Vec3i missingChunkPos);

                if (!chunksLoaded)
                {
                    network.AwaitChunkThenDiscover(missingChunkPos);
                    manager.testFullyLoaded(network); // To trigger that allFullyLoaded gets false
                    return network;
                }

                IMechanicalPowerDevice node = Api.World.BlockAccessor.GetBlock(neibPos).GetInterface<IMechanicalPowerDevice>(Api.World, neibPos);
                if (node?.Network == newNetwork)
                {
                    BlockFacing facing = node.IsPropagationDirection(Position, powerOutFacing) ? powerOutFacing : powerOutFacing.Opposite;
                    float ratio = node.GetGearedRatio(facing.Opposite);
                    if (ratio != 0)
                    {
                        LinkageAngleTransform transform = node is ILinkageProducer producer ? producer.LinkageAngleTransform : (node as BEBehaviorMPBase)?.AngleTransform;
                        SetPropagationDirection(new MechPowerPath(facing, ratio, neibPos, false, transform));
                    }
                }

                if (createdNetwork)
                {
                    newNetwork.TotalAngleRad = (IsRotationReversed() ? -retainedAngle : retainedAngle) / gearedRatio;
                    newNetwork.DirectionHasReversed = false;
                }
            }
            else
            {
                if (!tryConnect(powerOutFacing, out Vec3i missingChunkPos) && missingChunkPos != null)
                {
                    network.AwaitChunkThenDiscover(missingChunkPos);
                    manager.testFullyLoaded(network);
                }
            }

            return network;
        }

        /// <summary>
        /// Network propagation has a power path direction (for the network discovery process) and normally power transmission will be in the same sense, but it may be inverted (e.g. if a block is inserted in a way which joins two existing networks)
        /// </summary>
        public virtual bool JoinAndSpreadNetworkToNeighbours(ICoreAPI api, MechanicalNetwork network, MechPowerPath exitTurnDir, out Vec3i missingChunkPos)
        {
            missingChunkPos = null;
            if (disconnected) return true;
            if (this.network != null && !isNetworkConnectedAt(exitTurnDir.OutFacing.Opposite)) return true;
            if (this.network?.networkId == network?.networkId)
            {
                if (network != null)
                {
                    if (HasGearingConflict(exitTurnDir))
                    {
                        GearingConflict = true;
                    }
                    if (!LinkageAngleTransform.Matches(angleTransform, exitTurnDir.AngleTransform))
                    {
                        GearingConflict = true;
                    }
                    network.ConnectLoad(exitTurnDir.LoadFrom, this);
                }
                return true;
            }

            if (DEBUG) api.Logger.Notification("Spread to " + this.Position + " with direction " + exitTurnDir.OutFacing + (exitTurnDir.invert ? "-" : "") + " Network:" + network.networkId);
            JoinNetwork(network, exitTurnDir);
            network.ConnectLoad(exitTurnDir.LoadFrom, this);
            (Block as IMechanicalPowerBlock).DidConnectAt(api.World, Position, exitTurnDir.OutFacing.Opposite);

            MechPowerPath[] paths = GetMechPowerExits(exitTurnDir);
            for (int i = 0; i < paths.Length; i++)
            {
                paths = GetMechPowerExits(exitTurnDir);
                bool returnsOverIncomingEdge = paths[i].OutFacing == exitTurnDir.OutFacing.Opposite;
                bool sameSpurMesh = paths[i].spurGearFace == null
                    || exitTurnDir.spurGearFace == null
                    || paths[i].spurGearFace == exitTurnDir.spurGearFace;
                if (returnsOverIncomingEdge && sameSpurMesh) continue;
                if (DEBUG) api.Logger.Notification("-- spreading path " + (paths[i].invert ? "-" : "") + paths[i].OutFacing + "  " + paths[i].gearingRatio);
                BlockPos exitPos = Position.AddCopy(paths[i].OutFacing);
                bool chunkLoaded = spreadTo(api, network, exitPos, paths[i], out missingChunkPos);

                if (!chunkLoaded)
                {
                    //LeaveNetwork();    //we don't want to set the network to null here, as then there would be nowhere to store the missingChunkPos
                    return false;
                }
            }

            return true;
        }

        protected virtual bool spreadTo(ICoreAPI api, MechanicalNetwork network, BlockPos exitPos, MechPowerPath propagatePath, out Vec3i missingChunkPos)
        {
            missingChunkPos = null;
            BEBehaviorMPBase beMechBase = api.World.BlockAccessor.GetBlockEntity(exitPos)?.GetBehavior<BEBehaviorMPBase>();
            IMechanicalPowerBlock mechBlock = beMechBase?.Block as IMechanicalPowerBlock;
            if (DEBUG) api.Logger.Notification("attempting spread to " + exitPos + (beMechBase == null ? " -" : ""));

            if (beMechBase == null && api.World.BlockAccessor.GetChunkAtBlockPos(exitPos) == null)
            {
                if (OutsideMap(api.World.BlockAccessor, exitPos)) return true;  //Network discovery should not fail if there cannot be a block in this position
                missingChunkPos = new Vec3i(exitPos.X / GlobalConstants.ChunkSize, exitPos.InternalY / GlobalConstants.ChunkSize, exitPos.Z / GlobalConstants.ChunkSize);
                return false;
            }

            if (beMechBase != null && fitsConnector(mechBlock) && mechBlock.HasMechPowerConnectorAt(api.World, exitPos, propagatePath.OutFacing.Opposite, Block as BlockMPBase))
            {
                beMechBase.Api = api;
                propagatePath.LoadFrom = this;
                if (!beMechBase.JoinAndSpreadNetworkToNeighbours(api, network, propagatePath, out missingChunkPos))
                {
                    return false;
                }
            }
            else if (DEBUG) api.Logger.Notification("no connector at " + exitPos + " " + propagatePath.OutFacing.Opposite);

            return true;
        }

        private bool OutsideMap(IBlockAccessor blockAccessor, BlockPos exitPos)
        {
            if (exitPos.X < 0 || exitPos.X >= blockAccessor.MapSizeX) return true;
            if (exitPos.Y < 0 || exitPos.Y >= blockAccessor.MapSizeY) return true;
            if (exitPos.Z < 0 || exitPos.Z >= blockAccessor.MapSizeZ) return true;
            return false;
        }

        /// <summary>
        /// Must return the path mechanical power takes, assuming block is aligned so that the given entry direction is a valid path, and preserve inverted state
        /// </summary>
        /// <param name="entryDir"></param>
        /// <returns></returns>
        public virtual MechPowerPath[] GetMechPowerExits(MechPowerPath entryDir)
        {
            // Most blocks - like axles - have two power exits in opposite directions
            return [entryDir, entryDir.PropagatedClone(entryDir.OutFacing.Opposite, !entryDir.invert, fromPos: Position)];
        }

        #endregion

        public override void OnPlacementBySchematic(ICoreServerAPI api, IBlockAccessor blockAccessor, BlockPos pos, Dictionary<int, Dictionary<int, int>> replaceBlocks, int centerrockblockid, Block layerBlock, bool resolveImports)
        {
            base.OnPlacementBySchematic(api, blockAccessor, pos, replaceBlocks, centerrockblockid, layerBlock, resolveImports);
            foreach (var blockFacing in BlockFacing.ALLFACES)
            {
                if (tryConnect(blockFacing)) break;
            }
        }

        public virtual BlockFacing GetPropagatingTurnDir(BlockFacing toFacing)
        {
            return null;
        }
    }


}
