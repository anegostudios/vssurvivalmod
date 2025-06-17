using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent
{
    public class InventoryOven : InventoryBase, ISlotProvider
    {
        ItemSlot[] slots;
        readonly int cookingSize;
        public BlockPos pos;

        public InventoryOven(string inventoryID, int bakeableSlots) : base(inventoryID, null)
        {
            slots = GenEmptySlots(bakeableSlots + 1);
            this.cookingSize = bakeableSlots;
            CookingSlots = new ItemSlot[bakeableSlots];
            for (int i = 0; i < bakeableSlots; i++)
            {
                CookingSlots[i] = slots[i];
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
        public override ItemSlot GetAutoPushIntoSlot(BlockFacing atBlockFace, ItemSlot fromSlot)
        {
            return null;
        }
    }
}
