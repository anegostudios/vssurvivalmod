using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
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

        public IItemSlot[] CookingSlots { get { return HaveCookingContainer ? cookingSlots : new IItemSlot[0]; } }

        public IItemSlot[] Slots
        {
            get { return cookingSlots; }
        }

        public bool HaveCookingContainer
        {
            get { return slots[1].Itemstack?.Collectible.Attributes?.KeyExists("cookingContainerSlots") == true; }
        }

        public InventorySmelting(string inventoryID, ICoreAPI api) : base(inventoryID, api)
        {
            // slot 0 = fuel
            // slot 1 = input
            // slot 2 = output
            // slot 3,4,5,6 = extra input slots with crucible in input
            slots = GenEmptySlots(7);
            cookingSlots = new ItemSlot[] { slots[3], slots[4], slots[5], slots[6] };
        }

        public InventorySmelting(string className, string instanceID, ICoreAPI api) : base(className, instanceID, api)
        {
            slots = GenEmptySlots(7);
            cookingSlots = new ItemSlot[] { slots[3], slots[4], slots[5], slots[6] };
        }

        public override int QuantitySlots
        {
            get { return slots.Length; }
        }

        public override void DidModifyItemSlot(IItemSlot slot, IItemStack extractedStack = null)
        {
            base.DidModifyItemSlot(slot, extractedStack);

            if (slots[1] == slot)
            {
                if (slot.Itemstack == null)
                {
                    discardCookingSlots();
                } else
                {
                    int storageType = defaultStorageType;
                    if (slot.Itemstack.ItemAttributes?["storageType"] != null)
                    {
                        storageType = slot.Itemstack.ItemAttributes["storageType"].AsInt(defaultStorageType);
                    }

                    for (int i = 0; i < cookingSlots.Length; i++)
                    {
                        cookingSlots[i].StorageType = (EnumItemStorageFlags)storageType;
                    }
                }
                
            }
        }


        public override ItemSlot GetSlot(int slotId)
        {
            return slots[slotId];
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
            for (int i = 0; i < modifiedSlots.Count; i++) MarkSlotDirty(GetSlotId(modifiedSlots[i]));

        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            SlotsToTreeAttributes(slots, tree);
        }

        public override void OnItemSlotModified(IItemSlot slot)
        {
            base.OnItemSlotModified(slot);
        }

        protected override ItemSlot NewSlot(int i)
        {
            if (i == 2) return new ItemSlotOutput(this);

            return new ItemSlotSurvival(this);
        }


        public override WeightedSlot GetBestSuitedSlot(IPlayer actingPlayer, ItemSlot sourceSlot, List<IItemSlot> skipSlots = null)
        {
            if (!HaveCookingContainer)
            {
                skipSlots.Add(slots[2]);
                skipSlots.Add(slots[3]);
                skipSlots.Add(slots[4]);
                skipSlots.Add(slots[5]);
                skipSlots.Add(slots[6]);
            }

            WeightedSlot slot = base.GetBestSuitedSlot(actingPlayer, sourceSlot, skipSlots);

            return slot;
        }


        public override float GetSuitability(IPlayer actingPlayer, ItemSlot sourceSlot, ItemSlot targetSlot, bool isMerge)
        {
            ItemStack stack = sourceSlot.Itemstack;

            if (targetSlot == slots[0] && (stack.Collectible.CombustibleProps == null || stack.Collectible.CombustibleProps.BurnTemperature <= 0)) return 0;

            return base.GetSuitability(actingPlayer, sourceSlot, targetSlot, isMerge);
        }


        public string GetOutputText()
        {
            ItemStack inputStack = slots[1].Itemstack;

            if (inputStack == null) return null;

            if (inputStack.Collectible is BlockSmeltingContainer)
            {
                return ((BlockSmeltingContainer)inputStack.Collectible).GetOutputText(Api.World, this, slots[1]);
            }

            ItemStack smeltedStack = inputStack.Collectible.CombustibleProps?.SmeltedStack?.ResolvedItemstack;

            if (smeltedStack == null) return null;
            if (inputStack.Collectible.CombustibleProps.RequiresContainer) return "Can't smelt, requires smelting container (i.e. Crucible)";

            return string.Format("Will create {0}x {1}", inputStack.StackSize / inputStack.Collectible.CombustibleProps.SmeltedRatio, smeltedStack.GetName());
        }


    }
}
