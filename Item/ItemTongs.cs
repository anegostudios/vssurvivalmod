using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace Vintagestory.GameContent
{
    public class ItemTongs : Item
    {
        public override string GetHeldTpIdleAnimation(ItemSlot activeHotbarSlot, Entity forEntity, EnumHand hand)
        {
            if (forEntity is EntityPlayer eplr && !eplr.RightHandItemSlot.Empty)
            {
                var stack = eplr.RightHandItemSlot.Itemstack;
                if (stack.Collectible.GetTemperature(forEntity.World, stack) > 200)
                {
                    return "holdbothhands";
                }
            }

            return base.GetHeldTpIdleAnimation(activeHotbarSlot, forEntity, hand);
        }
    }
}
