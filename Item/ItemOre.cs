using System;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent
{
    public class ItemOre : ItemPileable
    {
        public bool IsCoal => Variant["ore"] == "lignite" || Variant["ore"] == "bituminouscoal" || Variant["ore"] == "anthracite";
        public override bool IsPileable => IsCoal;
        protected override AssetLocation PileBlockCode => new AssetLocation("coalpile");

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            var combustibleProps = inSlot.Itemstack.Collectible.GetCombustibleProperties(world, inSlot.Itemstack, null);
            if (combustibleProps?.SmeltedStack?.ResolvedItemstack == null)
            {
                if (Attributes?["metalUnits"].Exists == true)
                {
                    float units = Attributes["metalUnits"].AsInt();

                    string orename = LastCodePart(1);
                    if (orename.Contains("_"))
                    {
                        orename = orename.Split('_')[1];
                    }
                    AssetLocation loc = new AssetLocation("nugget-" + orename);
                    Item item = api.World.GetItem(loc);

                    if (item != null)
                    {
                        ItemStack nuggetStack = new ItemStack(item);
                        CombustibleProperties nuggetCombustibleProps = nuggetStack.Collectible.GetCombustibleProperties(world, nuggetStack, null);
                        if (nuggetCombustibleProps?.SmeltedStack?.ResolvedItemstack != null)
                        {
                            string smelttype = nuggetCombustibleProps.SmeltingType.ToString().ToLowerInvariant();

                            string metal = nuggetCombustibleProps.SmeltedStack.ResolvedItemstack.Collectible?.Variant?["metal"];
                            string metalname = Lang.Get("material-" + metal);
                            if (metal == null) metalname = nuggetCombustibleProps.SmeltedStack.ResolvedItemstack.GetName();

                            dsc.AppendLine(Lang.Get("game:smeltdesc-" + smelttype + "ore-plural", units.ToString("0.#"), metalname));
                        }
                    }

                    dsc.AppendLine(Lang.Get("Parent Material: {0}", Lang.Get("rock-" + LastCodePart())));
                    dsc.AppendLine();
                    dsc.AppendLine(Lang.Get("Crush with hammer to extract nuggets"));
                }
            }
            else
            {

                base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

                if (combustibleProps.SmeltedStack.ResolvedItemstack.Collectible.FirstCodePart() == "ingot")
                {
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

                return;
            }


            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
        }

        public override string GetHeldItemName(ItemStack itemStack)
        {
            if (Attributes?["metalUnits"].Exists == true)
            {
                string orename = LastCodePart(1);
                string rockname = LastCodePart(0);

                if (FirstCodePart() == "crystalizedore")
                {
                    return Lang.Get(LastCodePart(2) + "-crystallizedore-chunk", Lang.Get("ore-" + orename));

                }
                return Lang.Get(LastCodePart(2) + "-ore-chunk", Lang.Get("ore-" + orename));

            }

            return base.GetHeldItemName(itemStack);
        }

    }
}
