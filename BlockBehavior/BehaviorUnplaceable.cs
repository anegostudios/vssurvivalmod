using Vintagestory.API.Common;

#nullable disable

namespace Vintagestory.GameContent
{
    public class BlockBehaviorUnplaceable : BlockBehavior
    {
        public BlockBehaviorUnplaceable(Block block) : base(block)
        {
        }

        public override bool CanPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref EnumHandling handling, ref string failureCode)
        {
            handling = EnumHandling.PreventSubsequent;
            failureCode = "__ignore__";
            return false;
        }
    }
}
