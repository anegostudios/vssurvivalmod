using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    /// <summary>
    /// Inventory with one fuel slot, one ore slot, one output slot and an optional 4 cooking slots
    /// </summary>
    public class InventorySmelting : InventoryBase, ISlotProvider
    {
        ItemSlot[] slots;
        ItemSlot[] cookingSlots;
        public BlockPos pos;
        int defaultStorageType = (int)(EnumItemStorageFlags.General | EnumItemStorageFlags.Agriculture | EnumItemStorageFlags.Alchemy | EnumItemStorageFlags.Jewellery | EnumItemStorageFlags.Metallurgy | EnumItemStorageFlags.Outfit);

        public ItemSlot[] CookingSlots { get { return HaveCookingContainer ? cookingSlots.Take(slots[1].Itemstack.ItemAttributes["cookingContainerSlots"].AsInt(4)).ToArray() : new ItemSlot[0]); } }

        /// <summary>
        /// Returns the cooking slots
        /// </summary>
        public ItemSlot[] Slots
        {
            get { return cookingSlots; }
        }

        public override Size3f MaxContentDimensions {
            get {
                return slots[1].Itemstack?.ItemAttributes?["maxContentDimensions"].AsObject<Size3f>(null);
            }
            set { }
        }

        public bool HaveCookingContainer
        {
            get { return slots[1].Itemstack?.ItemAttributes?.KeyExists("cookingContainerSlots") == true; }
        }

        public float CookingSlotCapacityLitres
        {
            get { return slots?[1]?.Itemstack?.ItemAttributes?["cookingSlotCapacityLitres"].AsFloat(6) ?? 6; }
        }

        public int CookingContainerMaxSlotStackSize
        {
            get {
                if (!HaveCookingContainer) return 0;
                return slots[1].Itemstack.ItemAttributes["maxContainerSlotStackSize"].AsInt(999);
            }
        }

        public override bool CanContain(ItemSlot sinkSlot, ItemSlot sourceSlot)
        {
            int slotid = GetSlotId(sinkSlot);
            return slotid < 3 || base.CanContain(sinkSlot, sourceSlot);
        }

        public InventorySmelting(string inventoryID, ICoreAPI api) : base(inventoryID, api)
        {
            // slot 0 = fuel
            // slot 1 = input
            // slot 2 = output
            // slot 3,4,5,6 = extra input slots with crucible in input
            slots = GenEmptySlots(11);
            cookingSlots = new ItemSlot[] { slots[3], slots[4], slots[5], slots[6], slots[7], slots[8], slots[9], slots[10] };
            baseWeight = 4f;
            
        }

        public InventorySmelting(string className, string instanceID, ICoreAPI api) : base(className, instanceID, api)
        {
            slots = GenEmptySlots(11);
            cookingSlots = new ItemSlot[] { slots[3], slots[4], slots[5], slots[6], slots[7], slots[8], slots[9], slots[10] };
            baseWeight = 4f;
        }

        public override void LateInitialize(string inventoryID, ICoreAPI api)
        {
            base.LateInitialize(inventoryID, api);

            for (int i = 0; i < cookingSlots.Length; i++)
            {
                cookingSlots[i].MaxSlotStackSize = CookingContainerMaxSlotStackSize;
            }

            updateStorageTypeFromContainer(slots[1].Itemstack);
        }

        public override int Count
        {
            get { return slots.Length; }
        }

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
                if (value == null) throw new ArgumentNullException(nameof(value));
                slots[slotId] = value;
            }
        }


        public override void DidModifyItemSlot(ItemSlot slot, ItemStack extractedStack = null)
        {
            base.DidModifyItemSlot(slot, extractedStack);

            if (slots[1] == slot)
            {
                if (slot?.Itemstack?.ItemAttributes?["storageType"].Exists != true)
                {
                    discardCookingSlots();
                } else
                {
                    updateStorageTypeFromContainer(slot.Itemstack);
                }
            }
        }

        void updateStorageTypeFromContainer(ItemStack stack)
        {
            int storageType = defaultStorageType;
            if (stack?.ItemAttributes?["storageType"] != null)
            {
                storageType = stack.ItemAttributes["storageType"].AsInt(defaultStorageType);
            }

            for (int i = 0; i < cookingSlots.Length; i++)
            {
                cookingSlots[i].StorageType = (EnumItemStorageFlags)storageType;
                cookingSlots[i].MaxSlotStackSize = CookingContainerMaxSlotStackSize;
                (cookingSlots[i] as ItemSlotWatertight).capacityLitres = CookingSlotCapacityLitres;
            }
        }


        public override float GetTransitionSpeedMul(EnumTransitionType transType, ItemStack stack)
        {
            return base.GetTransitionSpeedMul(transType, stack);
        }

        public void discardCookingSlots()
        {
            Vec3d droppos = pos.ToVec3d().Add(0.5, 0.5, 0.5);

            for (int i = 0; i < cookingSlots.Length; i++)
            {
                if (cookingSlots[i] == null) continue;
                Api.World.SpawnItemEntity(cookingSlots[i].Itemstack, droppos);
                cookingSlots[i].Itemstack = null;
            }
        }


        public override void FromTreeAttributes(ITreeAttribute tree)
        {
            List<ItemSlot> modifiedSlots = new List<ItemSlot>();
            slots = SlotsFromTreeAttributes(tree, slots, modifiedSlots);
            for (int i = 0; i < modifiedSlots.Count; i++) DidModifyItemSlot(modifiedSlots[i]);

            if (Api != null)
            {
                for (int i = 0; i < cookingSlots.Length; i++)
                {
                    cookingSlots[i].MaxSlotStackSize = CookingContainerMaxSlotStackSize;
                }
            }
        }
            
        

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            SlotsToTreeAttributes(slots, tree);
        }

        public override void OnItemSlotModified(ItemSlot slot)
        {
            base.OnItemSlotModified(slot);
        }

        protected override ItemSlot NewSlot(int i)
        {
            if (i == 0) return new ItemSlotSurvival(this); // Fuel
            if (i == 1) return new ItemSlotInput(this, 2);
            if (i == 2) return new ItemSlotOutput(this);

            return new ItemSlotWatertight(this, CookingSlotCapacityLitres);
        }


        public override WeightedSlot GetBestSuitedSlot(ItemSlot sourceSlot, ItemStackMoveOperation op, List<ItemSlot> skipSlots = null)
        {
            if (!HaveCookingContainer)
            {
                if (skipSlots == null) skipSlots = new List<ItemSlot>();
                skipSlots.Add(slots[2]);
                skipSlots.Add(slots[3]);
                skipSlots.Add(slots[4]);
                skipSlots.Add(slots[5]);
                skipSlots.Add(slots[6]);
            }

            WeightedSlot slot = base.GetBestSuitedSlot(sourceSlot, op, skipSlots);

            return slot;
        }


        public override float GetSuitability(ItemSlot sourceSlot, ItemSlot targetSlot, bool isMerge)
        {
            ItemStack stack = sourceSlot.Itemstack;

            if (targetSlot == slots[1] && (stack.Collectible is BlockSmeltingContainer || stack.Collectible is BlockCookingContainer)) return 2.2f;

            if (targetSlot == slots[0] && (stack.Collectible.CombustibleProps == null || stack.Collectible.CombustibleProps.BurnTemperature <= 0)) return 0;
            if (targetSlot == slots[1] && (stack.Collectible.CombustibleProps == null || stack.Collectible.CombustibleProps.SmeltedStack  == null)) return 0.5f;


            return base.GetSuitability(sourceSlot, targetSlot, isMerge);
        }


        public string GetOutputText()
        {
            ItemStack inputStack = slots[1].Itemstack;

            if (inputStack == null) return null;

            if (inputStack.Collectible is BlockSmeltingContainer)
            {
                return ((BlockSmeltingContainer)inputStack.Collectible).GetOutputText(Api.World, this, slots[1]);
            }
            if (inputStack.Collectible is BlockCookingContainer)
            {
                return ((BlockCookingContainer)inputStack.Collectible).GetOutputText(Api.World, this, slots[1]);
            }

            ItemStack smeltedStack = inputStack.Collectible.CombustibleProps?.SmeltedStack?.ResolvedItemstack;

            if (smeltedStack == null) return null;
            if (inputStack.Collectible.CombustibleProps.SmeltingType == EnumSmeltType.Fire) return Lang.Get("Can't smelt, requires a kiln");
            if (inputStack.Collectible.CombustibleProps.RequiresContainer) return Lang.Get("Can't smelt, requires smelting container (i.e. Crucible)");

            return Lang.Get("firepit-gui-willcreate", inputStack.StackSize / inputStack.Collectible.CombustibleProps.SmeltedRatio, smeltedStack.GetName());
        }


    }
}
