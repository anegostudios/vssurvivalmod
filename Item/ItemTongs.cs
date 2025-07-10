using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

#nullable disable

namespace Vintagestory.GameContent
{
    public class ItemTongs : Item, IHeldHandAnimOverrider
    {
        public bool AllowHeldIdleHandAnim(Entity forEntity, ItemSlot slot, EnumHand hand)
        {
            return !isHoldingHotItem(forEntity);
        }

        public override string GetHeldTpIdleAnimation(ItemSlot activeHotbarSlot, Entity forEntity, EnumHand hand)
        {
            if (isHoldingHotItem(forEntity))
            {
                return "holdbothhands-tongs1";
            }

            return base.GetHeldTpIdleAnimation(activeHotbarSlot, forEntity, hand);
        }

        private static bool isHoldingHotItem(Entity forEntity)
        {
            if (forEntity is EntityPlayer eplr && !eplr.RightHandItemSlot.Empty)
            {
                var stack = eplr.RightHandItemSlot.Itemstack;
                if (stack.Collectible.GetTemperature(forEntity.World, stack) > 200)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
