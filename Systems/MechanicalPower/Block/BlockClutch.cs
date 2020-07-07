using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent.Mechanics
{
    public class BlockClutch : Block
    {
        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
        {
            if (!CanPlaceBlock(world, byPlayer, blockSel, ref failureCode)) return false;

            BlockFacing frontFacing = Block.SuggestedHVOrientation(byPlayer, blockSel)[0];
            BlockFacing bestFacing = frontFacing;
            if (!(world.BlockAccessor.GetBlock(blockSel.Position.AddCopy(frontFacing)) is BlockTransmission))
            {
                BlockFacing leftFacing = BlockFacing.HORIZONTALS_ANGLEORDER[GameMath.Mod(frontFacing.HorizontalAngleIndex - 1, 4)];
                if (world.BlockAccessor.GetBlock(blockSel.Position.AddCopy(leftFacing)) is BlockTransmission)
                {
                    bestFacing = leftFacing;
                }
                else
                {
                    BlockFacing rightFacing = leftFacing.GetOpposite();
                    if (world.BlockAccessor.GetBlock(blockSel.Position.AddCopy(rightFacing)) is BlockTransmission)
                    {
                        bestFacing = rightFacing;
                    }
                    else
                    {
                        BlockFacing backFacing = frontFacing.GetOpposite();
                        if (world.BlockAccessor.GetBlock(blockSel.Position.AddCopy(backFacing)) is BlockTransmission)
                        {
                            bestFacing = backFacing;
                        }
                    }
                }
            }

            Block orientedBlock = world.BlockAccessor.GetBlock(CodeWithParts(bestFacing.Code));
            return orientedBlock.DoPlaceBlock(world, byPlayer, blockSel, itemstack);
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            BEClutch be = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BEClutch;
            if (be != null) return be.OnInteract(byPlayer);

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }
    }
}
