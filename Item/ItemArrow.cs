using System;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class ItemArrow : Item
    {
        public override void GetHeldItemInfo(ItemStack stack, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(stack, dsc, world, withDebugInfo);

            if (stack.Collectible.Attributes == null) return;

            float dmg = stack.Collectible.Attributes["damage"].AsFloat(0);
            if (dmg != 0) dsc.AppendLine((dmg > 0 ? "+" : "") + dmg + " piercing damage");
        }
    }
}
