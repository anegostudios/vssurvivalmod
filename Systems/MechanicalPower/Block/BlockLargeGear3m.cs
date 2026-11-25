using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent.Mechanics
{
    public class BlockLargeGear3m : BlockMPBase
    {
        static readonly BlockFacing[][] axleFacings = [getFacesOnAxis(EnumAxis.X), getFacesOnAxis(EnumAxis.Y), getFacesOnAxis(EnumAxis.Z)];
        static readonly BlockFacing[][] ringFacings = [getFacesOffAxis(EnumAxis.X), getFacesOffAxis(EnumAxis.Y), getFacesOffAxis(EnumAxis.Z)];

        // The axle ends, with the end that counts as inverted last, see BEBehaviorMPLargeGear3m.IsRimInverted().
        // That is south, east and down, so only the X pair comes out of ALLFACES the wrong way round.
        static BlockFacing[] getFacesOnAxis(EnumAxis axis)
        {
            var faces = BlockFacing.ALLFACES.Where(face => face.Axis == axis);
            return [..(axis == EnumAxis.X ? faces.Reverse() : faces)];
        }

        // The ring is every face that is not on the gear axis.
        static BlockFacing[] getFacesOffAxis(EnumAxis axis)
        {
            return [..BlockFacing.ALLFACES.Where(face => face.Axis != axis)];
        }

        // The orientation variant is the direction of the axle stub.
        public static BlockFacing GetStubFacing(Block block)
        {
            if (block == null) return BlockFacing.UP;
            return BlockFacing.FromFirstLetter(block.Variant["orientation"][0]) ?? BlockFacing.UP;
        }

        // The axle axis is also the normal of the gear plane.
        public static EnumAxis GetGearAxis(Block block)
        {
            return GetStubFacing(block).Axis;
        }

        public static BlockFacing[] GetAxleFacings(Block block) => axleFacings[(int)GetGearAxis(block)];

        public static BlockFacing[] GetAxleFacings(EnumAxis axis) => axleFacings[(int)axis];

        public static BlockFacing[] GetRingFacings(Block block) => ringFacings[(int)GetGearAxis(block)];

        public static BlockFacing[] GetRingFacings(EnumAxis axis) => ringFacings[(int)axis];

        // A ring step of (da, db) laid onto the plane of the gear.
        static FastVec3i getRingOffset(EnumAxis axis, int da, int db)
        {
            if (axis == EnumAxis.Z) return new FastVec3i(da, db, 0);   // XY plane
            if (axis == EnumAxis.X) return new FastVec3i(0, db, da);   // ZY plane
            return new FastVec3i(da, 0, db);   // XZ plane
        }

        static BlockFacing getRingFacing(EnumAxis axis, int da, int db)
        {
            if (axis == EnumAxis.Z)
            {
                if (da != 0) return da > 0 ? BlockFacing.EAST : BlockFacing.WEST;
                return db > 0 ? BlockFacing.UP : BlockFacing.DOWN;
            }
            if (axis == EnumAxis.X)
            {
                if (da != 0) return da > 0 ? BlockFacing.SOUTH : BlockFacing.NORTH;
                return db > 0 ? BlockFacing.UP : BlockFacing.DOWN;
            }
            if (da != 0) return da > 0 ? BlockFacing.EAST : BlockFacing.WEST;
            return db > 0 ? BlockFacing.SOUTH : BlockFacing.NORTH;
        }

        static BlockMPBase getCageGearReplacementAt(IWorldAccessor world, BlockPos pos, BlockFacing gearFace)
        {
            IBlockAccessor blockAccessor = world.BlockAccessor;
            Block block = blockAccessor.GetBlock(pos);
            BlockAngledGears gear = block as BlockAngledGears;
            if (block is BlockAngledGearMulti)
            {
                gear = blockAccessor.GetBlockEntity(pos)?.GetBehavior<BEBehaviorMPAngledGears>()?.GetGearBlock(gearFace);
            }
            return gear?.GetGearBlock(world, true, gearFace) as BlockMPBase;
        }

        public override void DidConnectAt(IWorldAccessor world, BlockPos pos, BlockFacing face)
        {
            //TODO - change model, start with half axle but extend axle upwards to full block height if connection above
        }

        public override bool HasMechPowerConnectorAt(IWorldAccessor world, BlockPos pos, BlockFacing face, BlockMPBase forBlock)
        {
            var axles = GetAxleFacings(this);
            if (face == axles[0] || face == axles[1]) return true;
            return (world.BlockAccessor.GetBlockEntity(pos) is BELargeGear3m beg) && beg.HasGearAt(world.Api, pos.AddCopy(face));
        }

        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
        {
            // The stub points out from the face the player clicked against.
            BlockFacing stub = blockSel.Face ?? BlockFacing.UP;
            EnumAxis axis = stub.Axis;

            Block blockToPlace = world.GetBlock(CodeWithVariant("orientation", $"{stub.Code[0]}"));
            if (blockToPlace == null)
            {
                failureCode = "notreplaceable";
                return false;
            }

            if (!canPlaceBlock(world, byPlayer, blockSel, ref failureCode, axis, out SmallBoolArray smallGearFaces))
            {
                return false;
            }

            BlockPos ringPos = new BlockPos(blockSel.Position.dimension);
            BlockMPBase[] cageGears = new BlockMPBase[BlockFacing.ALLFACES.Length];
            foreach (BlockFacing gearFace in GetRingFacings(axis))
            {
                if (!smallGearFaces[gearFace.Index]) continue;

                cageGears[gearFace.Index] = getCageGearReplacementAt(world, ringPos.Set(blockSel.Position).Add(gearFace), gearFace);
                if (cageGears[gearFace.Index] == null)
                {
                    failureCode = "notreplaceable";
                    return false;
                }
            }

            bool ok = blockToPlace.DoPlaceBlock(world, byPlayer, blockSel, itemstack);
            if (ok)
            {
                BlockEntity beOwn = world.BlockAccessor.GetBlockEntity(blockSel.Position);
                List<BlockFacing> connections = [];
                BlockPos connectionPos = new BlockPos(blockSel.Position.dimension);

                foreach (BlockFacing gearFace in GetRingFacings(axis))
                {
                    if (!smallGearFaces[gearFace.Index]) continue;

                    connectionPos.Set(blockSel.Position).Add(gearFace);
                    BlockMPBase toPlaceBlock = cageGears[gearFace.Index];

                    BEBehaviorMPAngledGears gear = world.BlockAccessor.GetBlockEntity(connectionPos)?.GetBehavior<BEBehaviorMPAngledGears>();
                    gear?.LeaveNetwork();
                    ItemStack gearStack = gear?.Block is BlockAngledGearMulti ? gear.RemoveGear(gearFace) : null;
                    toPlaceBlock.ExchangeBlockAt(world, connectionPos);

                    if (gearStack != null) gear.OnBlockPlaced(gearStack);

                    toPlaceBlock.DidConnectAt(world, connectionPos, gearFace.Opposite);
                    if (beOwn is BELargeGear3m largeGearBe)
                    {
                        largeGearBe.AngledGearNotAlreadyAdded(connectionPos);
                    }
                    connections.Add(gearFace);
                }

                placeFakeBlocks(world, blockSel.Position, smallGearFaces, axis);

                // Connect to an axle on either end.
                foreach (BlockFacing axleFace in GetAxleFacings(axis))
                {
                    connectionPos.Set(blockSel.Position).Add(axleFace);
                    IMechanicalPowerBlock axleBlock = world.BlockAccessor.GetBlock(connectionPos) as IMechanicalPowerBlock;
                    if (axleBlock != null && axleBlock.HasMechPowerConnectorAt(world, connectionPos, axleFace.Opposite, blockToPlace as BlockMPBase))
                    {
                        axleBlock.DidConnectAt(world, connectionPos, axleFace.Opposite);
                        connections.Add(axleFace);
                    }
                }

                BEBehaviorMPBase beMechBase = beOwn?.GetBehavior<BEBehaviorMPBase>();
                foreach (BlockFacing face in connections)
                {
                    beMechBase?.WasPlaced(face);
                }

                foreach (BlockFacing gearFace in GetRingFacings(axis))
                {
                    if (!smallGearFaces[gearFace.Index]) continue;

                    connectionPos.Set(blockSel.Position).Add(gearFace);
                    BlockMPBase cageGear = cageGears[gearFace.Index];
                    if (cageGear.GetNetwork(world, connectionPos) == null)
                    {
                        cageGear.tryConnect(world, byPlayer, connectionPos, gearFace);
                    }
                }
            }
            return ok;
        }

        void placeFakeBlocks(IWorldAccessor world, BlockPos pos, SmallBoolArray smallGearFaces, EnumAxis axis)
        {
            Block toPlaceBlock = world.GetBlock("mpmultiblockwood");
            BlockPos tmpPos = new BlockPos(pos.dimension);

            for (int da = -1; da <= 1; da++)
            {
                for (int db = -1; db <= 1; db++)
                {
                    if (da == 0 && db == 0) continue;
                    BlockFacing ringFacing = (da != 0 && db == 0) || (da == 0 && db != 0)
                        ? getRingFacing(axis, da, db)
                        : null;
                    if (ringFacing != null && smallGearFaces[ringFacing.Index]) continue;

                    tmpPos.Set(pos).Add(getRingOffset(axis, da, db));
                    world.BlockAccessor.SetBlock(toPlaceBlock.BlockId, tmpPos);
                    BEMPMultiblock be = world.BlockAccessor.GetBlockEntity(tmpPos) as BEMPMultiblock;
                    if (be != null) be.Principal = pos;
                }
            }
        }

        bool canPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref string failureCode, EnumAxis axis, out SmallBoolArray smallGearFaces)
        {
            smallGearFaces = new SmallBoolArray();
            if (!base.CanPlaceBlock(world, byPlayer, blockSel, ref failureCode)) return false;
            BlockPos pos = blockSel.Position;

            BlockPos tmpPos = new BlockPos(pos.dimension);
            BlockSelection bs = blockSel.Clone();

            for (int da = -1; da <= 1; da++)
            {
                for (int db = -1; db <= 1; db++)
                {
                    if (da == 0 && db == 0) continue;
                    tmpPos.Set(pos).Add(getRingOffset(axis, da, db));
                    BlockFacing ringFacing = (da != 0 && db == 0) || (da == 0 && db != 0)
                        ? getRingFacing(axis, da, db)
                        : null;

                    if (ringFacing != null && hasConvertibleSmallGear(world, tmpPos, ringFacing))
                    {
                        smallGearFaces[ringFacing.Index] = true;
                        continue;
                    }
                    bs.Position = tmpPos;
                    if (!base.CanPlaceBlock(world, byPlayer, bs, ref failureCode)) return false;
                }
            }

            return true;
        }

        static bool hasConvertibleSmallGear(IWorldAccessor world, BlockPos pos, BlockFacing facing)
        {
            IBlockAccessor blockAccessor = world.BlockAccessor;
            Block block = blockAccessor.GetBlock(pos);
            if (block is BlockAngledGears gear)
            {
                return gear.Orientation?.Length == 1 && gear.IsOrientedTo(facing);
            }

            if (block is BlockAngledGearMulti)
            {
                BEBehaviorMPAngledGears beh = blockAccessor.GetBlockEntity(pos)?.GetBehavior<BEBehaviorMPAngledGears>();
                return beh?.GearCount == 1 && beh.GetGearBlock(facing) != null;
            }

            return false;
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (blockSel.SelectionBoxIndex == 0)
            {
                blockSel.Face = GetStubFacing(this);   // Point along the axle stub.
            }
            return base.OnBlockInteractStart(world, byPlayer, blockSel);
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
            BlockFacing facing = GetStubFacing(this);
            if (facing.Axis == flipAxis) facing = facing.Opposite;
            return CodeWithVariant("orientation", $"{facing.GetHorizontalRotated(angle).Code[0]}");
        }

        public override void OnBlockRemoved(IWorldAccessor world, BlockPos pos)
        {
            base.OnBlockRemoved(world, pos);
            EnumAxis axis = GetGearAxis(this);
            BlockPos tmpPos = new BlockPos(pos.dimension);

            for (int da = -1; da <= 1; da++)
            {
                for (int db = -1; db <= 1; db++)
                {
                    if (da == 0 && db == 0) continue;
                    tmpPos.Set(pos).Add(getRingOffset(axis, da, db));

                    // Destroy fake blocks and restore cage gears to peg gears.
                    BEMPMultiblock be = world.BlockAccessor.GetBlockEntity(tmpPos) as BEMPMultiblock;
                    if (be != null && pos.Equals(be.Principal))
                    {
                        be.Principal = null;  // Allow the fake block to break without recursing.
                        world.BlockAccessor.SetBlock(0, tmpPos);
                    }
                    else
                    {
                        BlockAngledGears smallGear = world.BlockAccessor.GetBlock(tmpPos) as BlockAngledGears;
                        if (smallGear != null) smallGear.ToPegGear(world, tmpPos);
                    }
                }
            }
        }
    }
}
