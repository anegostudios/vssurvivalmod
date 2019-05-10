using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace Vintagestory.GameContent
{
    // The 4 trader slots for selling stuff to the trader
    public class ItemSlotBuying : ItemSlotSurvival
    {
        InventoryTrader inv;

        public ItemSlotBuying(InventoryTrader inventory) : base(inventory)
        {
            this.inv = inventory;
        }

        public override bool CanHold(ItemSlot itemstackFromSourceSlot)
        {
            return
                base.CanHold(itemstackFromSourceSlot) &&
               (!CollectibleObject.IsBackPack(itemstackFromSourceSlot.Itemstack) || CollectibleObject.IsEmptyBackPack(itemstackFromSourceSlot.Itemstack)) &&
               IsTraderInterested(itemstackFromSourceSlot)
            ;
        }

        private bool IsTraderInterested(ItemSlot slot)
        {
            return slot.Itemstack != null && inv.IsTraderInterestedIn(slot.Itemstack);
        }
    }
}
