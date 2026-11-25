using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent.Mechanics
{
    public class BlockSpurGear : BlockMPBase
    {
        const float selectionThickness = 0.25f;
        const float collisionThickness = 0.1875f;
        const float gearClearanceTolerance = 0.02f;
        const float toothPitchTolerance = 0.001f;
        const float spurOuterRadiusScale = 9f / 8f;

        public static Cuboidf GetSelectionBox(BlockFacing face, float radius) => getFaceSlab(face, selectionThickness, radius);
        public static Cuboidf GetCollisionBox(BlockFacing face, float radius) => getFaceSlab(face, collisionThickness, radius);
        public static bool CanMesh(float radius, float otherRadius) => Math.Abs(radius + otherRadius - 1f) < gearClearanceTolerance;

        public static bool CanMesh(float radius, int teeth, float otherRadius, int otherTeeth)
        {
            // Radius metadata rounds thirds to five decimal places!
            return teeth > 0 && otherTeeth > 0 && CanMesh(radius, otherRadius)
                && Math.Abs(radius * otherTeeth - otherRadius * teeth) < toothPitchTolerance;
        }

        // Disc slab against its mounting face.
        static Cuboidf getFaceSlab(BlockFacing face, float thickness, float radius)
        {
            float lo = GameMath.Clamp(0.5f - radius, 0f, 1f);
            float hi = GameMath.Clamp(0.5f + radius, 0f, 1f);
            switch (face.Index)
            {
                case 0: return new Cuboidf(lo, lo, 0f, hi, hi, thickness);          // North
                case 1: return new Cuboidf(1f - thickness, lo, lo, 1f, hi, hi);     // East
                case 2: return new Cuboidf(lo, lo, 1f - thickness, hi, hi, 1f);     // South
                case 3: return new Cuboidf(0f, lo, lo, thickness, hi, hi);          // West
                case 4: return new Cuboidf(lo, 1f - thickness, lo, hi, 1f, hi);     // Up
                default: return new Cuboidf(lo, 0f, lo, hi, thickness, hi);         // Down
            }
        }

        protected BlockFacing orientation;
        protected BlockFacing[] orientations;
        public BlockFacing SpurFacing => orientation;
        public BlockFacing[] SpurFacings => orientations ?? (orientation == null ? [] : [orientation]);
        public int ToothCount { get; private set; } = 16;
        public float PitchRadius { get; private set; } = 0.5f;

        public float MeshPhaseDeg { get; private set; } = 0f;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            loadOrientations();
            ToothCount = Attributes?["spurGearTeeth"].AsInt(16) ?? 16;
            PitchRadius = Attributes?["spurGearRadius"].AsFloat(ToothCount / 32f) ?? (ToothCount / 32f);
            MeshPhaseDeg = Attributes?["spurGearMeshPhaseDeg"].AsFloat(0f) ?? 0f;
        }

        void loadOrientations()
        {
            string orientationCode = (Variant["orientation"] ?? "").ToLowerInvariant();
            BlockFacing[] facings = orientationCode.Distinct().Select(BlockFacing.FromFirstLetter).Where(face => face != null).ToArray();

            orientations = facings.Length == 0 ? [BlockFacing.SOUTH] : facings;
            orientation = orientations[0];
        }

        static Cuboidf getShaftColumn(EnumAxis axis)
        {
            if (axis == EnumAxis.X) return new Cuboidf(0f, 0.375f, 0.375f, 1f, 0.625f, 0.625f);
            if (axis == EnumAxis.Y) return new Cuboidf(0.375f, 0f, 0.375f, 0.625f, 1f, 0.625f);
            return new Cuboidf(0.375f, 0.375f, 0f, 0.625f, 0.625f, 1f);
        }

        public override Cuboidf[] GetSelectionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            if (orientation == null) return base.GetSelectionBoxes(blockAccessor, pos);
            return getGearBoxes(blockAccessor, pos, selectionThickness);
        }

        public override Cuboidf[] GetCollisionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            if (orientation == null) return base.GetCollisionBoxes(blockAccessor, pos);
            return getGearBoxes(blockAccessor, pos, collisionThickness);
        }

        Cuboidf[] getGearBoxes(IBlockAccessor blockAccessor, BlockPos pos, float thickness)
        {
            List<Cuboidf> boxes = [];
            foreach (BlockFacing face in SpurFacings) boxes.Add(getFaceSlab(face, thickness, PitchRadius));
            BEBehaviorMPSpurGear gear = blockAccessor.GetBlockEntity(pos)?.GetBehavior<BEBehaviorMPSpurGear>();
            if (gear != null)
            {
                foreach (BlockFacing face in BlockFacing.ALLFACES)
                {
                    if (gear.HasExtraExternalSpurFace(face)) boxes.Add(getFaceSlab(face, thickness, gear.GetExternalSpurRadius(face)));
                }
                if (gear.HasThroughShaft) boxes.Add(getShaftColumn(orientation.Axis));
            }
            return boxes.ToArray();
        }

        public BlockFacing GetSelectedGearFace(IWorldAccessor world, BlockPos pos, int selectionBoxIndex)
        {
            if (selectionBoxIndex >= 0 && selectionBoxIndex < SpurFacings.Length) return SpurFacings[selectionBoxIndex];

            int index = selectionBoxIndex - SpurFacings.Length;
            BEBehaviorMPSpurGear gear = world.BlockAccessor.GetBlockEntity(pos)?.GetBehavior<BEBehaviorMPSpurGear>();
            if (gear == null) return null;
            return BlockFacing.ALLFACES.Where(gear.HasExtraExternalSpurFace).ElementAtOrDefault(index);
        }

        public bool HasGearFacing(BlockFacing face)
        {
            return face != null && SpurFacings.Contains(face);
        }

        public bool HasGearFacingAt(IWorldAccessor world, BlockPos pos, BlockFacing face)
        {
            return HasGearFacing(face)
                || world.BlockAccessor.GetBlockEntity(pos)?.GetBehavior<BEBehaviorMPSpurGear>()?.HasExtraExternalSpurFace(face) == true;
        }

        public float GetGearRadiusAt(IWorldAccessor world, BlockPos pos, BlockFacing face)
        {
            BEBehaviorMPSpurGear beh = world.BlockAccessor.GetBlockEntity(pos)?.GetBehavior<BEBehaviorMPSpurGear>();
            return getGearRadius(beh, face);
        }

        float getGearRadius(BEBehaviorMPSpurGear beh, BlockFacing face)
        {
            if (beh?.HasExternalSpurFace(face) == true) return beh.GetExternalSpurRadius(face);
            return HasGearFacing(face) ? PitchRadius : 0f;
        }

        bool meshesWithAt(IWorldAccessor world, BlockPos pos, BlockSpurGear other, BlockPos otherPos)
        {
            BEBehaviorMPSpurGear ownBeh = world.BlockAccessor.GetBlockEntity(pos)?.GetBehavior<BEBehaviorMPSpurGear>();
            BEBehaviorMPSpurGear otherBeh = world.BlockAccessor.GetBlockEntity(otherPos)?.GetBehavior<BEBehaviorMPSpurGear>();
            foreach (BlockFacing gearFace in BlockFacing.ALLFACES)
            {
                float radius = getGearRadius(ownBeh, gearFace);
                float otherRadius = other.getGearRadius(otherBeh, gearFace);
                int teeth = ownBeh?.GetExternalSpurToothCount(gearFace) ?? (HasGearFacing(gearFace) ? ToothCount : 0);
                int otherTeeth = otherBeh?.GetExternalSpurToothCount(gearFace) ?? (other.HasGearFacing(gearFace) ? other.ToothCount : 0);
                if (CanMesh(radius, teeth, otherRadius, otherTeeth)) return true;
            }
            return false;
        }

        public override bool HasMechPowerConnectorAt(IWorldAccessor world, BlockPos pos, BlockFacing face, BlockMPBase forBlock)
        {
            if (!HasCompatibleConnector(forBlock)) return false;

            if (face.Axis == orientation.Axis)
            {
                if (HasGearFacing(face)) return true;

                BEBehaviorMPSpurGear beh = world.BlockAccessor.GetBlockEntity(pos)?.GetBehavior<BEBehaviorMPSpurGear>();
                return beh != null && (beh.HasExtraExternalSpurFace(face) || beh.HasThroughShaft);
            }

            if (forBlock is BlockAxle)
            {
                BlockPos axlePos = pos.AddCopy(face);
                BEBehaviorMPAxle axle = world.BlockAccessor.GetBlockEntity(axlePos)?.GetBehavior<BEBehaviorMPAxle>();
                return axle?.GetHostedMeshingFace(world, pos, this) != null && !axle.IsAxialFace(face.Opposite);
            }

            return forBlock is BlockSpurGear spurGear && meshesWithAt(world, pos, spurGear, pos.AddCopy(face));
        }

        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            return GetBEBehavior<BEBehaviorMPBase>(pos)?.GetPlacedStack() ?? new ItemStack(world.GetBlock(CodeWithVariant("orientation", "s")));
        }

        public override AssetLocation GetRotatedBlockCode(int angle)
        {
            return getTransformedBlockCode(angle, null);
        }

        public override AssetLocation GetHorizontallyFlippedBlockCode(EnumAxis axis)
        {
            return getTransformedBlockCode(0, axis);
        }

        public override AssetLocation GetVerticallyFlippedBlockCode()
        {
            return getTransformedBlockCode(0, EnumAxis.Y);
        }

        AssetLocation getTransformedBlockCode(int angle, EnumAxis? flipAxis)
        {
            BlockFacing facing = getVariantFacing();
            if (facing.Axis == flipAxis) facing = facing.Opposite;
            return CodeWithVariant("orientation", $"{facing.GetHorizontalRotated(angle).Code[0]}");
        }

        BlockFacing getVariantFacing() => BlockFacing.FromFirstLetter(Variant["orientation"][0]);

        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1f)
        {
            List<ItemStack> drops = [..base.GetDrops(world, pos, byPlayer, dropQuantityMultiplier)];
            BEBehaviorMPSpurGear gear = world.BlockAccessor.GetBlockEntity(pos)?.GetBehavior<BEBehaviorMPSpurGear>();
            if (gear != null)
            {
                drops.AddRange(gear.GetExtraExternalSpurDrops());
                ItemStack shaft = gear.GetThroughShaftDrop();
                if (shaft != null) drops.Add(shaft);
            }
            return drops.ToArray();
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            var hotbarSlot = byPlayer?.InventoryManager?.ActiveHotbarSlot;
            if (hotbarSlot?.Itemstack?.Block is BlockAngledGears) return true;

            if (hotbarSlot?.Itemstack?.Block is BlockAxle)
            {
                BEBehaviorMPSpurGear gearBeh = world.BlockAccessor.GetBlockEntity(blockSel.Position)?.GetBehavior<BEBehaviorMPSpurGear>();
                if (gearBeh != null && gearBeh.TryAddThroughShaft(hotbarSlot.Itemstack))
                {
                    if (byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative)
                    {
                        hotbarSlot.TakeOut(1);
                        hotbarSlot.MarkDirty();
                    }
                    world.PlaySoundAt(new AssetLocation("sounds/block/planks"), blockSel.Position, 0, byPlayer);
                    return true;
                }
            }

            if (hotbarSlot?.Itemstack?.Block is BlockSpurGear)
            {
                string failureCode = "";
                if (tryAddExternalSpurToExisting(world, hotbarSlot.Itemstack, blockSel.Position, blockSel.Face, ref failureCode))
                {
                    if (byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative)
                    {
                        hotbarSlot.TakeOut(1);
                        hotbarSlot.MarkDirty();
                    }
                    return true;
                }

                BlockFacing mountFace = GetSelectedGearFace(world, blockSel.Position, blockSel.SelectionBoxIndex) ?? blockSel.Face;
                if (!shouldRouteToHostedGear(world, blockSel.Position, mountFace))
                {
                    return base.OnBlockInteractStart(world, byPlayer, blockSel);
                }

                if (tryPlaceGearOnGear(world, hotbarSlot.Itemstack, blockSel.Position, mountFace, ref failureCode))
                {
                    if (byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative)
                    {
                        hotbarSlot.TakeOut(1);
                        hotbarSlot.MarkDirty();
                    }
                    return true;
                }

                return !string.IsNullOrEmpty(failureCode);
            }

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }


        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
        {
            if (blockSel?.Face != null)
            {
                BlockPos clickedPos = blockSel.Position.AddCopy(blockSel.Face.Opposite);
                if (world.BlockAccessor.GetBlockEntity(clickedPos)?.GetBehavior<BEBehaviorMPAxle>() != null)
                {
                    return TryPlaceGearOnAxle(world, itemstack, clickedPos, blockSel.Face, ref failureCode, byPlayer.Entity.Controls.CtrlKey, blockSel);
                }

                if (world.BlockAccessor.GetBlock(clickedPos) is BlockSpurGear mountGear)
                {
                    BlockFacing mountFace = mountGear.GetSelectedGearFace(world, clickedPos, blockSel.SelectionBoxIndex) ?? blockSel.Face;
                    if (shouldRouteToHostedGear(world, clickedPos, mountFace))
                    {
                        return tryPlaceGearOnGear(world, itemstack, clickedPos, mountFace, ref failureCode);
                    }
                }
            }

            if (world.BlockAccessor.GetBlock(blockSel.Position) is BlockSpurGear)
            {
                return tryAddExternalSpurToExisting(world, itemstack, blockSel.Position, blockSel.Face, ref failureCode);
            }

            if (!CanPlaceBlock(world, byPlayer, blockSel, ref failureCode))
            {
                return false;
            }

            BlockFacing preferredFace = blockSel.Face?.Opposite;
            BlockPos axlePos = blockSel.Position.Copy();
            if (preferredFace != null)
            {
                axlePos.Add(preferredFace);
                if (tryPlaceOnNeighbourAxle(world, itemstack, axlePos, preferredFace, ref failureCode)) return true;
            }

            axlePos.Set(blockSel.Position);
            foreach (BlockFacing face in BlockFacing.ALLFACES)
            {
                face.IterateThruFacingOffsets(axlePos);
                if (face != preferredFace && tryPlaceOnNeighbourAxle(world, itemstack, axlePos, face, ref failureCode)) return true;
            }

            if (string.IsNullOrEmpty(failureCode)) failureCode = "requiresaxle";
            return false;
        }

        bool tryPlaceOnNeighbourAxle(IWorldAccessor world, ItemStack gearStack, BlockPos axlePos, BlockFacing faceToAxle, ref string failureCode)
        {
            if (world.BlockAccessor.GetBlock(axlePos) is not BlockAxle) return false;

            string faceFailure = null;
            if (TryPlaceGearOnAxle(world, gearStack, axlePos, faceToAxle.Opposite, ref faceFailure)) return true;
            if (string.IsNullOrEmpty(failureCode)) failureCode = faceFailure;
            return false;
        }

        static bool shouldRouteToHostedGear(IWorldAccessor world, BlockPos mountGearPos, BlockFacing clickedFace)
        {
            BlockSpurGear mountGear = world.BlockAccessor.GetBlock(mountGearPos) as BlockSpurGear;
            return mountGear?.HasGearFacingAt(world, mountGearPos, clickedFace) == true;
        }

        static bool tryAddExternalSpurToExisting(IWorldAccessor world, ItemStack gearStack, BlockPos gearPos, BlockFacing face, ref string failureCode)
        {
            if (face == null || gearStack?.Block is not BlockSpurGear gearBlock)
            {
                failureCode = "requiresaxle";
                return false;
            }

            IBlockAccessor blockAccessor = world.BlockAccessor;
            BlockEntity gearBe = blockAccessor.GetBlockEntity(gearPos);
            if (gearBe?.Block is not BlockSpurGear existing)
            {
                failureCode = "requiresaxle";
                return false;
            }

            BEBehaviorMPSpurGear gear = gearBe.GetBehavior<BEBehaviorMPSpurGear>();
            if (gear == null)
            {
                failureCode = "requiresaxle";
                return false;
            }

            if (face.IsAdjacent(existing.SpurFacing) || gear.HasExternalSpurFace(face))
            {
                failureCode = "requiresaxle";
                return false;
            }

            BlockPos axlePos = gearPos.AddCopy(face);
            BlockEntity axleBe = blockAccessor.GetBlockEntity(axlePos);
            if (axleBe?.Block is not BlockAxle axle || !axle.IsOrientedTo(face.Opposite))
            {
                failureCode = "requiresaxle";
                return false;
            }

            if (!axle.HasCompatibleConnector(gearBlock))
            {
                failureCode = "incompatibleconnector";
                return false;
            }

            BEBehaviorMPAxle axleBeh = axleBe.GetBehavior<BEBehaviorMPAxle>();
            if (axleBeh == null)
            {
                failureCode = "requiresaxle";
                return false;
            }

            if (wouldGearClip(world, axlePos, face.Opposite, gearBlock.PitchRadius, false))
            {
                failureCode = "notenoughspace";
                return false;
            }

            if (!gear.TryAddExternalSpur(face, gearStack, ref failureCode)) return false;
            axle.DidConnectAt(world, axlePos, face.Opposite);
            blockAccessor.MarkBlockDirty(axlePos);
            axleBeh.RefreshPowerConnections(face.Opposite);
            return true;
        }

        static bool tryPlaceGearOnGear(IWorldAccessor world, ItemStack gearStack, BlockPos mountGearPos, BlockFacing mountFace, ref string failureCode)
        {
            if (mountFace == null || gearStack?.Block is not BlockSpurGear) return false;

            BlockSpurGear mountGear = world.BlockAccessor.GetBlock(mountGearPos) as BlockSpurGear;
            if (mountGear == null || !mountGear.HasGearFacingAt(world, mountGearPos, mountFace))
            {
                failureCode = "requiresaxle";
                return false;
            }

            BlockPos axlePos = mountGearPos.AddCopy(mountFace);
            BlockFacing axleGearFace = mountFace.Opposite;
            return tryAddHostedSpurOnAxle(world, gearStack, axlePos, axleGearFace, ref failureCode);
        }

        static bool tryAddHostedSpurOnAxle(IWorldAccessor world, ItemStack gearStack, BlockPos axlePos, BlockFacing preferredFace, ref string failureCode)
        {
            if (gearStack?.Block is not BlockSpurGear gearBlock) return false;

            BEBehaviorMPAxle axle = world.BlockAccessor.GetBlockEntity(axlePos)?.GetBehavior<BEBehaviorMPAxle>();
            if (axle == null)
            {
                failureCode = "requiresaxle";
                return false;
            }

            if (preferredFace == null || !axle.IsAxialFace(preferredFace))
            {
                failureCode = "requiresaxle";
                return false;
            }

            if (axle.HasSpurGear(preferredFace))
            {
                failureCode = "notreplaceable";
                return false;
            }

            if (wouldGearClip(world, axlePos, preferredFace, gearBlock.PitchRadius, true))
            {
                failureCode = "notenoughspace";
                return false;
            }

            if (!axle.TryAddSpurGear(preferredFace, gearStack, ref failureCode)) return false;
            BlockPos endPos = axlePos.AddCopy(preferredFace);
            (world.BlockAccessor.GetBlock(endPos) as IMechanicalPowerBlock)?.DidConnectAt(world, endPos, preferredFace.Opposite);
            world.BlockAccessor.MarkBlockDirty(endPos);
            axle.RefreshPowerConnections(preferredFace);
            return true;
        }

        static bool wouldGearClip(IWorldAccessor world, BlockPos axleCell, BlockFacing endFace, float pitchRadius, bool placeHosted)
        {
            BlockPos discCell = placeHosted ? axleCell.Copy() : axleCell.AddCopy(endFace);
            BlockFacing discFace = placeHosted ? endFace : endFace.Opposite;

            BlockPos neighbourDisc = new(axleCell.dimension);
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        int distanceSq = dx * dx + dy * dy + dz * dz;
                        if (distanceSq == 0) continue;

                        int axialOffset = endFace.Axis switch
                        {
                            EnumAxis.X => dx,
                            EnumAxis.Y => dy,
                            _ => dz
                        };
                        if (axialOffset != 0) continue;

                        neighbourDisc.Set(discCell).Add(dx, dy, dz);
                        float neighbourRadius = getDiscRadius(world, neighbourDisc, discFace);
                        if (neighbourRadius <= 0f) continue;

                        float clearanceRadius = neighbourRadius + pitchRadius;
                        if (distanceSq == 2) clearanceRadius *= spurOuterRadiusScale;
                        clearanceRadius -= gearClearanceTolerance;
                        if (clearanceRadius * clearanceRadius > distanceSq) return true;
                    }
                }
            }
            return wouldCrossAxisGearClip(world, discCell, discFace, pitchRadius);
        }

        // Discs on another axis cut through this one instead of sitting beside it.
        static bool wouldCrossAxisGearClip(IWorldAccessor world, BlockPos discCell, BlockFacing discFace, float pitchRadius)
        {
            IBlockAccessor blockAccessor = world.BlockAccessor;
            BlockPos neighbour = new(discCell.dimension);
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        neighbour.Set(discCell).Add(dx, dy, dz);
                        BlockEntity be = blockAccessor.GetBlockEntity(neighbour);
                        if (be == null) continue;

                        BEBehaviorMPAxle axle = be.GetBehavior<BEBehaviorMPAxle>();
                        BlockSpurGear gearBlock = be.Block as BlockSpurGear;
                        if (axle == null && gearBlock == null) continue;

                        BEBehaviorMPSpurGear gear = gearBlock == null ? null : be.GetBehavior<BEBehaviorMPSpurGear>();
                        foreach (BlockFacing face in BlockFacing.ALLFACES)
                        {
                            if (!face.IsAdjacent(discFace)) continue;   // Coaxial discs are already covered above.

                            float radius = axle != null ? axle.GetHostedSpurRadius(face) : gearBlock.getGearRadius(gear, face);
                            if (radius > 0f && discsCross(discCell, discFace, pitchRadius, neighbour, face, radius)) return true;
                        }
                    }
                }
            }
            return false;
        }

        // Two discs on different axes meet along the line where their planes cross.
        static bool discsCross(BlockPos cell, BlockFacing face, float pitchRadius, BlockPos otherCell, BlockFacing otherFace, float otherPitchRadius)
        {
            int ownAxis = (int)face.Axis;
            int otherAxis = (int)otherFace.Axis;
            int sharedAxis = 3 - ownAxis - otherAxis;   // The axis lying in both disc planes.

            float ownRadius = pitchRadius * spurOuterRadiusScale - gearClearanceTolerance;
            float otherRadius = otherPitchRadius * spurOuterRadiusScale - gearClearanceTolerance;

            float toOwnPlane = Math.Abs(discCenter(otherCell, otherFace, ownAxis) - discCenter(cell, face, ownAxis));
            float toOtherPlane = Math.Abs(discCenter(cell, face, otherAxis) - discCenter(otherCell, otherFace, otherAxis));
            if (toOwnPlane >= otherRadius || toOtherPlane >= ownRadius) return false;

            // Neither disc reaches the crossing line with its full radius.
            float reach = chordHalfWidth(ownRadius, toOtherPlane) + chordHalfWidth(otherRadius, toOwnPlane);
            return Math.Abs(discCenter(cell, face, sharedAxis) - discCenter(otherCell, otherFace, sharedAxis)) < reach;
        }

        // A disc sits on the boundary between its cell and the cell it faces.
        static float discCenter(BlockPos cell, BlockFacing face, int axis)
        {
            return cell[axis] + 0.5f + face.Normali[axis] * 0.5f;
        }

        static float chordHalfWidth(float radius, float distanceFromCenter)
        {
            return GameMath.Sqrt(radius * radius - distanceFromCenter * distanceFromCenter);
        }

        static float getDiscRadius(IWorldAccessor world, BlockPos discCell, BlockFacing discFace)
        {
            IBlockAccessor blockAccessor = world.BlockAccessor;
            Block block = blockAccessor.GetBlock(discCell);
            if (block is not BlockSpurGear && block is not BlockAxle) return 0f;

            BlockEntity be = blockAccessor.GetBlockEntity(discCell);
            if (block is BlockSpurGear gear)
            {
                return gear.getGearRadius(be?.GetBehavior<BEBehaviorMPSpurGear>(), discFace);
            }

            BEBehaviorMPAxle axle = be?.GetBehavior<BEBehaviorMPAxle>();
            return axle?.HasSpurGear(discFace) == true ? axle.GetHostedSpurRadius(discFace) : 0f;
        }

        public static bool TryPlaceGearOnAxle(IWorldAccessor world, ItemStack gearStack, BlockPos axlePos, BlockFacing axleEndFace, ref string failureCode, bool placeHosted = false, BlockSelection blockSel = null)
        {
            if (axleEndFace == null || gearStack?.Block is not BlockSpurGear gearBlock) return false;

            IBlockAccessor blockAccessor = world.BlockAccessor;
            Block axleBlock = blockAccessor.GetBlock(axlePos);
            if (axleBlock is not BlockAxle axle)
            {
                failureCode = "requiresaxle";
                return false;
            }

            BlockFacing placementFace = axle.GetAxleEndFromSelection(axlePos, blockSel, axleEndFace) ?? axleEndFace;

            BlockPos gearPos = axlePos.AddCopy(placementFace);
            Block current = blockAccessor.GetBlock(gearPos);
            BlockFacing externalFace = placementFace.Opposite;
            if (!placeHosted && current is BlockSpurGear)
            {
                return tryAddExternalSpurToExisting(world, gearStack, gearPos, externalFace, ref failureCode);
            }

            // Ctrl-right-click mounts the gear on the axle.
            if (placeHosted)
            {
                return tryAddHostedSpurOnAxle(world, gearStack, axlePos, placementFace, ref failureCode);
            }

            if (!axle.IsOrientedTo(placementFace))
            {
                failureCode = "requiresaxle";
                return false;
            }

            if (!axle.HasCompatibleConnector(gearBlock))
            {
                failureCode = "incompatibleconnector";
                return false;
            }

            if (current.Replaceable < 6000)
            {
                failureCode ??= "notenoughspace";
                return false;
            }

            BlockSpurGear toPlaceBlock = world.GetBlock(gearBlock.CodeWithVariant("orientation", $"{placementFace.Opposite.Code[0]}")) as BlockSpurGear;
            if (toPlaceBlock == null)
            {
                failureCode = "requiresaxle";
                return false;
            }

            if (wouldGearClip(world, axlePos, placementFace, gearBlock.PitchRadius, false))
            {
                failureCode = "notenoughspace";
                return false;
            }

            blockAccessor.SetBlock(toPlaceBlock.BlockId, gearPos, gearStack);

            var axleMechBlock = axleBlock as IMechanicalPowerBlock;
            axleMechBlock?.DidConnectAt(world, axlePos, placementFace);

            connectPlacedGear(world, gearPos, toPlaceBlock, placementFace.Opposite);
            blockAccessor.GetBlockEntity(axlePos)?.GetBehavior<BEBehaviorMPAxle>()?.RefreshPowerConnections(placementFace);
            return true;
        }

        static void connectPlacedGear(IWorldAccessor world, BlockPos gearPos, BlockSpurGear gearBlock, BlockFacing entryFacing)
        {
            var selfBeh = gearBlock.GetBEBehavior<BEBehaviorMPBase>(gearPos);
            if (selfBeh == null) return;

            var exits = selfBeh.GetMechPowerExits(new MechPowerPath() { OutFacing = entryFacing });

            List<BlockFacing> possiblyNetworklessCandidates = [];
            BlockPos npos = new BlockPos(gearPos.dimension);
            foreach (var exit in exits)
            {
                npos.Set(gearPos).Add(exit.OutFacing);
                var neibBlock = world.BlockAccessor.GetBlock(npos) as IMechanicalPowerBlock;
                neibBlock?.DidConnectAt(world, npos, exit.OutFacing.Opposite);
                if (neibBlock != null && !selfBeh.tryConnect(exit.OutFacing))
                {
                    // This neighbour may not have a network until the gear connects elsewhere.
                    // Retry it once this gear has joined a network.
                    possiblyNetworklessCandidates.Add(exit.OutFacing);
                }
            }

            if (selfBeh.Network != null)
            {
                foreach (var face in possiblyNetworklessCandidates) selfBeh.tryConnect(face);
            }
        }


        public override void OnNeighbourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos)
        {
            BEBehaviorMPSpurGear gear = world.BlockAccessor.GetBlockEntity(pos)?.GetBehavior<BEBehaviorMPSpurGear>();

            // Each disc is tied to the arbor on its own face.
            if (gear != null)
            {
                foreach (BlockFacing face in BlockFacing.ALLFACES)
                {
                    if (!gear.HasExtraExternalSpurFace(face) || HasDiscSupport(world, pos, face)) continue;
                    gear.RemoveExtraExternalSpur(face, true);
                }
            }

            bool ownSupported = false;
            foreach (BlockFacing face in SpurFacings)
            {
                if (HasDiscSupport(world, pos, face))
                {
                    ownSupported = true;
                    break;
                }
            }

            if (!ownSupported)
            {
                // A surviving extra disc takes over when the primary arbor is removed.
                BlockFacing survivor = null;
                if (gear != null)
                {
                    foreach (BlockFacing face in BlockFacing.ALLFACES)
                    {
                        if (gear.HasExtraExternalSpurFace(face))
                        {
                            survivor = face;
                            break;
                        }
                    }
                }

                Block variant = survivor == null
                    ? null
                    : world.GetBlock((gear.GetExtraDiscBlock(survivor) ?? this).CodeWithVariant("orientation", $"{survivor.Code[0]}"));

                if (variant == null)
                {
                    world.BlockAccessor.BreakBlock(pos, null);
                    return;
                }

                List<MechanicalNetwork> networks = gear?.GetConnectedNetworks();
                if (world.Side == EnumAppSide.Server)
                {
                    world.SpawnItemEntity(OnPickBlock(world, pos), pos.ToVec3d().Add(0.5, 0.5, 0.5));
                }
                world.BlockAccessor.SetBlock(variant.BlockId, pos, gear.GetExtraExternalSpurDrop(survivor));

                BEBehaviorMPSpurGear replacement = world.BlockAccessor.GetBlockEntity(pos)?.GetBehavior<BEBehaviorMPSpurGear>();
                if (networks != null) gear.RebuildNetworks(networks);
                if (replacement?.Network == null) replacement?.RefreshPowerConnections(survivor);
                return;
            }

            base.OnNeighbourBlockChange(world, pos, neibpos);
        }

        public static bool HasDiscSupport(IWorldAccessor world, BlockPos pos, BlockFacing face)
        {
            if (face == null) return false;

            BlockPos supportPos = pos.AddCopy(face);
            if (world.BlockAccessor.GetChunkAtBlockPos(supportPos) == null) return true;

            IBlockAccessor blockAccessor = world.BlockAccessor;
            Block supportBlock = blockAccessor.GetBlock(supportPos);
            BEBehaviorMPBase support = blockAccessor.GetBlockEntity(supportPos)?.GetBehavior<BEBehaviorMPBase>();
            return supportBlock is BlockAxle axle && axle.IsOrientedTo(face.Opposite) && support?.disconnected != true;
        }


        public override void DidConnectAt(IWorldAccessor world, BlockPos pos, BlockFacing face) { }
    }
}
