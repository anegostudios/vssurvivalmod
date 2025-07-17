using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent
{
    public class BlockTrapdoor : Block, IClaimTraverseable
    {
        public override int GetRetention(BlockPos pos, BlockFacing facing, EnumRetentionType type)
        {
            return 3;
        }
    }
}
