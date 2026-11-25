using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent.Mechanics
{
    public class BEBehaviorMPSpurGear : BEBehaviorMPBase, IRotatable
    {
        public BlockFacing Facing => spurBlock?.SpurFacing ?? BlockFacing.FromFirstLetter(Block.Variant["orientation"][0]);
        BlockSpurGear spurBlock => Block as BlockSpurGear;
        int toothCount => spurBlock?.ToothCount ?? 16;

        // Extra discs belong to arbors that share this cell.
        readonly HostedSpurGears extraDiscs = new("extraSpur");

        float angleOffset;
        public override float AngleRad => base.AngleRad + angleOffset;

        public BEBehaviorMPSpurGear(BlockEntity blockentity) : base(blockentity)
        {
        }

        public override void Initialize(ICoreAPI api, JsonObject properties)
        {
            base.Initialize(api, properties);
            resolveThroughShaftRenderBlock(api.World);

            angleOffset = 180f / toothCount * GameMath.DEG2RAD * (Pos.X % 32 + Pos.Y % 32 + Pos.Z % 32)
                + (spurBlock?.MeshPhaseDeg ?? 0f) * GameMath.DEG2RAD;

            AxisSign = [0, 0, 0];
            switch (Facing.Index)
            {
                case 0: // N
                case 2: // S
                    AxisSign[2] = -1;
                    break;
                case 1: // E
                case 3: // W
                    AxisSign[0] = -1;
                    break;
                case 4: // U
                case 5: // D
                    AxisSign[1] = 1;
                    break;
            }
        }

        // Independent arbors passing through this cell.
        public int ExternalSpurFaceCount => (spurBlock?.SpurFacings.Length ?? 0) + extraDiscs.Count;

        public bool HasExternalSpurFace(BlockFacing face)
        {
            return spurBlock?.HasGearFacing(face) == true || extraDiscs.Has(face);
        }

        bool isExternalSpurFaceSupported(BlockFacing face)
        {
            if (!HasExternalSpurFace(face)) return false;
            return BlockSpurGear.HasDiscSupport(Api.World, Pos, face);
        }

        public bool HasExtraExternalSpurFace(BlockFacing face) => extraDiscs.Has(face);

        public bool TryAddExternalSpur(BlockFacing face, ItemStack gearStack, ref string failureCode)
        {
            if (face == null || gearStack?.Block is not BlockSpurGear discBlock || spurBlock == null || face.IsAdjacent(Facing))
            {
                failureCode = "requiresaxle";
                return false;
            }

            if (!spurBlock.HasCompatibleConnector(discBlock))
            {
                failureCode = "incompatibleconnector";
                return false;
            }

            if (HasThroughShaft || HasExternalSpurFace(face) || !extraDiscs.TryAdd(face, gearStack))
            {
                failureCode = "notreplaceable";
                return false;
            }

            updateShape(Api.World);
            Blockentity.MarkDirty(true);
            Api.World.BlockAccessor.MarkBlockDirty(Position);
            RefreshPowerConnections(face);
            return true;
        }

        // Returns 0 if the face has no disc.
        public int GetExternalSpurToothCount(BlockFacing face)
        {
            if (spurBlock?.HasGearFacing(face) == true) return toothCount;
            return extraDiscs.GetToothCount(face);
        }

        // Returns 0 if the face has no disc.
        public float GetExternalSpurRadius(BlockFacing face)
        {
            if (spurBlock?.HasGearFacing(face) == true) return spurBlock.PitchRadius;
            return extraDiscs.GetRadius(face);
        }

        public bool TryGetExternalSpur(BlockFacing face, out int toothCount, out float radius)
        {
            toothCount = GetExternalSpurToothCount(face);
            radius = GetExternalSpurRadius(face);
            return toothCount > 0 && radius > 0f;
        }

        public ItemStack[] GetExtraExternalSpurDrops() => extraDiscs.GetDrops();

        public ItemStack GetExtraExternalSpurDrop(BlockFacing face) => extraDiscs.GetDrop(face);

        public BlockSpurGear GetExtraDiscBlock(BlockFacing face) => extraDiscs.GetDiscBlock(face);

        public void GetExtraDiscMaterialUvOffset(ICoreClientAPI capi, BlockFacing face, string textureCode, Vec2f offset) => extraDiscs.GetMaterialUvOffset(capi, face, textureCode, offset);

        // A merged axle carries the arbor through this cell.
        ItemStack throughShaftStack;
        Block throughShaftRenderBlock;
        public bool HasThroughShaft => throughShaftStack?.Block is BlockAxle;

        public bool TryAddThroughShaft(ItemStack axleStack)
        {
            if (HasThroughShaft || axleStack?.Block is not BlockAxle axleBlock || ExternalSpurFaceCount > 1) return false;
            if (spurBlock?.HasCompatibleConnector(axleBlock) == false) return false;

            throughShaftStack = axleStack.Clone();
            throughShaftStack.StackSize = 1;
            resolveThroughShaftRenderBlock(Api.World);
            Blockentity.MarkDirty(true);
            Api.World.BlockAccessor.MarkBlockDirty(Position);
            RefreshPowerConnections(Facing.Opposite);
            return true;
        }

        public ItemStack GetThroughShaftDrop() => HasThroughShaft ? throughShaftStack.Clone() : null;

        public Block GetThroughShaftRenderBlock() => throughShaftRenderBlock;

        public void GetThroughShaftMaterialUvOffset(ICoreClientAPI capi, string textureCode, Vec2f offset)
        {
            if (HasThroughShaft && throughShaftRenderBlock is BlockMPBase shaftBlock)
            {
                shaftBlock.GetMaterialUvOffset(capi, throughShaftStack, textureCode, offset);
                return;
            }

            offset.X = 0;
            offset.Y = 0;
        }

        void resolveThroughShaftRenderBlock(IWorldAccessor world)
        {
            BlockAxle throughShaftBlock = throughShaftStack?.Block as BlockAxle;
            if (throughShaftBlock == null)
            {
                throughShaftRenderBlock = null;
                return;
            }

            string axisCode = Facing.Axis switch
            {
                EnumAxis.X => "we",
                EnumAxis.Y => "ud",
                _ => "ns"
            };
            throughShaftRenderBlock = world.GetBlock(throughShaftBlock.CodeWithVariant("rotation", axisCode))
                ?? throughShaftBlock;
        }

        public float GetShaftRenderAngle(BEBehaviorMPAxle axleBeh)
        {
            return axleBeh?.AngleRad ?? base.AngleRad;
        }

        // Drops the cell back to one disc when an arbor breaks.
        public void RemoveExtraExternalSpur(BlockFacing face, bool dropItem)
        {
            if (!HasExtraExternalSpurFace(face)) return;

            List<MechanicalNetwork> networks = GetConnectedNetworks();

            if (dropItem && Api.World.Side == EnumAppSide.Server)
            {
                ItemStack stack = extraDiscs.GetDrop(face);
                if (stack != null) Api.World.SpawnItemEntity(stack, Position.ToVec3d().Add(0.5, 0.5, 0.5));
            }

            extraDiscs.Remove(face);
            Blockentity.MarkDirty(true);
            Api.World.BlockAccessor.MarkBlockDirty(Position);

            RebuildNetworks(networks);
            if (Network == null) RefreshPowerConnections(spurBlock?.SpurFacing);
        }

        public float GetDiscRenderAngle(BlockFacing face, BEBehaviorMPAxle axleBeh)
        {
            float angle = axleBeh?.AngleRad ?? base.AngleRad;
            return angle + getDiscAngleOffset(face);
        }

        float getDiscAngleOffset(BlockFacing face)
        {
            if (spurBlock?.HasGearFacing(face) == true) return angleOffset;

            int teeth = GetExternalSpurToothCount(face);
            float phase = GetExtraDiscBlock(face)?.MeshPhaseDeg ?? 0f;
            return 180f / Math.Max(1, teeth) * GameMath.DEG2RAD * (Pos.X % 32 + Pos.Y % 32 + Pos.Z % 32)
                + phase * GameMath.DEG2RAD;
        }

        SmallBoolArray forwardingFaces;

        public override void JoinNetwork(MechanicalNetwork network)
        {
            if (ExternalSpurFaceCount > 1)
            {
                if (Network != null) LeaveNetwork();
                return;
            }

            base.JoinNetwork(network);
        }

        public override bool JoinAndSpreadNetworkToNeighbours(ICoreAPI api, MechanicalNetwork network, MechPowerPath exitTurnDir, out Vec3i missingChunkPos)
        {
            missingChunkPos = null;
            if (disconnected) return true;
            if (ExternalSpurFaceCount <= 1) return base.JoinAndSpreadNetworkToNeighbours(api, network, exitTurnDir, out missingChunkPos);

            BlockFacing activeFace = getActiveExternalFace(exitTurnDir);
            if (activeFace == null) return true;
            if (forwardingFaces[activeFace.Index])
            {
                network.ConnectLoad(exitTurnDir.LoadFrom, this);
                return true;
            }

            if (Network != null) LeaveNetwork();

            forwardingFaces[activeFace.Index] = true;
            try
            {
                SetPropagationDirection(exitTurnDir);
                network.ConnectLoad(exitTurnDir.LoadFrom, this);
                MechPowerPath[] paths = GetMechPowerExits(exitTurnDir);
                BlockPos exitPos = new BlockPos(Position.dimension);
                foreach (MechPowerPath path in paths)
                {
                    if (!spreadTo(api, network, exitPos.Set(Position).Add(path.OutFacing), path, out missingChunkPos)) return false;
                }
            }
            finally
            {
                forwardingFaces[activeFace.Index] = false;
            }

            return true;
        }

        public override void OnBlockBroken(IPlayer byPlayer = null)
        {
            List<MechanicalNetwork> networks = GetConnectedNetworks();
            MechanicalNetwork ownNetwork = Network;
            base.OnBlockBroken(byPlayer);
            RebuildNetworks(networks, ownNetwork);
        }

        public void RefreshPowerConnections(BlockFacing preferredFace = null)
        {
            if (ExternalSpurFaceCount > 1)
            {
                RebuildNetworks(GetConnectedNetworks());
                return;
            }

            if (Network != null)
            {
                manager?.OnNodeRemoved(this);
                return;
            }

            List<BlockFacing> faces = [];
            if (preferredFace != null)
            {
                faces.AddIfNotPresent(preferredFace);
                faces.AddIfNotPresent(preferredFace.Opposite);
            }
            foreach (BlockFacing face in BlockFacing.ALLFACES) faces.AddIfNotPresent(face);

            foreach (BlockFacing face in faces)
            {
                if (tryConnect(face)) break;
            }
        }

        public override MechPowerPath[] GetMechPowerExits(MechPowerPath entryDir)
        {
            List<MechPowerPath> paths = [];
            BlockFacing entrySide = entryDir.OutFacing.Opposite;
            BlockFacing activeFace = getActiveExternalFace(entryDir);
            if (activeFace == null) return paths.ToArray();

            if (ExternalSpurFaceCount <= 1)
            {
                foreach (BlockFacing face in BlockFacing.ALLFACES)
                {
                    if (face.IsAdjacent(Facing) || face == entrySide) continue;
                    if (!HasThroughShaft && !HasExternalSpurFace(face)) continue;
                    paths.Add(entryDir.PropagatedClone(face, entryDir.invert, propagationDir));
                }
            }
            else if (activeFace != entrySide)
            {
                paths.Add(entryDir.PropagatedClone(activeFace, entryDir.invert, propagationDir));
            }

            foreach (BlockFacing face in BlockFacing.ALLFACES)
            {
                if (!face.IsAdjacent(Facing)) continue;
                if (tryGetMeshingToothCount(face, activeFace, out int otherTeeth)) paths.Add(createMeshedPath(face, activeFace, otherTeeth, entryDir));
            }

            return paths.ToArray();
        }

        BlockFacing getActiveExternalFace(MechPowerPath entryDir)
        {
            if (isExternalSpurFaceSupported(entryDir?.spurGearFace)) return entryDir.spurGearFace;

            BlockFacing entrySide = entryDir?.OutFacing?.Opposite;
            if (isExternalSpurFaceSupported(entrySide)) return entrySide;

            if (entryDir?.FromPos != null)
            {
                BlockFacing side = entryDir.FromPos.FacingFrom(Position);
                if (side.IsAdjacent(Facing))
                {
                    BlockFacing meshingFace = getMeshingFaceOnSide(side);
                    if (meshingFace != null) return meshingFace;
                }
                return null;
            }

            foreach (BlockFacing face in BlockFacing.ALLFACES)
            {
                if (isExternalSpurFaceSupported(face)) return face;
            }

            return null;
        }

        BlockFacing getMeshingFaceOnSide(BlockFacing sideFace)
        {
            if (sideFace == null || !sideFace.IsAdjacent(Facing)) return null;

            var neighbourPos = new FastBlockPos(Position).Offset(sideFace);
            IBlockAccessor blockAccessor = Api.World.BlockAccessor;
            BlockEntity neighbourBe = blockAccessor.GetBlockEntity(neighbourPos);
            Block block = neighbourBe?.Block ?? blockAccessor.GetBlock(neighbourPos);
            if (block is BlockSpurGear gear)
            {
                BEBehaviorMPSpurGear other = neighbourBe?.GetBehavior<BEBehaviorMPSpurGear>();
                foreach (BlockFacing gearFace in BlockFacing.ALLFACES)
                {
                    if (!isExternalSpurFaceSupported(gearFace)) continue;
                    if (tryGetOtherSpurToothCount(gear, other, gearFace, out _)) return gearFace;
                }
            }
            else if (block is BlockAxle)
            {
                BEBehaviorMPAxle axle = neighbourBe?.GetBehavior<BEBehaviorMPAxle>();
                foreach (BlockFacing gearFace in BlockFacing.ALLFACES)
                {
                    if (!isExternalSpurFaceSupported(gearFace)) continue;
                    if (axle?.CanHostedGearMesh(gearFace, GetExternalSpurRadius(gearFace), GetExternalSpurToothCount(gearFace)) == true) return gearFace;
                }
            }

            return null;
        }

        bool tryGetMeshingToothCount(BlockFacing sideFace, BlockFacing activeFace, out int toothCount)
        {
            toothCount = 0;
            if (activeFace == null || !HasExternalSpurFace(activeFace)) return false;

            var neighbourPos = new FastBlockPos(Position).Offset(sideFace);
            IBlockAccessor blockAccessor = Api.World.BlockAccessor;
            BlockEntity neighbourBe = blockAccessor.GetBlockEntity(neighbourPos);
            Block block = neighbourBe?.Block ?? blockAccessor.GetBlock(neighbourPos);
            if (block is BlockSpurGear gear)
            {
                BEBehaviorMPSpurGear other = neighbourBe?.GetBehavior<BEBehaviorMPSpurGear>();
                return tryGetOtherSpurToothCount(gear, other, activeFace, out toothCount);
            }

            if (block is BlockAxle)
            {
                BEBehaviorMPAxle axle = neighbourBe?.GetBehavior<BEBehaviorMPAxle>();
                if (axle?.CanHostedGearMesh(activeFace, GetExternalSpurRadius(activeFace), GetExternalSpurToothCount(activeFace)) == true)
                {
                    toothCount = axle.GetHostedSpurToothCount(activeFace);
                    return toothCount > 0;
                }
            }

            return false;
        }

        bool tryGetOtherSpurToothCount(BlockSpurGear gear, BEBehaviorMPSpurGear other, BlockFacing activeFace, out int toothCount)
        {
            toothCount = 0;
            if (gear == null || activeFace == null) return false;

            float otherRadius = 0f;
            if (other != null)
            {
                other.TryGetExternalSpur(activeFace, out toothCount, out otherRadius);
            }
            else if (gear.HasGearFacing(activeFace))
            {
                otherRadius = gear.PitchRadius;
                toothCount = gear.ToothCount;
            }

            return BlockSpurGear.CanMesh(GetExternalSpurRadius(activeFace), GetExternalSpurToothCount(activeFace), otherRadius, toothCount);
        }

        MechPowerPath createMeshedPath(BlockFacing outFacing, BlockFacing activeFace, int otherToothCount, MechPowerPath entryDir)
        {
            float ratio = GetExternalSpurToothCount(activeFace) / (float)otherToothCount;
            return new MechPowerPath(outFacing, entryDir.gearingRatio * ratio, null, !entryDir.invert,
                entryDir.AngleTransform, propagationDir.Opposite, activeFace);
        }


        // Paths pulled from a neighbour still need the tooth ratio added during normal spread.
        public override void SetPropagationDirection(MechPowerPath path)
        {
            if (path.FromPos != null)
            {
                BlockFacing face = path.FromPos.FacingFrom(Pos);
                if (face.IsAdjacent(Facing))
                {
                    IBlockAccessor blockAccessor = Api.World.BlockAccessor;
                    BlockEntity neighbourBe = blockAccessor.GetBlockEntity(path.FromPos);
                    Block block = neighbourBe?.Block ?? blockAccessor.GetBlock(path.FromPos);
                    if (block is BlockSpurGear neighGear)
                    {
                        BlockFacing activeFace = getMeshingFaceOnSide(face) ?? getActiveExternalFace(path);
                        BEBehaviorMPSpurGear neighBeh = neighbourBe?.GetBehavior<BEBehaviorMPSpurGear>();
                        if (tryGetOtherSpurToothCount(neighGear, neighBeh, activeFace, out int neighTeeth))
                        {
                            path.gearingRatio *= neighTeeth / (float)GetExternalSpurToothCount(activeFace);
                        }
                    }
                    else if (block is BlockAxle)
                    {
                        BEBehaviorMPAxle axle = neighbourBe?.GetBehavior<BEBehaviorMPAxle>();
                        BlockFacing activeFace = getMeshingFaceOnSide(face) ?? getActiveExternalFace(path);
                        if (activeFace != null)
                        {
                            float ownTeeth = GetExternalSpurToothCount(activeFace);
                            int hostedTeeth = axle?.CanHostedGearMesh(activeFace, GetExternalSpurRadius(activeFace), GetExternalSpurToothCount(activeFace)) == true
                                ? axle.GetHostedSpurToothCount(activeFace)
                                : 0;
                            if (hostedTeeth > 0 && ownTeeth > 0) path.gearingRatio *= hostedTeeth / ownTeeth;
                        }
                    }
                }
            }

            base.SetPropagationDirection(path);
        }

        public override BlockFacing GetPropagatingTurnDir(BlockFacing toFacing)
        {
            if (toFacing != null && toFacing.Axis == Facing.Axis) return propagationDir;
            return propagationDir.Opposite;
        }


        public override float GetResistance()
        {
            return 0.0005f;
        }

        public override float GetRotationalInertia()
        {
            BlockAxle shaft = throughShaftStack?.Block as BlockAxle;
            return base.GetRotationalInertia() + (shaft?.RotationalInertia ?? 0f);
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder sb)
        {
            if (ExternalSpurFaceCount > 1)
            {
                BlockFacing face = getSelectedExternalFace(forPlayer);
                if (face != null)
                {
                    BEBehaviorMPAxle axle = Api.World.BlockAccessor.GetBlockEntity(Pos.AddCopy(face))?.GetBehavior<BEBehaviorMPAxle>();
                    if (axle != null)
                    {
                        axle.GetBlockInfo(forPlayer, sb);
                        return;
                    }
                }
            }

            base.GetBlockInfo(forPlayer, sb);
        }

        BlockFacing getSelectedExternalFace(IPlayer forPlayer)
        {
            return spurBlock?.GetSelectedGearFace(Api.World, Pos, forPlayer?.CurrentBlockSelection?.SelectionBoxIndex ?? -1);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);

            extraDiscs.FromTreeAttributes(tree, worldAccessForResolve);
            throughShaftStack = tree.GetItemstack("throughShaftStack");
            throughShaftStack?.ResolveBlockOrItem(worldAccessForResolve);
            resolveThroughShaftRenderBlock(worldAccessForResolve);
            updateShape(worldAccessForResolve);
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            extraDiscs.ToTreeAttributes(tree);
            if (!HasThroughShaft) tree.RemoveAttribute("throughShaftStack");
            else tree.SetItemstack("throughShaftStack", throughShaftStack);
        }

        public override void OnLoadCollectibleMappings(IWorldAccessor worldForNewMappings, Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping, int schematicSeed, bool resolveImports)
        {
            base.OnLoadCollectibleMappings(worldForNewMappings, oldBlockIdMapping, oldItemIdMapping, schematicSeed, resolveImports);
            extraDiscs.OnLoadCollectibleMappings(worldForNewMappings, oldBlockIdMapping, oldItemIdMapping, resolveImports);

            if (throughShaftStack != null)
            {
                if (!throughShaftStack.FixMapping(oldBlockIdMapping, oldItemIdMapping, worldForNewMappings)
                    || throughShaftStack.Block is not BlockAxle)
                {
                    throughShaftStack = null;
                }
                else
                {
                    throughShaftStack.Collectible.OnLoadCollectibleMappings(worldForNewMappings, new DummySlot(throughShaftStack), oldBlockIdMapping, oldItemIdMapping, resolveImports);
                }
            }

            resolveThroughShaftRenderBlock(worldForNewMappings);
            updateShape(worldForNewMappings);
        }

        public override void OnStoreCollectibleMappings(Dictionary<int, AssetLocation> blockIdMapping, Dictionary<int, AssetLocation> itemIdMapping)
        {
            base.OnStoreCollectibleMappings(blockIdMapping, itemIdMapping);
            extraDiscs.OnStoreCollectibleMappings(Api.World, blockIdMapping, itemIdMapping);
            if (HasThroughShaft) throughShaftStack.Collectible.OnStoreCollectibleMappings(Api.World, new DummySlot(throughShaftStack), blockIdMapping, itemIdMapping);
        }

        public void OnTransformed(IWorldAccessor worldAccessor, ITreeAttribute tree, int degreeRotation, Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping, EnumAxis? flipAxis)
        {
            extraDiscs.OnTransformed(tree, degreeRotation, flipAxis);
        }
    }
}
