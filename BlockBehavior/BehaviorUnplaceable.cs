using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockBehaviorUnplaceable : BlockBehavior
    {
        public BlockBehaviorUnplaceable(Block block) : base(block)
        {
        }

        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref EnumHandling handled)
        {
            handled = EnumHandling.PreventSubsequent;
            return false;
        }
    }
}
