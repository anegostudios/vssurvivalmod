using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockTrapdoor : Block
    {
        public override int GetRetention(BlockPos pos, BlockFacing facing, EnumRetentionType type)
        {
            return 3;
        }
    }
}
