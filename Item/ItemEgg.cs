using System;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;

#nullable enable

namespace Vintagestory.GameContent
{
    public class ItemEgg : Item
    {
        public override int GetMergableQuantity(ItemStack sinkStack, ItemStack sourceStack, EnumMergePriority priority)
        {
            string[] ignored = new string[GlobalConstants.IgnoredStackAttributes.Length + 1];
            ignored[0] = "chick";
            Array.Copy(GlobalConstants.IgnoredStackAttributes, 0, ignored, 1, GlobalConstants.IgnoredStackAttributes.Length);
            if (Equals(sinkStack, sourceStack, ignored) && sinkStack.StackSize < MaxStackSize)
            {
                return Math.Min(MaxStackSize - sinkStack.StackSize, sourceStack.StackSize);
            }

            return 0;
        }

        public override void TryMergeStacks(ItemStackMergeOperation op)
        {
            IAttribute? sourceChick = op.SourceSlot.Itemstack?.Attributes?["chick"];
            IAttribute? sinkChick = op.SinkSlot.Itemstack?.Attributes?["chick"];
            bool chickDataMatches = (sourceChick == null && sinkChick == null) || (sourceChick != null && sourceChick.Equals(sinkChick));
            base.TryMergeStacks(op);
            if (op.MovedQuantity > 0 && !chickDataMatches)
            {
                op.SinkSlot.Itemstack?.Attributes?.RemoveAttribute("chick");
            }
        }

    }
}
