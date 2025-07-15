using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent
{
    public class BlockLeavesNarrow : BlockLeaves
    {
        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            string woodType = Variant["wood"];
            return new ItemStack(world.GetBlock(CodeWithParts("placed", woodType, "5")));
        }
    }
}
