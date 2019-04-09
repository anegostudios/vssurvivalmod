using System;
using System.Text;
using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Config;

namespace Vintagestory.GameContent
{
    public class ItemNugget : Item
    {

        public override void GetHeldItemInfo(ItemStack stack, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            if (CombustibleProps?.SmeltedStack == null)
            {
                base.GetHeldItemInfo(stack, dsc, world, withDebugInfo);
                return;
            }

            CombustibleProperties props = CombustibleProps;

            base.GetHeldItemInfo(stack, dsc, world, withDebugInfo);

            string smelttype = CombustibleProps.SmeltingType.ToString().ToLowerInvariant();
            int instacksize = CombustibleProps.SmeltedRatio;
            int outstacksize = CombustibleProps.SmeltedStack.ResolvedItemstack.StackSize;
            float units = outstacksize * 100f / instacksize;

            string metalname = CombustibleProps.SmeltedStack.ResolvedItemstack.GetName().Replace(" ingot", "");

            string str = Lang.Get("game:smeltdesc-" + smelttype + "ore-plural", units.ToString("0.#"), metalname);
            dsc.AppendLine(str);
        }
    }
}
