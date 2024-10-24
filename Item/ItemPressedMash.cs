using System;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace Vintagestory.GameContent
{
    public class ItemPressedMash : Item
    {
        public override string GetHeldItemName(ItemStack itemStack)
        {
            float availableLitres = (float)Math.Round(itemStack.Attributes.GetDecimal("juiceableLitresLeft"), 2);
            string ap = availableLitres > 0 ? "wet" : "dry";
            string type = ItemClass.Name();

            return Lang.GetMatching(Code?.Domain + AssetLocation.LocationSeparator + type + "-" + Code?.Path + "-" + ap);
        }

        public override ItemStack OnTransitionNow(ItemSlot slot, TransitionableProperties props)
        {
            if (props.Type == EnumTransitionType.Perish)
            {
                float pressedDryRatio = slot.Itemstack.ItemAttributes["juiceableProperties"]["pressedDryRatio"].AsFloat(1);
                double juiceableLitresTotal = slot.Itemstack.Attributes.GetDouble("juiceableLitresLeft") + slot.Itemstack.Attributes.GetDouble("juiceableLitresTransfered");

                if (juiceableLitresTotal > 0)
                {
                    int stacksize = GameMath.RoundRandom(api.World.Rand, (float)juiceableLitresTotal);
                    slot.Itemstack.Attributes.RemoveAttribute("juiceableLitresTransfered");
                    slot.Itemstack.Attributes.RemoveAttribute("juiceableLitresLeft");
                    slot.Itemstack.Attributes.RemoveAttribute("squeezeRel");
                    props.TransitionRatio *= (int)(stacksize * pressedDryRatio);
                }
            }

            return base.OnTransitionNow(slot, props);
        }
    }
}
