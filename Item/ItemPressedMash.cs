using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

#nullable disable

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

        public override List<ItemStack> GetHandBookStacks(ICoreClientAPI capi)
        {
            List<ItemStack> stacks = base.GetHandBookStacks(capi);
            if (stacks != null)
            {
                foreach (var stack in stacks)
                {
                    var props = stack?.ItemAttributes?["juiceableProperties"]?.AsObject<JuiceableProperties>(null, stack.Collectible.Code.Domain);
                    props?.LiquidStack?.Resolve(api.World, "juiceable properties liquidstack");
                    props?.PressedStack?.Resolve(api.World, "juiceable properties pressedstack");
                    props?.ReturnStack?.Resolve(api.World, "juiceable properties returnstack");

                    if (props?.ReturnStack?.ResolvedItemstack != null) stack.Attributes.SetDouble("juiceableLitresLeft", 1);
                }
            }

            return stacks;
        }

        public override ItemStack OnTransitionNow(ItemSlot slot, TransitionableProperties props)
        {
            float pressedDryRatio = slot.Itemstack.ItemAttributes["juiceableProperties"]["pressedDryRatio"].AsFloat(1);
            double juiceableLitresTotal = slot.Itemstack.Attributes.GetDouble("juiceableLitresLeft") + slot.Itemstack.Attributes.GetDouble("juiceableLitresTransfered");
            TransitionableProperties nProps = props.Clone();

            if (juiceableLitresTotal > 0) nProps.TransitionRatio = props.TransitionRatio * (int)(GameMath.RoundRandom(api.World.Rand, (float)juiceableLitresTotal) * pressedDryRatio);

            return base.OnTransitionNow(slot, nProps);
        }
    }
}
