using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace Vintagestory.GameContent
{
    public class ItemDeadButterfly : Item
    {

        public override string GetHeldItemName(ItemStack itemStack)
        {
            return Lang.Get("item-deadbutterfly", Lang.Get("item-creature-butterfly-" + Variant["type"]));
        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            dsc.AppendLine(Lang.Get("Butterfly"));
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
        }
    }
}
