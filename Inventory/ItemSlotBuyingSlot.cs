using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;

namespace Vintagestory.GameContent
{
    public class ItemSlotBuyingSlot : ItemSlotOutput
    {
        public ItemSlotBuyingSlot(InventoryBase inventory) : base(inventory)
        {
        }

        public override void ActivateSlot(ItemSlot sourceSlot, ref ItemStackMoveOperation op)
        {
            return;
        }

        public override bool CanTake()
        {
            return false;
        }
    }
}
