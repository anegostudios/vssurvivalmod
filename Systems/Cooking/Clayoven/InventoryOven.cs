using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace Vintagestory.GameContent
{
    public class InventoryOven : InventoryBase, ISlotProvider
    {
        ItemSlot[] slots;
        readonly int cookingSize;
        public BlockPos pos;

        public InventoryOven(string inventoryID, int cookingSize, int fuelSize) : base(inventoryID, null)
        {
            slots = GenEmptySlots(cookingSize + 1);
            this.cookingSize = cookingSize;
            CookingSlots = new ItemSlot[cookingSize];
            for (int i = 0; i < cookingSize; i++)
            {
                CookingSlots[i] = slots[i];
                slots[i].MaxSlotStackSize = 1;
            }
        }

        public ItemSlot[] CookingSlots { get; }

        public ItemSlot[] Slots { get { return slots; } }

        public override int Count { get { return slots.Length; } }

        public override ItemSlot this[int slotId]
        {
            get
            {
                if (slotId < 0 || slotId >= Count) return null;
                return slots[slotId];
            }
            set
            {
                if (slotId < 0 || slotId >= Count) throw new ArgumentOutOfRangeException(nameof(slotId));
                slots[slotId] = value ?? throw new ArgumentNullException(nameof(value));
            }
        }

        public override void FromTreeAttributes(ITreeAttribute tree)
        {
            List<ItemSlot> modifiedSlots = new List<ItemSlot>();
            slots = SlotsFromTreeAttributes(tree, slots, modifiedSlots);
            for (int i = 0; i < modifiedSlots.Count; i++) MarkSlotDirty(GetSlotId(modifiedSlots[i]));

            if (Api != null)
            {
                for (int i = 0; i < CookingSlots.Length; i++)
                {
                    CookingSlots[i].MaxSlotStackSize = 1;
                }
            }
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            SlotsToTreeAttributes(slots, tree);
        }

        protected override ItemSlot NewSlot(int i)
        {
            return new ItemSlotSurvival(this);
        }

        public override float GetSuitability(ItemSlot sourceSlot, ItemSlot targetSlot, bool isMerge)
        {
            CombustibleProperties props = sourceSlot.Itemstack.Collectible.CombustibleProps;
            if (targetSlot == slots[cookingSize] && (props == null || props.BurnTemperature <= 0)) return 0;

            return base.GetSuitability(sourceSlot, targetSlot, isMerge);
        }
    }
}
