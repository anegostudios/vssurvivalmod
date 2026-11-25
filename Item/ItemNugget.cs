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


        public override void OnCreatedByCrafting(ItemSlot[] allInputslots, ItemSlot outputSlot, IRecipeBase byRecipe)
        {
            ItemSlot oreSlot = allInputslots.FirstOrDefault(slot => slot.Itemstack?.Collectible is ItemOre);
            if (oreSlot != null)
            {
                int units = oreSlot.Itemstack.ItemAttributes["metalUnits"].AsInt(5);
                outputSlot.Itemstack.StackSize = Math.Max(1, units / 5);
            }

            base.OnCreatedByCrafting(allInputslots, outputSlot, byRecipe);
        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            CombustibleProperties combustibleProps = GetCombustibleProperties(world, inSlot.Itemstack, null);

            if (combustibleProps?.SmeltedStack == null)
            {
                base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
                return;
            }

            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

            string smelttype = combustibleProps.SmeltingType.ToString().ToLowerInvariant();
            int instacksize = combustibleProps.SmeltedRatio;
            int outstacksize = combustibleProps.SmeltedStack.ResolvedItemstack.StackSize;
            float units = outstacksize * 100f / instacksize;

            string metal = combustibleProps.SmeltedStack.ResolvedItemstack.Collectible?.Variant?["metal"];
            string metalname = Lang.Get("material-" + metal);
            if (metal == null) metalname = combustibleProps.SmeltedStack.ResolvedItemstack.GetName();

            string str = Lang.Get("game:smeltdesc-" + smelttype + "ore-plural", units.ToString("0.#"), metalname);
            dsc.AppendLine(str);
        }
    }
}
