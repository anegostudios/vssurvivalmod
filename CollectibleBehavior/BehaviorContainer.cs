using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class CollectibleBehaviorContainer : CollectibleBehavior
    {
        public CollectibleBehaviorContainer(CollectibleObject collObj) : base(collObj)
        {
        }

        public virtual float GetContainingTransitionModifierContained(IWorldAccessor world, ItemSlot inSlot, EnumTransitionType transType)
        {
            return 1;
        }

        public virtual float GetContainingTransitionModifierPlaced(IWorldAccessor world, BlockPos pos, EnumTransitionType transType)
        {
            return 1;
        }


        public virtual void SetContents(ItemStack containerStack, ItemStack[] stacks)
        {
            TreeAttribute stacksTree = new TreeAttribute();
            for (int i = 0; i < stacks.Length; i++)
            {
                stacksTree[i + ""] = new ItemstackAttribute(stacks[i]);
            }

            containerStack.Attributes["contents"] = stacksTree;
        }

        public virtual ItemStack[] GetContents(IWorldAccessor world, ItemStack itemstack)
        {
            ITreeAttribute treeAttr = itemstack.Attributes.GetTreeAttribute("contents");
            if (treeAttr == null) return new ItemStack[0];

            ItemStack[] stacks = new ItemStack[treeAttr.Count];
            foreach (var val in treeAttr)
            {
                ItemStack stack = (val.Value as ItemstackAttribute).value;
                if (stack != null) stack.ResolveBlockOrItem(world);

                if (int.TryParse(val.Key, out int index)) stacks[index] = stack;
            }

            return stacks;
        }

        public virtual ItemStack[] GetNonEmptyContents(IWorldAccessor world, ItemStack itemstack)
        {
            return GetContents(world, itemstack).Where(stack => stack != null).ToArray();
        }
    }
}
