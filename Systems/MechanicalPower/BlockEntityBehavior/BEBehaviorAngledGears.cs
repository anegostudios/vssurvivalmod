using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent.Mechanics
{
    public class BEBehaviorMPAngledGears : BEBehaviorMPBase, IRotatable
    {
        public BlockFacing axis1 = null;
        public BlockFacing axis2 = null;
        private BEBehaviorMPLargeGear3m largeGear;
        public BlockFacing turnDir1 = null;
        public BlockFacing turnDir2 = null;
        public BlockFacing orientation = null;
        public bool newlyPlaced = false;

        const float multiGearPhaseStepDeg = 11.75f;

        readonly FaceMountedGearStacks<BlockAngledGears> gearStacks = new("angledGear");
        public int GearFaces => gearStacks.GetFaces().Value();
        public bool HasGear(BlockFacing face) => gearStacks.Has(face);
        public BlockAngledGears GetGearBlock(BlockFacing face) => gearStacks.GetBlock(face);
        public ItemStack GetFirstGearDrop() => gearStacks.GetFirstDrop();
        public ItemStack[] GetGearDrops() => gearStacks.GetDrops();
        public void GetGearMaterialUvOffset(ICoreClientAPI capi, BlockFacing face, string textureCode, Vec2f offset) => gearStacks.GetMaterialUvOffset(capi, face, textureCode, offset);

        public bool TryAddGear(BlockFacing face, ItemStack gearStack)
        {
            if (!gearStacks.TryAdd(face, gearStack)) return false;

            Blockentity.MarkDirty(true);
            return true;
        }

        public ItemStack RemoveGear(BlockFacing face)
        {
            ItemStack drop = gearStacks.GetDrop(face);
            if (drop == null) return null;

            gearStacks.Remove(face);
            Blockentity.MarkDirty(true);
            return drop;
        }

        public int GearCount => gearStacks.Count;

        public bool IsMultiGearJammed
        {
            get
            {
                bool hasX = HasGear(BlockFacing.EAST) || HasGear(BlockFacing.WEST);
                bool hasY = HasGear(BlockFacing.UP) || HasGear(BlockFacing.DOWN);
                bool hasZ = HasGear(BlockFacing.NORTH) || HasGear(BlockFacing.SOUTH);
                if (hasX && hasY && hasZ) return true;
                if (!multiEngaged) return false;

                bool collinearX = HasGear(BlockFacing.EAST) && HasGear(BlockFacing.WEST);
                bool collinearY = HasGear(BlockFacing.UP) && HasGear(BlockFacing.DOWN);
                bool collinearZ = HasGear(BlockFacing.NORTH) && HasGear(BlockFacing.SOUTH);
                return collinearX && (hasY || hasZ)
                    || collinearY && (hasX || hasZ)
                    || collinearZ && (hasX || hasY);
            }
        }

        public override float AngleRad
        {
            get
            {
                float angle = base.AngleRad;

                bool flip = propagationDir == BlockFacing.DOWN || propagationDir == BlockFacing.WEST;
                if (propagationDir == this.orientation && propagationDir != BlockFacing.NORTH && propagationDir != BlockFacing.SOUTH) flip = !flip;
                //if (flip) return /*lastKnownAngleRad = - why do i do this? it creates massive jitter*/ GameMath.TWOPI - angle;

                return flip ? GameMath.TWOPI - angle : angle;
            }
        }

        public BEBehaviorMPAngledGears(BlockEntity blockentity) : base(blockentity)
        {
        }

        public override void Initialize(ICoreAPI api, JsonObject properties)
        {
            if (Block is BlockAngledGearMulti && tryGetClutchEngaged(api.World.BlockAccessor, out bool engaged))
            {
                multiEngaged = engaged;
            }

            base.Initialize(api, properties);

            if (largeGear != null && this.orientation != null)
            {
                if (largeGear.AngledGearNotAlreadyAdded(this.Position))
                {
                    // during chunk loading it's random whether the angled gear or the large gear gets initialised first
                    this.tryConnect(this.orientation.Opposite);
                }
            }
        }

        public override void SetOrientations()
        {
            if (Block is BlockAngledGearMulti)
            {
                this.orientation = null;
                this.turnDir1 = null;
                this.turnDir2 = null;
                this.axis1 = null;
                this.axis2 = null;
                AxisSign = [0, 0, 0];
                return;
            }

            string orientations = (Block as BlockAngledGears).Orientation;

            // This applies only when the BE is being updated when the gear orientations change after a neighbour block breaks
            if (this.turnDir1 != null)
            {
                if (propagationDir == turnDir1) propagationDir = turnDir2.Opposite;
                else if (propagationDir == turnDir2) propagationDir = turnDir1.Opposite;
                else if (propagationDir == turnDir2.Opposite) propagationDir = turnDir1;
                else if (propagationDir == turnDir1.Opposite) propagationDir = turnDir2;
                this.turnDir1 = null;
                this.turnDir2 = null;
            }

            this.orientation = null;
            switch (orientations)
            {
                case "n":
                case "nn":   //"nn" "ss" "ee" "ww" variants are used for the single cage gear instead of the peg gear (e.g. on interaction with LargeGear3)
                    this.orientation = BlockFacing.NORTH;
                    AxisSign = new int[3] { 0, 0, -1 };   //for these "single direction" gears, the renderertype is generic, i.e. only one mesh, the peg gear, will be rendered
                    axis1 = BlockFacing.NORTH;  //render rotation in the reverse sense for this axis (negative axis)
                    break;
                case "s":
                case "ss":
                    AxisSign = new int[3] { 0, 0, -1 };
                    this.orientation = BlockFacing.SOUTH;
                    break;

                case "e":
                case "ee":
                    AxisSign = new int[3] { 1, 0, 0 };
                    this.orientation = BlockFacing.EAST;
                    break;

                case "w":
                case "ww":
                    AxisSign = new int[3] { -1, 0, 0 };
                    this.orientation = BlockFacing.WEST;
                    axis1 = BlockFacing.WEST;
                    break;

                case "u":
                    AxisSign = new int[3] { 0, -1, 0 };
                    this.orientation = BlockFacing.UP;
                    break;

                case "d":
                    AxisSign = new int[3] { 0, 1, 0 };
                    this.orientation = BlockFacing.DOWN;
                    axis1 = BlockFacing.DOWN;
                    break;

                case "uu":
                    AxisSign = [0, 1, 0];   // Sign flipped so the cage gear co-rotates with its axle.
                    this.orientation = BlockFacing.UP;
                    axis1 = BlockFacing.UP;
                    break;

                case "dd":
                    AxisSign = [0, -1, 0];
                    this.orientation = BlockFacing.DOWN;
                    axis1 = BlockFacing.DOWN;
                    break;

                case "es":
                    AxisSign = new int[6] { 1, 0, 0, 0, 0, -1 };  //for all these "2 directions" gears, the rendererByType is "angledgears" ie. AngledGearBlockRenderer and two meshes will be rendered, the renderer looks for a special 6 member AxisSign array
                    axis1 = BlockFacing.EAST;
                    axis2 = null;
                    this.turnDir1 = BlockFacing.EAST;
                    this.turnDir2 = BlockFacing.SOUTH;
                    break;

                case "ws":
                    AxisSign = new int[6] { 0, 0, -1, -1, 0, 0 };
                    axis1 = BlockFacing.WEST;
                    axis2 = null;
                    this.turnDir1 = BlockFacing.WEST;
                    this.turnDir2 = BlockFacing.SOUTH;
                    break;

                case "nw":
                    AxisSign = new int[6] { 1, 0, 0, 0, 0, -1 };
                    axis1 = null;
                    axis2 = BlockFacing.EAST;
                    this.turnDir1 = BlockFacing.NORTH;
                    this.turnDir2 = BlockFacing.WEST;
                    break;

                case "sd":
                    AxisSign = new int[6] { 0, 0, -1, 0, -1, 0 };
                    axis1 = null;
                    axis2 = null; // BlockFacing.NORTH;
                    this.turnDir1 = BlockFacing.SOUTH;
                    this.turnDir2 = BlockFacing.DOWN;
                    break;

                case "ed":  //1
                    AxisSign = new int[6] { 0, 1, 0, 1, 0, 0 };
                    axis1 = BlockFacing.EAST;
                    axis2 = BlockFacing.DOWN;
                    this.turnDir1 = BlockFacing.EAST;
                    this.turnDir2 = BlockFacing.DOWN;
                    break;

                case "wd":
                    AxisSign = new int[6] { -1, 0, 0, 0, 1, 0 };
                    axis1 = BlockFacing.DOWN;  //1
                    axis2 = BlockFacing.WEST;  //1
                    this.turnDir1 = BlockFacing.WEST;
                    this.turnDir2 = BlockFacing.DOWN;
                    break;

                case "nd":  //north checked
                    AxisSign = new int[6] { 0, 0, -1, 0, 1, 0 };
                    axis1 = BlockFacing.DOWN;   //1
                    axis2 = null;
                    this.turnDir1 = BlockFacing.NORTH;
                    this.turnDir2 = BlockFacing.DOWN;
                    break;

                case "nu": //1
                    AxisSign = new int[6] { 0, -1, 0, 0, 0, -1 };
                    axis1 = BlockFacing.UP;
                    axis2 = null;
                    this.turnDir1 = BlockFacing.NORTH;
                    this.turnDir2 = BlockFacing.UP;
                    break;

                case "eu": //1
                    AxisSign = new int[6] { 0, -1, 0, 1, 0, 0 };
                    axis1 = BlockFacing.UP;   //1
                    axis2 = BlockFacing.EAST;  //1
                    this.turnDir1 = BlockFacing.EAST;
                    this.turnDir2 = BlockFacing.UP;
                    break;

                case "su":   //south,up checked
                    AxisSign = new int[6] { 0, 1, 0, 0, 0, -1 };
                    axis1 = BlockFacing.DOWN;
                    axis2 = null;
                    this.turnDir1 = BlockFacing.SOUTH;
                    this.turnDir2 = BlockFacing.UP;
                    break;

                case "wu": //1
                    AxisSign = new int[6] { 0, -1, 0, -1, 0, 0 };
                    axis1 = BlockFacing.WEST;  //1
                    axis2 = BlockFacing.UP;   //1
                    this.turnDir1 = BlockFacing.WEST;
                    this.turnDir2 = BlockFacing.UP;
                    break;

                case "en":
                    AxisSign = new int[6] { 0, 0, 1, 1, 0, 0 };
                    axis1 = BlockFacing.SOUTH;
                    axis2 = BlockFacing.NORTH;
                    this.turnDir1 = BlockFacing.EAST;
                    this.turnDir2 = BlockFacing.NORTH;
                    break;

                default:
                    AxisSign = new int[6] { 0, 0, 1, 1, 0, 0 };
                    axis1 = null;
                    axis2 = null;
                    break;
            }

            if (Api != null) checkLargeGearJoin();
        }

        protected void checkLargeGearJoin()
        {
            if (Block is BlockAngledGearMulti) return;

            string orientations = (Block as BlockAngledGears).Orientation;
            if (orientations.Length == 2 && orientations[0] == orientations[1])
            {
                BlockPos largeGearPos = this.Position.AddCopy(this.orientation.Opposite);
                BlockEntity be = Api.World.BlockAccessor.GetBlockEntity(largeGearPos);
                if (be != null) SetLargeGear(be);
            }
        }

        public void SetLargeGear(BlockEntity be)
        {
            largeGear ??= be.GetBehavior<BEBehaviorMPLargeGear3m>();
        }

        public void AddToLargeGearNetwork(BEBehaviorMPLargeGear3m largeGear, BlockFacing outFacing)
        {
            this.JoinNetwork(largeGear.Network, new MechPowerPath(outFacing, largeGear.GearedRatio * largeGear.ratio, null, largeGear.IsRimInverted(), largeGear.AngleTransform));
            largeGear.Network?.ConnectLoad(largeGear, this);
        }

        public override bool IsInvertedNetworkFor(BlockPos pos)
        {
            return this.orientation != null && this.orientation != propagationDir;
        }

        public float GetLargeGearAngleRad(float unchanged)
        {
            if (largeGear == null)
            {
                BlockPos largeGearPos = this.Position.AddCopy(this.orientation.Opposite);
                BlockEntity be = Api.World.BlockAccessor.GetBlockEntity(largeGearPos);
                if (be != null) SetLargeGear(be);
                if (largeGear == null) return unchanged;
            }
            int dir = this.orientation == BlockFacing.SOUTH ? -1 : 1;
            return dir * largeGear.GetSmallgearAngleRad() % GameMath.TWOPI;
        }

        public override void SetPropagationDirection(MechPowerPath path)
        {
            if (Block is BlockAngledGearMulti)
            {
                networkInputFace = getMultiGearInputFace(path);
            }

            BlockFacing turnDir = path.NetworkDir();
            if (turnDir1 != null)
            {
                if (turnDir == turnDir1) turnDir = turnDir2.Opposite;
                else if (turnDir == turnDir2) turnDir = turnDir1.Opposite;
                else if (turnDir == turnDir2.Opposite) turnDir = turnDir1;
                else if (turnDir == turnDir1.Opposite) turnDir = turnDir2;
            }

            base.SetPropagationDirection(new MechPowerPath(turnDir, path.gearingRatio, null, false, path.AngleTransform));
        }

        public override void LeaveNetwork()
        {
            base.LeaveNetwork();
            networkInputFace = null;
        }

        protected override bool isPropagationDirectionConsistent(MechPowerPath path)
        {
            if (Block is not BlockAngledGearMulti) return IsPropagationDirection(path.FromPos, path.NetworkDir());

            BlockFacing inputFace = getNetworkInputFace();
            BlockFacing arrivalFace = getMultiGearInputFace(path);
            BlockFacing expectedDir = arrivalFace == inputFace || !hasDifferentAxisGear(inputFace)
                ? propagationDir
                : getExitSense(arrivalFace, propagationDir, inputFace);
            return expectedDir == path.NetworkDir();
        }

        public override BlockFacing GetPropagationDirectionInput()
        {
            if (this.turnDir1 != null)
            {
                if (propagationDir == turnDir1) return turnDir2.Opposite;
                if (propagationDir == turnDir2) return turnDir1.Opposite;
                if (propagationDir == turnDir1.Opposite) return turnDir2;
                if (propagationDir == turnDir2.Opposite) return turnDir1;
            }

            return propagationDir;
        }

        public override MechPowerPath GetNetworkDiscoveryPath()
        {
            if (Block is not BlockAngledGearMulti) return base.GetNetworkDiscoveryPath();
            return new MechPowerPath(getNetworkInputFace().Opposite, GearedRatio, null, false, AngleTransform, propagationDir);
        }

        public override bool IsPropagationDirection(BlockPos fromPos, BlockFacing test)
        {
            if (Block is BlockAngledGearMulti && propagationDir != null)
            {
                BlockFacing exitFace = test.Opposite;
                BlockFacing inputFace = getNetworkInputFace();
                if (inputFace != null && HasGear(exitFace) && exitFace != inputFace && hasDifferentAxisGear(inputFace))
                {
                    return getExitSense(exitFace, propagationDir, inputFace) == test;
                }
            }

            if (this.turnDir1 != null)
            {
                if (propagationDir == turnDir1) return propagationDir == test || test == turnDir2.Opposite;
                if (propagationDir == turnDir2) return propagationDir == test || test == turnDir1.Opposite;
                if (propagationDir == turnDir1.Opposite) return propagationDir == test || test == turnDir2;
                if (propagationDir == turnDir2.Opposite) return propagationDir == test || test == turnDir1;
            }

            return propagationDir == test;
        }

        public override void WasPlaced(BlockFacing connectedOnFacing)
        {
            if (Block is BlockAngledGearMulti)
            {
                if (connectedOnFacing != null) tryConnect(connectedOnFacing);
                return;
            }

            // Skip this if already called CreateJoinAndDiscoverNetwork in Initialize()
            if ((Api.Side == EnumAppSide.Client || OutFacingForNetworkDiscovery == null) && connectedOnFacing != null)
            {
                if (connectedOnFacing.Axis == EnumAxis.X)
                {
                    if (!tryConnect(BlockFacing.NORTH) && !tryConnect(BlockFacing.SOUTH))
                    {
                        Api.Logger.Notification("AG was placed fail connect 2nd: " + connectedOnFacing + " at " + Position);
                    }
                    return;
                }

                if (connectedOnFacing.Axis == EnumAxis.Z)
                {
                    if (!tryConnect(BlockFacing.WEST) && !tryConnect(BlockFacing.EAST))
                    {
                        Api.Logger.Notification("AG was placed fail connect 2nd: " + connectedOnFacing + " at " + Position);
                    }
                    return;
                }
            }
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            if (Block is not BlockAngledGearMulti) return;

            gearStacks.ToTreeAttributes(tree);
            tree.SetBool("multiEngaged", multiEngaged);
            tree.SetInt("networkInputFace", networkInputFace?.Index ?? -1);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);
            if (Block is not BlockAngledGearMulti) return;

            gearStacks.FromTreeAttributes(tree, worldAccessForResolve);
            multiEngaged = tree.GetBool("multiEngaged", false);

            // The input face is discovered network state, so only the client takes it from the tree.
            if (worldAccessForResolve.Side == EnumAppSide.Client)
            {
                int inputFaceIndex = tree.GetInt("networkInputFace", -1);
                networkInputFace = inputFaceIndex < 0 ? null : BlockFacing.ALLFACES[inputFaceIndex];
            }
        }

        public override void OnLoadCollectibleMappings(IWorldAccessor worldForNewMappings, Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping, int schematicSeed, bool resolveImports)
        {
            base.OnLoadCollectibleMappings(worldForNewMappings, oldBlockIdMapping, oldItemIdMapping, schematicSeed, resolveImports);
            if (Block is BlockAngledGearMulti) gearStacks.OnLoadCollectibleMappings(worldForNewMappings, oldBlockIdMapping, oldItemIdMapping, resolveImports);
        }

        public override void OnStoreCollectibleMappings(Dictionary<int, AssetLocation> blockIdMapping, Dictionary<int, AssetLocation> itemIdMapping)
        {
            base.OnStoreCollectibleMappings(blockIdMapping, itemIdMapping);
            if (Block is BlockAngledGearMulti) gearStacks.OnStoreCollectibleMappings(Api.World, blockIdMapping, itemIdMapping);
        }

        public void OnTransformed(IWorldAccessor worldAccessor, ITreeAttribute tree, int degreeRotation, Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping, EnumAxis? flipAxis)
        {
            if (Block is BlockAngledGearMulti) gearStacks.OnTransformed(tree, degreeRotation, flipAxis);
        }

        public override void OnBlockBroken(IPlayer byPlayer = null)
        {
            List<MechanicalNetwork> networks = Block is BlockAngledGearMulti ? GetConnectedNetworks() : null;
            MechanicalNetwork ownNetwork = Network;
            base.OnBlockBroken(byPlayer);
            if (networks != null) RebuildNetworks(networks, ownNetwork);
        }

        public override float GetResistance()
        {
            if (Block is BlockAngledGearMulti && IsMultiGearJammed) return 1000000f;
            if (Block is BlockAngledGearMulti) return 0.0005f * Math.Max(1, GearCount);
            return 0.0005f;
        }

        public override float GetRotationalInertia()
        {
            if (Block is not BlockAngledGearMulti) return base.GetRotationalInertia();

            float inertia = 0;
            BlockFacing inputFace = getNetworkInputFace();
            foreach (BlockFacing face in BlockFacing.ALLFACES)
            {
                if (inputFace != null && !hasMultiGearConnection(inputFace, face)) continue;
                BlockAngledGears gear = gearStacks.GetBlock(face);
                if (gear != null) inertia += gear.RotationalInertia;
            }
            return inertia;
        }

        // Opposite gears only pass power when a perpendicular clutch is engaged.
        bool multiEngaged;
        // The gear face the current network actually arrived through. Rotation direction cannot tell the two collinear faces apart
        BlockFacing networkInputFace;

        public BlockFacing GetCollinearGearFace()
        {
            if (Block is not BlockAngledGearMulti) return null;
            foreach (BlockFacing face in BlockFacing.ALLFACES)
            {
                if (HasGear(face) && HasGear(face.Opposite)) return face;
            }
            return null;
        }

        public bool IsCollinearHub => GetCollinearGearFace() != null;

        public void CheckEngagedByClutch(IBlockAccessor access, bool updateNetwork)
        {
            if (!tryGetClutchEngaged(access, out bool clutchEngaged) || clutchEngaged == multiEngaged) return;

            multiEngaged = clutchEngaged;
            Blockentity.MarkDirty(true);
            if (!updateNetwork) return;
            if (!multiEngaged)
            {
                manager?.OnNodeRemoved(this);
                return;
            }

            BlockAngledGearMulti.RefreshPowerConnections(Api.World, Position);
        }

        bool tryGetClutchEngaged(IBlockAccessor access, out bool engaged)
        {
            engaged = false;
            BlockFacing axisDir = GetCollinearGearFace();
            if (axisDir == null) return true;

            bool unknown = false;
            BlockPos clutchPos = Position.Copy();
            foreach (BlockFacing side in BlockFacing.ALLFACES)
            {
                side.IterateThruFacingOffsets(clutchPos);
                if (!side.IsAdjacent(axisDir)) continue;

                if (access.GetChunkAtBlockPos(clutchPos) == null)
                {
                    unknown = true;
                    continue;
                }
                if (access.GetBlock(clutchPos) is not BlockClutch) continue;

                BEClutch clutch = access.GetBlockEntity(clutchPos) as BEClutch;
                if (clutch == null)
                {
                    unknown = true;
                    continue;
                }

                BlockFacing facing = clutch.Facing ?? BlockFacing.FromCode(clutch.Block.Variant["side"]);
                if (facing == side.Opposite && clutch.Engaged)
                {
                    engaged = true;
                    return true;
                }
            }

            return !unknown;
        }

        public float GetMultiGearAngleRad(BlockFacing face)
        {
            if (Block is not BlockAngledGearMulti || !HasGear(face) || IsMultiGearJammed) return 0f;
            BlockFacing netDir = propagationDir;
            BlockFacing inputFace = getNetworkInputFace();
            if (inputFace == null) return 0f;
            if (!hasDifferentAxisGear(inputFace)) return face == inputFace ? AngleRad : 0f;
            BlockFacing sense = face == inputFace ? netDir : getExitSense(face, netDir, inputFace);
            return getSpin(sense) == getSpin(netDir) ? AngleRad : GameMath.TWOPI - AngleRad;
        }
        public float GetMultiGearRenderAngleOffset(BlockFacing face)
        {
            BlockFacing referenceFace = getReferenceFace(face);
            float? meshPhaseDeg = GetGearBlock(face)?.MeshPhaseDeg;
            if (meshPhaseDeg == null) return multiGearPhaseStepDeg * GameMath.DEG2RAD * referenceFace.Index;

            foreach (BlockFacing otherFace in BlockFacing.ALLFACES)
            {
                if (otherFace.IsAdjacent(face) && HasGear(otherFace) && getReferenceFace(otherFace).Index < referenceFace.Index) return meshPhaseDeg.Value * GameMath.DEG2RAD;
            }
            return 0f;
        }

        BlockFacing getReferenceFace(BlockFacing face)
        {
            return HasGear(face.Opposite) && face.Opposite.Index < face.Index ? face.Opposite : face;
        }

        BlockFacing getMultiGearInputFace(BlockFacing netDir)
        {
            if (netDir == null) return null;
            if (HasGear(netDir.Opposite)) return netDir.Opposite;
            if (HasGear(netDir)) return netDir;
            return null;
        }

        BlockFacing getNetworkInputFace()
        {
            return networkInputFace ?? getMultiGearInputFace(propagationDir);
        }

        BlockFacing getMultiGearInputFace(MechPowerPath path)
        {
            if (path?.OutFacing == null) return null;

            if (path.FromPos != null)
            {
                BlockFacing face = path.FromPos.FacingFrom(Position);
                if (HasGear(face)) return face;
            }

            if (HasGear(path.OutFacing.Opposite)) return path.OutFacing.Opposite;
            if (HasGear(path.OutFacing)) return path.OutFacing;
            return null;
        }

        static int getSpin(BlockFacing facing)
        {
            return facing == BlockFacing.UP || facing == BlockFacing.SOUTH || facing == BlockFacing.WEST ? 1 : -1;
        }

        BlockFacing getExitSense(BlockFacing exitFace, BlockFacing netDir, BlockFacing inputFace)
        {
            bool flip = netDir == inputFace;
            // An opposite gear is driven through the perpendicular idler.
            if (exitFace == inputFace.Opposite) flip = !flip;
            return flip ? exitFace.Opposite : exitFace;
        }

        bool hasDifferentAxisGear(BlockFacing inputFace)
        {
            foreach (BlockFacing face in BlockFacing.ALLFACES)
            {
                if (HasGear(face) && face.IsAdjacent(inputFace)) return true;
            }
            return false;
        }

        bool hasMultiGearConnection(BlockFacing inputFace, BlockFacing outputFace)
        {
            if (!HasGear(inputFace) || !HasGear(outputFace)) return false;
            if (inputFace == outputFace || hasDifferentAxisGear(inputFace)) return true;
            return multiEngaged && outputFace == inputFace.Opposite;
        }

        protected override bool isNetworkConnectedAt(BlockFacing face)
        {
            if (Block is not BlockAngledGearMulti) return base.isNetworkConnectedAt(face);

            BlockFacing inputFace = getNetworkInputFace();
            return inputFace != null && hasMultiGearConnection(inputFace, face);
        }

        MechPowerPath[] getMultiGearExits(MechPowerPath fromExitTurnDir)
        {
            BlockFacing inputSide = getMultiGearInputFace(fromExitTurnDir);
            if (inputSide == null) return [];

            if (!hasDifferentAxisGear(inputSide))
            {
                if (!hasMultiGearConnection(inputSide, inputSide.Opposite)) return [];
                return base.GetMechPowerExits(fromExitTurnDir);
            }

            BlockFacing netDir = fromExitTurnDir.NetworkDir();
            List<MechPowerPath> paths = [];
            foreach (BlockFacing face in BlockFacing.ALLFACES)
            {
                if (!HasGear(face) || face == inputSide) continue;
                BlockFacing exitSense = getExitSense(face, netDir, inputSide);
                paths.Add(new MechPowerPath(face, GearedRatio, null, exitSense == face.Opposite, fromExitTurnDir.AngleTransform));
            }
            return paths.ToArray();
        }

        public override BlockFacing GetPropagatingTurnDir(BlockFacing toFacing)
        {
            if (Block is BlockAngledGearMulti && propagationDir != null)
            {
                BlockFacing inputFace = getNetworkInputFace();
                if (inputFace != null && HasGear(toFacing) && toFacing != inputFace && hasDifferentAxisGear(inputFace))
                {
                    return getExitSense(toFacing, propagationDir, inputFace);
                }
            }

            return base.GetPropagatingTurnDir(toFacing);
        }

        public override MechPowerPath[] GetMechPowerExits(MechPowerPath fromExitTurnDir)
        {
            if (Block is BlockAngledGearMulti) return getMultiGearExits(fromExitTurnDir);

            // This method could be called from another (earlier in the loading chunk) block's Initialise() method, i.e. before this itself is initialised.
            if (this.orientation == null) this.SetOrientations();

            string orientations = (Block as BlockAngledGears).Orientation;
            if (orientations.Length < 2 || orientations[0] != orientations[1])
            {
                bool invert = fromExitTurnDir.invert;
                BlockFacing[] connectors = (Block as BlockAngledGears).Facings;
                BlockFacing inputSide = fromExitTurnDir.OutFacing;
                if (!connectors.Contains(inputSide))
                {
                    inputSide = inputSide.Opposite;
                    invert = !invert;
                }

                // The code for removing the inputSide from the MechPowerExits is unwanted for a newly placed AngledGears block - it needs to seek networks on both faces if newly placed
                if (!newlyPlaced)
                {
                    connectors = connectors.Remove(inputSide);
                }
                MechPowerPath[] paths = new MechPowerPath[connectors.Length];
                for (int i = 0; i < paths.Length; i++)
                {
                    BlockFacing pathFacing = connectors[i];

                    // An angled gear's output side rotates in the opposite sense from the input side
                    paths[i] = new MechPowerPath(pathFacing, this.GearedRatio, null, pathFacing == inputSide ? invert : !invert, fromExitTurnDir.AngleTransform);
                }
                return paths;
            }
            else
            {
                // Alternative code for a small gear connected to a Large Gear - essentially pass through
                MechPowerPath[] paths = new MechPowerPath[2];
                paths[0] = fromExitTurnDir.PropagatedClone(this.orientation.Opposite, this.orientation == fromExitTurnDir.OutFacing ? !fromExitTurnDir.invert : fromExitTurnDir.invert);
                paths[1] = fromExitTurnDir.PropagatedClone(this.orientation, this.orientation == fromExitTurnDir.OutFacing ? fromExitTurnDir.invert : !fromExitTurnDir.invert);
                return paths;
            }
        }

        protected override float? getSpeedForDisplay(IPlayer forPlayer)
        {
            if (Block is not BlockAngledGearMulti) return base.getSpeedForDisplay(forPlayer);
            if (IsMultiGearJammed) return 0;

            int index = forPlayer?.CurrentBlockSelection?.SelectionBoxIndex ?? -1;
            foreach (BlockFacing face in BlockFacing.ALLFACES)
            {
                if (!HasGear(face)) continue;
                if (index-- != 0) continue;

                BEBehaviorMPBase support = Api.World.BlockAccessor.GetBlockEntity(Position.AddCopy(face))?.GetBehavior<BEBehaviorMPBase>();
                if (support != null) return support.CurrentSpeed;

                BlockFacing inputFace = getNetworkInputFace();
                return inputFace != null && (face == inputFace || hasDifferentAxisGear(inputFace)) ? CurrentSpeed : 0;
            }
            return null;
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder sb)
        {
            base.GetBlockInfo(forPlayer, sb);
            if (Api.World.EntityDebugMode)
            {
                string orientations = Block.Variant["orientation"];
                bool rev = propagationDir == axis1 || propagationDir == axis2;
                sb.AppendLine(string.Format(Lang.Get("Orientation: {0} {1} {2}", orientations, this.orientation, rev ? "-" : "")));
            }
        }

        public void ClearLargeGear()
        {
            this.largeGear = null;
        }
    }
}
