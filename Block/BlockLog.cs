using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockLog : Block
    {


        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            return Drops[0].ResolvedItemstack.Clone();
        }
    }
}
