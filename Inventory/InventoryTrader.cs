using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace Vintagestory.GameContent
{
    public class InventoryTrader : InventoryBase
    {
        ItemSlot[] slots;

        // Slots 0..24: Selling slots
        // Slots 25..29: Buying cart
        // Slots 30..55: Buying slots
        // Slots 56..60: Selling cart

        public InventoryTrader(string inventoryID, ICoreAPI api) : base(inventoryID, api)
        {
        }

        public InventoryTrader(string className, string instanceID, ICoreAPI api) : base(className, instanceID, api)
        {
        }

        public override int QuantitySlots
        {
            get
            {
                return 5 * 5 + 5 + 5 * 5 + 5;
            }
        }

        public override void FromTreeAttributes(ITreeAttribute tree)
        {
            slots = SlotsFromTreeAttributes(tree);
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            SlotsToTreeAttributes(slots, tree);
        }

        public override ItemSlot GetSlot(int slotId)
        {
            return slots[slotId];
        }


        protected override ItemSlot NewSlot(int slotId)
        {
            if (slotId < 25) return new ItemSellingSlot(this);
            if (slotId < 30) return new ItemSlotSurvival(this);
            if (slotId < 56) return new ItemSlotBuyingSlot(this);

            return new ItemSlotSurvival(this);
        }


    }
}
