using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
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

        public override void DoCheckPlayerInventory(IPlayerInventoryManager inventoryManager)
        {
            if (MatchEventName is "onitemcollected")
            {
                var stacks = inventoryManager.Inventories.Where(val => val.Key is not GlobalConstants.creativeInvClassName and not GlobalConstants.groundInvClassName)
                                                         .Select(val => val.Value)
                                                         .SelectMany(inv => inv.Where(slot => slot?.Itemstack != null && StackMatcher(slot.Itemstack))
                                                                               .Select(slot => slot.Itemstack));

                if (stacks.Count() > 0)
                {
                    QuantityAchieved = stacks.Sum(stack => stack.StackSize);
                    Dirty = true;
                }
            }
        }
    }
}