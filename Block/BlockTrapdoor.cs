using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockTrapdoor : Block
    {
        public override int GetHeatRetention(BlockPos pos, BlockFacing facing)
        {
            return 3;
        }
    }
}
