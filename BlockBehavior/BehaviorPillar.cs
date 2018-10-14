using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockBehaviorPillar : BlockBehavior
    {
        public BlockBehaviorPillar(Block block) : base(block)
        {
        }

        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref EnumHandling handled)
        {
            handled = EnumHandling.PreventDefault;
            //BlockFacing[] horVer = Block.SuggestedHVOrientation(byPlayer, blockSel);

            string rotation = "ud";
            //if (horVer[1] == null)
            {
                switch (blockSel.Face.Axis/* horVer[0].Axis*/)
                {
                    case EnumAxis.X: rotation = "we"; break;
                    case EnumAxis.Z: rotation = "ns"; break;
                }
            }

            Block orientedBlock = world.BlockAccessor.GetBlock(block.CodeWithParts(rotation));

            if (orientedBlock.IsSuitablePosition(world, blockSel.Position))
            {
                orientedBlock.DoPlaceBlock(world, blockSel.Position, blockSel.Face, itemstack);
                return true;
            }

            return false;
        }

        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos, ref EnumHandling handling)
        {
            handling = EnumHandling.PreventDefault;
            return new ItemStack(world.GetBlock(block.CodeWithParts("ud")));
        }

        public override AssetLocation GetRotatedBlockCode(int angle, ref EnumHandling handled)
        {
            handled = EnumHandling.PreventDefault;

            if (block.LastCodePart() == "ud") return block.Code;

            string[] angles = { "ns", "we" };
            int index = angle / 90;
            if (block.LastCodePart() == "we") index++;

            return block.CodeWithParts(angles[index % 2]);
        }
    }
}
