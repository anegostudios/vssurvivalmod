using System;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using System.Linq;

#nullable disable

namespace Vintagestory.GameContent
{
    public class ItemNugget : Item
    {


        public override void OnCreatedByCrafting(ItemSlot[] allInputslots, ItemSlot outputSlot, GridRecipe byRecipe)
        {
            ItemSlot oreSlot = allInputslots.FirstOrDefault(slot => slot.Itemstack?.Collectible is ItemOre);
            if (oreSlot != null)
            {
                int units = oreSlot.Itemstack.ItemAttributes["metalUnits"].AsInt(5);
                string type = oreSlot.Itemstack.Collectible.Variant["ore"].Replace("quartz_", "").Replace("galena_", "");

                Item item = api.World.GetItem(new AssetLocation("nugget-" + type));
                ItemStack outStack = new ItemStack(item);
                outStack.StackSize = Math.Max(1, units / 5);
                outputSlot.Itemstack = outStack;
            }

            base.OnCreatedByCrafting(allInputslots, outputSlot, byRecipe);
        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            
            if (CombustibleProps?.SmeltedStack == null)
            {
                base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
                return;
            }

            CombustibleProperties props = CombustibleProps;

            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

            string smelttype = CombustibleProps.SmeltingType.ToString().ToLowerInvariant();
            int instacksize = CombustibleProps.SmeltedRatio;
            int outstacksize = CombustibleProps.SmeltedStack.ResolvedItemstack.StackSize;
            float units = outstacksize * 100f / instacksize;

            string metal = CombustibleProps.SmeltedStack.ResolvedItemstack.Collectible?.Variant?["metal"];
            string metalname = Lang.Get("material-" + metal);
            if (metal == null) metalname = CombustibleProps.SmeltedStack.ResolvedItemstack.GetName();

            string str = Lang.Get("game:smeltdesc-" + smelttype + "ore-plural", units.ToString("0.#"), metalname);
            dsc.AppendLine(str);
        }
    }
}
