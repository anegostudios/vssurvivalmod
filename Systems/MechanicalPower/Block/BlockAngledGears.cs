using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent.Mechanics
{
    public class BlockAngledGears : BlockMPBase
    {
        public string Orientation;
        bool doubled;
        BlockFacing[] facings;
        string supportOrientation;
        BlockFacing[] supportFacings;

        public override void OnLoaded(ICoreAPI api)
        {
            Orientation = Variant["orientation"];
            doubled = Orientation.Length == 2 && Orientation[0] == Orientation[1];

            facings = [.. Orientation.Select(BlockFacing.FromFirstLetter)];
            if (doubled) facings[1] = facings[1].Opposite;

            supportOrientation = doubled ? $"{Orientation[0]}" : Orientation;
            supportFacings = doubled ? [facings[0]] : facings;

            AngledGearProfile = Attributes?["angledGearProfile"].AsString();
            AngledGearPartnerProfile = Attributes?["angledGearPartnerProfile"].AsString();
            JsonObject meshPhase = Attributes?["angledGearMeshPhaseDeg"];
            MeshPhaseDeg = meshPhase?.Exists == true ? meshPhase.AsFloat() : null;
            HasShaft = Attributes?["angledGearHasShaft"].AsBool() ?? false;

            base.OnLoaded(api);
        }

        public BlockFacing[] Facings => facings;

        public string AngledGearProfile { get; private set; }
        public string AngledGearPartnerProfile { get; private set; }
        public float? MeshPhaseDeg { get; private set; }
        public bool HasShaft { get; private set; }

        // Perpendicular gears in one block mesh only when each names the other's profile.
        public bool CanMeshWith(BlockAngledGears other)
        {
            return AngledGearProfile == other.AngledGearPartnerProfile && AngledGearPartnerProfile == other.AngledGearProfile;
        }

        Cuboidf[] getCageGearBoxes(IBlockAccessor blockAccessor, BlockPos pos, bool collision)
        {
            Cuboidf[] boxes = collision ? base.GetCollisionBoxes(blockAccessor, pos) : base.GetSelectionBoxes(blockAccessor, pos);
            if (!doubled || boxes?.Length == 0) return boxes;

            BlockFacing gearFace = BlockFacing.FromFirstLetter(Orientation[0]);
            Block principalBlock = blockAccessor.GetBlock(pos.AddCopy(gearFace.Opposite));
            Cuboidf[] rimBoxes = BlockMPMultiblockGear.GetOrientedRimBoxes(principalBlock, collision);
            if (rimBoxes == null) return boxes;

            return [boxes[0], rimBoxes[0]];
        }

        public override Cuboidf[] GetSelectionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            return getCageGearBoxes(blockAccessor, pos, false);
        }

        public override Cuboidf[] GetCollisionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            return getCageGearBoxes(blockAccessor, pos, true);
        }

        public bool IsDeadEnd()
        {
            return Orientation.Length == 1;
        }

        public bool IsOrientedTo(BlockFacing facing)
        {
            string dirs = Orientation;
            if (dirs[0] == facing.Code[0]) return true;  //all configurations of angled gear have a power connection on the first side
            if (dirs.Length == 1) return false;  //dead end
            if (dirs[0] == dirs[1]) return dirs[0] == facing.Opposite.Code[0];   //small gears on large gear can accept power from large gear hub
            return dirs[1] == facing.Code[0];  //angled gears - secondary direction
        }

        public override bool HasMechPowerConnectorAt(IWorldAccessor world, BlockPos pos, BlockFacing face, BlockMPBase forBlock)
        {
            // Angled gears only engage perpendicular gears inside a shared block.
            if (forBlock is BlockAngledGears || forBlock is BlockAngledGearMulti) return false;
            return HasCompatibleConnector(forBlock) && IsOrientedTo(face);
        }

        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            return GetBEBehavior<BEBehaviorMPBase>(pos)?.GetPlacedStack() ?? new ItemStack(world.GetBlock(CodeWithVariant("orientation", "s")));
        }


        public Block GetGearBlock(IWorldAccessor world, bool cageGear, BlockFacing facing, BlockFacing adjFacing = null)
        {
            if (adjFacing == null)
            {
                char orient = facing.Code[0];
                string orientation = cageGear ? new string(orient, 2) : $"{orient}";
                return world.GetBlock(CodeWithVariant("orientation", orientation));
            }

            AssetLocation loc = CodeWithVariant("orientation", $"{adjFacing.Code[0]}{facing.Code[0]}");
            Block toPlaceBlock = world.GetBlock(loc);

            if (toPlaceBlock == null)
            {
                loc = CodeWithVariant("orientation", $"{facing.Code[0]}{adjFacing.Code[0]}");
                toPlaceBlock = world.GetBlock(loc);
            }

            return toPlaceBlock;
        }

        public BlockAngledGearMulti GetMultiGearBlock(IWorldAccessor world)
        {
            string code = Attributes["multiGearBlockCode"].AsString();
            return code == null ? null : world.GetBlock(AssetLocation.Create(code, Code.Domain)) as BlockAngledGearMulti;
        }

        public override void DidConnectAt(IWorldAccessor world, BlockPos pos, BlockFacing face)
        {
            // Each gear is placed individually, so connections do not upgrade this block.
        }

        public bool CanPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref string failureCode, Block blockExisting)
        {
            if (blockExisting is BlockMPMultiblockGear testMultiblock && !testMultiblock.IsReplacableByGear(world, blockSel.Position))
            {
                failureCode = "notreplaceable";
                return false;
            }
            return base.CanPlaceBlock(world, byPlayer, blockSel, ref failureCode);
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (TryHandleGearInHand(world, byPlayer, blockSel)) return true;
            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }

        public static bool TryHandleGearInHand(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            var hotbarSlot = byPlayer?.InventoryManager?.ActiveHotbarSlot;
            if (hotbarSlot?.Itemstack?.Block is not BlockAngledGears) return false;

            // Prevent placement from falling back to another axle.
            string failureCode = "";
            if (BlockAngledGearMulti.TryAddGear(world, blockSel.Position, blockSel.Face, hotbarSlot.Itemstack, ref failureCode))
            {
                if (byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative)
                {
                    hotbarSlot.TakeOut(1);
                    hotbarSlot.MarkDirty();
                }
            }
            return true;
        }

        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
        {
            IBlockAccessor blockAccessor = world.BlockAccessor;
            if (blockSel.Face != null)
            {
                BlockPos clickedPos = blockSel.Position.AddCopy(blockSel.Face.Opposite);
                if (blockAccessor.GetBlockEntity(clickedPos)?.GetBehavior<BEBehaviorMPAxle>() != null)
                {
                    return BlockAngledGearMulti.TryPlaceGearOnAxle(world, byPlayer, itemstack, clickedPos, blockSel.Face, ref failureCode);
                }
            }

            Block blockExisting = blockAccessor.GetBlock(blockSel.Position);

            // Redirect placement on an existing gear to the shared block.
            if (blockExisting is BlockAngledGears || blockExisting is BlockAngledGearMulti)
            {
                if (blockSel.Face == null)
                {
                    failureCode = "requiresaxle";
                    return false;
                }
                return BlockAngledGearMulti.TryAddGear(world, blockSel.Position, blockSel.Face.Opposite, itemstack, ref failureCode);
            }

            if (!CanPlaceBlock(world, byPlayer, blockSel, ref failureCode, blockExisting))
            {
                return false;
            }

            BlockFacing connectionFace = null;
            BlockMPMultiblockGear largeGearEdge = blockExisting as BlockMPMultiblockGear;
            bool validLargeGear = false;
            EnumAxis largeGearAxleAxis = EnumAxis.Y;
            if (largeGearEdge != null)
            {
                BEMPMultiblock be = blockAccessor.GetBlockEntity(blockSel.Position) as BEMPMultiblock;
                if (be?.Principal != null && blockAccessor.GetBlock(be.Principal) is BlockLargeGear3m largeGear)
                {
                    validLargeGear = true;
                    largeGearAxleAxis = BlockLargeGear3m.GetGearAxis(largeGear);
                }
            }

            // Prefer the axle the player aimed at over another neighbour.
            BlockFacing preferredFace = blockSel.Face?.Opposite;
            BlockPos connectionPos = blockSel.Position.Copy();
            if (preferredFace != null && (!validLargeGear || preferredFace.Axis != largeGearAxleAxis))
            {
                connectionPos.Add(preferredFace);
                IMechanicalPowerBlock preferredBlock = blockAccessor.GetBlock(connectionPos) as IMechanicalPowerBlock;
                if (preferredBlock?.HasMechPowerConnectorAt(world, connectionPos, preferredFace.Opposite, this) == true)
                {
                    connectionFace = preferredFace;
                }
            }

            if (connectionFace == null)
            {
                connectionPos.Set(blockSel.Position);
                foreach (BlockFacing face in BlockFacing.ALLFACES)
                {
                    face.IterateThruFacingOffsets(connectionPos);
                    if (face == preferredFace) continue;
                    if (validLargeGear && face.Axis == largeGearAxleAxis) continue;

                    IMechanicalPowerBlock block = blockAccessor.GetBlock(connectionPos) as IMechanicalPowerBlock;
                    if (block != null && block.HasMechPowerConnectorAt(world, connectionPos, face.Opposite, this))
                    {
                        connectionFace = face;
                        break;
                    }
                }
            }

            if (connectionFace != null)
            {
                BlockEntity be = blockAccessor.GetBlockEntity(connectionPos);
                IMechanicalPowerBlock neighbour = be?.Block as IMechanicalPowerBlock;

                if (validLargeGear)
                {
                    Block toPlaceBlock = GetGearBlock(world, true, connectionFace);
                    if (toPlaceBlock == null)
                    {
                        failureCode = "notreplaceable";
                        return false;
                    }

                    BlockEntity largeGearBE = largeGearEdge.GearPlaced(world, blockSel.Position);
                    blockAccessor.SetBlock(toPlaceBlock.BlockId, blockSel.Position, itemstack);

                    BEBehaviorMPAngledGears beAngledGear = blockAccessor.GetBlockEntity(blockSel.Position)?.GetBehavior<BEBehaviorMPAngledGears>();
                    if (beAngledGear == null) return true; // Matches other blocks above the build limit.
                    if (largeGearBE?.GetBehavior<BEBehaviorMPBase>() is BEBehaviorMPLargeGear3m largeGear)
                    {
                        beAngledGear.AddToLargeGearNetwork(largeGear, connectionFace);
                    }

                    neighbour?.DidConnectAt(world, connectionPos, connectionFace.Opposite);
                    beAngledGear.newlyPlaced = true;
                    beAngledGear.tryConnect(connectionFace);
                    beAngledGear.newlyPlaced = false;

                    return true;
                }

                BlockAngledGearMulti multiGearBlock = GetMultiGearBlock(world);
                if (multiGearBlock == null)
                {
                    failureCode = "requiresaxle";
                    return false;
                }

                blockAccessor.SetBlock(multiGearBlock.BlockId, blockSel.Position);

                BEBehaviorMPAngledGears multiGear = blockAccessor.GetBlockEntity(blockSel.Position)?.GetBehavior<BEBehaviorMPAngledGears>();
                if (multiGear == null) return true;
                multiGear.TryAddGear(connectionFace, itemstack);
                BlockAngledGearMulti.RefreshPowerConnections(world, blockSel.Position);

                neighbour?.DidConnectAt(world, connectionPos, connectionFace.Opposite);

                return true;
            }

            failureCode = "requiresaxle";

            return false;
        }

        public override void WasPlaced(IWorldAccessor world, BlockPos ownPos, BlockFacing connectedOnFacing)
        {
            if (connectedOnFacing == null) return;
            BEBehaviorMPAngledGears beMechBase = world.BlockAccessor.GetBlockEntity(ownPos)?.GetBehavior<BEBehaviorMPAngledGears>();
            beMechBase?.tryConnect(connectedOnFacing);
        }

        public override void OnNeighbourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos)
        {
            string orients = supportOrientation;
            List<BlockFacing> lostFacings = [];
            BEBehaviorMPBase ownBehavior = world.BlockAccessor.GetBlockEntity(pos)?.GetBehavior<BEBehaviorMPBase>();

            foreach (BlockFacing facing in supportFacings)
            {
                BlockPos npos = pos.AddCopy(facing);
                if (world.BlockAccessor.GetChunkAtBlockPos(npos) == null) continue;

                IMechanicalPowerBlock nblock = world.BlockAccessor.GetBlock(npos) as IMechanicalPowerBlock;

                if (nblock == null || !nblock.HasMechPowerConnectorAt(world, npos, facing.Opposite, this) || ownBehavior?.disconnected == true)
                {
                    lostFacings.Add(facing);
                }
            }

            if (lostFacings.Count == orients.Length)
            {
                world.BlockAccessor.BreakBlock(pos, null);
                return;
            }

            if (lostFacings.Count > 0)
            {
                if (world.Side == EnumAppSide.Server)
                {
                    world.SpawnItemEntity(OnPickBlock(world, pos), pos.ToVec3d().Add(0.5, 0.5, 0.5));
                }

                orients = orients.Replace($"{lostFacings[0].Code[0]}", "");
                Block toPlaceBlock = world.GetBlock(CodeWithVariant("orientation", orients));
                (toPlaceBlock as BlockMPBase).ExchangeBlockAt(world, pos);
                BlockEntity be = world.BlockAccessor.GetBlockEntity(pos);
                BEBehaviorMPAngledGears bemp = be.GetBehavior<BEBehaviorMPAngledGears>();
                BlockFacing remainingFace = BlockFacing.FromFirstLetter(orients[0]);
                BlockPos remainingPos = pos.AddCopy(remainingFace);
                BlockEntity beNeib = world.BlockAccessor.GetBlockEntity(remainingPos);
                IMechanicalPowerBlock neighbour = beNeib?.Block as IMechanicalPowerBlock;

                neighbour?.DidConnectAt(world, remainingPos, remainingFace.Opposite);
                if (bemp?.Network != null) bemp.RebuildNetworks(bemp.GetConnectedNetworks());
                if (bemp?.Network == null) WasPlaced(world, pos, remainingFace);
            }

        }

        public override void OnBlockRemoved(IWorldAccessor world, BlockPos pos)
        {
            bool preventDefault = false;

            foreach (BlockBehavior behavior in BlockBehaviors)
            {
                EnumHandling handled = EnumHandling.PassThrough;

                behavior.OnBlockRemoved(world, pos, ref handled);
                if (handled == EnumHandling.PreventSubsequent) return;
                if (handled == EnumHandling.PreventDefault) preventDefault = true;
            }

            if (preventDefault) return;

            world.BlockAccessor.RemoveBlockEntity(pos);

            // Restore the large gear multiblock when a cage gear is broken.
            if (doubled)
            {
                BlockMPMultiblockGear.OnGearDestroyed(world, pos, Orientation[0]);
            }
        }

        public void ToPegGear(IWorldAccessor world, BlockPos pos)
        {
            if (doubled)
            {
                Block toPlaceBlock = world.GetBlock(CodeWithVariant("orientation", $"{Orientation[0]}"));
                ((BlockMPBase)toPlaceBlock).ExchangeBlockAt(world, pos);
                BEBehaviorMPAngledGears beg = world.BlockAccessor.GetBlockEntity(pos)?.GetBehavior<BEBehaviorMPAngledGears>();
                beg?.ClearLargeGear();
                if (beg?.Network != null) beg.RebuildNetworks(beg.GetConnectedNetworks());
                if (beg?.Network == null) beg?.tryConnect(BlockFacing.FromFirstLetter(Orientation[0]));
            }
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
            char[] transformed = Variant["orientation"].ToCharArray();
            for (int i = 0; i < transformed.Length; i++)
            {
                BlockFacing facing = BlockFacing.FromFirstLetter(transformed[i]);
                if (facing.Axis == flipAxis) facing = facing.Opposite;
                transformed[i] = facing.GetHorizontalRotated(angle).Code[0];
            }

            string orientation = new(transformed);
            AssetLocation transformedCode = CodeWithVariant("orientation", orientation);
            if (orientation.Length < 2 || api.World.GetBlock(transformedCode) != null) return transformedCode;

            return CodeWithVariant("orientation", $"{orientation[1]}{orientation[0]}");
        }
    }
}
