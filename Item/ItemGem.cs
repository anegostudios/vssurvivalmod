using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace Vintagestory.GameContent
{
    public class ItemGem : Item
    {
        public override TransitionState[] UpdateAndGetTransitionStates(IWorldAccessor world, ItemSlot inslot)
        {
            if (!inslot.Itemstack.Attributes.HasAttribute("potential"))
            {
                inslot.Itemstack.Attributes.SetString("potential", "low");
            }

            return base.UpdateAndGetTransitionStates(world, inslot);
        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            if (inSlot.Itemstack.Attributes != null)
            {
                string potential = inSlot.Itemstack.Attributes.GetString("potential", "low");

                dsc.AppendLine(Lang.Get("Potential: {0}", Lang.Get("gem-potential-" + potential)));
            }

            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
        }

    }
}
