using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent
{
    public class PlayerLookatBlockWatcher : PlayerMilestoneWatcherGeneric
    {
        public BlockLookatMatcherDelegate BlockMatcher;

        public override void OnBlockLookedAt(BlockSelection blockSel)
        {
            if (BlockMatcher(blockSel))
            {
                QuantityAchieved=1;
                Dirty = true;
            }
        }
    }

    public class PlayerPlaceBlockWatcher : PlayerMilestoneWatcherGeneric
    {
        public BlockMatcherDelegate BlockMatcher;

        public override void OnBlockPlaced(BlockPos pos, Block block, ItemStack withStackInHands)
        {
            if (BlockMatcher(pos, block, withStackInHands))
            {
                QuantityAchieved++;
                Dirty = true;
            }
        }
    }

    public class PlayerReceiveItemWatcher : PlayerMilestoneWatcherGeneric
    {
        public ItemStackMatcherDelegate StackMatcher;
        public string MatchEventName;

        public override void OnItemStackReceived(ItemStack stack, string eventName)
        {
            if (eventName == MatchEventName && !MilestoneReached())
            {
                if (StackMatcher(stack))
                {
                    QuantityAchieved += stack.StackSize;
                    Dirty = true;
                }
            }
        }
    }
}