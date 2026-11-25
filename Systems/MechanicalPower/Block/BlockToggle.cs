using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent.Mechanics
{

    public class BlockToggle : BlockMPBase
    {
        public bool IsOrientedTo(BlockFacing facing)
        {
            string dirs = LastCodePart();

            return dirs[0] == facing.Code[0] || (dirs.Length > 1 && dirs[1] == facing.Code[0]);
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

            foreach (BlockFacing face in BlockFacing.HORIZONTALS)
            {
                BlockPos pos = blockSel.Position.AddCopy(face);

                IMechanicalPowerBlock block = world.BlockAccessor.GetBlock(pos) as IMechanicalPowerBlock;
                if (block != null)
                {
                    if (block.HasMechPowerConnectorAt(world, pos, face.Opposite, this))
                    {
                        AssetLocation loc = new AssetLocation(FirstCodePart() + "-" + face.Opposite.Code[0] + face.Code[0]);
                        Block toPlaceBlock = world.GetBlock(loc);
                        if (toPlaceBlock == null)
                        {
                            loc = new AssetLocation(FirstCodePart() + "-" + face.Code[0] + face.Opposite.Code[0]);
                            toPlaceBlock = world.GetBlock(loc);
                        }

                        if (toPlaceBlock.DoPlaceBlock(world, byPlayer, blockSel, itemstack))
                        {
                            block.DidConnectAt(world, pos, face.Opposite);
                            WasPlaced(world, blockSel.Position, face);
                            return true;
                        }
                    }
                }
            }


            if (base.TryPlaceBlock(world, byPlayer, itemstack, blockSel, ref failureCode))
            {
                WasPlaced(world, blockSel.Position, null);
                return true;
            }
            return false;
        }

        public override void DidConnectAt(IWorldAccessor world, BlockPos pos, BlockFacing face)
        {
            
        }
    }
}
