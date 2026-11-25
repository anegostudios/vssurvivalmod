using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent
{
    public class InventoryGear : InventoryBase
    {
        ItemSlot[] slots;

        public InventoryGear(string className, string id, ICoreAPI api) : base(className, id, api)
        {
            slots = GenEmptySlots(19);
            baseWeight = 2.5f;
        }

        public InventoryGear(string inventoryId, ICoreAPI api) : base(inventoryId, api)
        {
            slots = GenEmptySlots(19);
            baseWeight = 2.5f;
        }

        public override void OnItemSlotModified(ItemSlot slot)
        {
            base.OnItemSlotModified(slot);
        }


        public override int Count
        {
            get { return slots.Length; }
        }

        public override ItemSlot this[int slotId] { get { return slots[slotId]; }  set { slots[slotId] = value; } }

        public override void FromTreeAttributes(ITreeAttribute tree)
        {
            List<ItemSlot> modifiedSlots = new List<ItemSlot>();
            slots = SlotsFromTreeAttributes(tree, slots, modifiedSlots);
            for (int i = 0; i < modifiedSlots.Count; i++) DidModifyItemSlot(modifiedSlots[i]);
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            SlotsToTreeAttributes(slots, tree);
        }

        Dictionary<EnumCharacterDressType, string> iconByDressType = new Dictionary<EnumCharacterDressType, string>()
        {
            { EnumCharacterDressType.Foot, "boots" },
            { EnumCharacterDressType.Hand, "gloves" },
            { EnumCharacterDressType.Shoulder, "cape" },
            { EnumCharacterDressType.Head, "hat" },
            { EnumCharacterDressType.LowerBody, "trousers" },
            { EnumCharacterDressType.UpperBody, "shirt" },
            { EnumCharacterDressType.UpperBodyOver, "pullover" },
            { EnumCharacterDressType.Neck, "necklace" },
            { EnumCharacterDressType.Arm, "bracers" },
            { EnumCharacterDressType.Waist, "belt" },
            { EnumCharacterDressType.Emblem, "medal" },
            { EnumCharacterDressType.Face, "face" },
        };


        protected override ItemSlot NewSlot(int slotId)
        {
            if (slotId == 15 || slotId == 16) return new ItemSlotSurvival(this);
            if (slotId > 16)
            {
                return new ItemSlotBackpack(this);
            }

            EnumCharacterDressType type = (EnumCharacterDressType)slotId;
            ItemSlotCharacter slot = new ItemSlotCharacter(type, this);
            iconByDressType.TryGetValue(type, out slot.BackgroundIcon);

            return slot;
        }


        public override void DiscardAll()
        {
            base.DiscardAll();
            for (int i = 0; i < Count; i++)
            {
                DidModifyItemSlot(this[i]);
            }
        }


        public override void OnOwningEntityDeath(Vec3d pos)
        {
            // Don't drop contents on death
        }
    }
}
