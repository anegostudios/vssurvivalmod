using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent.Mechanics
{

    public class BlockArchimedesScrew : BlockMPBase, IBlockItemFlow
    {
        public bool IsOrientedTo(BlockFacing facing)
        {
            return facing.Axis == EnumAxis.Y;
        }


        public bool HasItemFlowConnectorAt(BlockFacing facing)
        {
            if (Variant["type"] == "ported-north") return facing == BlockFacing.NORTH;
            if (Variant["type"] == "ported-east") return facing == BlockFacing.EAST;
            if (Variant["type"] == "ported-south") return facing == BlockFacing.SOUTH;
            if (Variant["type"] == "ported-west") return facing == BlockFacing.WEST;
            return false;
        }


        public override bool HasMechPowerConnectorAt(IWorldAccessor world, BlockPos pos, BlockFacing face, BlockMPBase forBlock)
        {
            return IsOrientedTo(face);
        }

        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
        {
            if (!CanPlaceBlock(world, byPlayer, blockSel, ref failureCode))
            {
                return false;
            }

            BlockArchimedesScrew blockToPlace = this;
            BlockFacing[] facings = SuggestedHVOrientation(byPlayer, blockSel);
            if (Variant["type"].StartsWithOrdinal("ported"))
            {
                blockToPlace = api.World.GetBlock(CodeWithVariant("type", "ported-" + facings[0].Opposite.Code)) as BlockArchimedesScrew;
            }



            foreach (BlockFacing face in BlockFacing.VERTICALS)
            {
                BlockPos pos = blockSel.Position.AddCopy(face);

                IMechanicalPowerBlock block = world.BlockAccessor.GetBlock(pos) as IMechanicalPowerBlock;
                if (block != null && block.HasMechPowerConnectorAt(world, pos, face.Opposite, this))
                {
                    if (blockToPlace.DoPlaceBlock(world, byPlayer, blockSel, itemstack))
                    {
                        block.DidConnectAt(world, pos, face.Opposite);
                        WasPlaced(world, blockSel.Position, face);
                        return true;
                    }
                }
            }

            if (blockToPlace.CanPlaceBlock(world, byPlayer, blockSel, ref failureCode) && blockToPlace.DoPlaceBlock(world, byPlayer, blockSel, itemstack))
            {
                blockToPlace.WasPlaced(world, blockSel.Position, null);
                return true;
            }

            return false;
        }

        public override void DidConnectAt(IWorldAccessor world, BlockPos pos, BlockFacing face)
        {
            
        }

    }
}
