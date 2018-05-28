using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockBehaviorNWOrientable : BlockBehavior
    {
        public BlockBehaviorNWOrientable(Block block) : base(block)
        {

        }

        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref EnumHandling handled)
        {
            handled = EnumHandling.PreventDefault;
            BlockFacing[] horVer = Block.SuggestedHVOrientation(byPlayer, blockSel);
            string code = "ns";
            if (horVer[0].Index == 1 || horVer[0].Index == 3) code = "we";
            Block orientedBlock = world.BlockAccessor.GetBlock(block.CodeWithParts(code));

            if (orientedBlock.IsSuitablePosition(world, blockSel.Position))
            {
                orientedBlock.DoPlaceBlock(world, blockSel.Position, blockSel.Face, itemstack);
                return true;
            }

            return false;
        }


        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier, ref EnumHandling handled)
        {
            handled = EnumHandling.PreventDefault;
            return new ItemStack[] { new ItemStack(world.BlockAccessor.GetBlock(block.CodeWithParts("ns"))) };
        }

        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos, ref EnumHandling handled)
        {
            handled = EnumHandling.PreventDefault;
            return new ItemStack(world.BlockAccessor.GetBlock(block.CodeWithParts("ns")));
        }
        
        

        public override AssetLocation GetRotatedBlockCode(int angle, ref EnumHandling handled)
        {
            handled = EnumHandling.PreventDefault;

            string[] angles = { "ns", "we" };
            int index = angle / 90;
            if (block.LastCodePart() == "we") index++;
            return block.CodeWithParts(angles[index % 2]);
        }


    }
}
