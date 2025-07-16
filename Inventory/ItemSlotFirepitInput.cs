using Vintagestory.API.Config;

#nullable disable

namespace Vintagestory.API.Common
{
    /// <summary>
    /// A slot from which the player can only take stuff out of, but not place anything in it
    /// </summary>
    public class ItemSlotInput : ItemSlot
    {
        public int outputSlotId;


        public bool IsCookingContainer
        {
            get { return Itemstack?.ItemAttributes?.KeyExists("cookingContainerSlots") == true; }
        }

        public override int GetRemainingSlotSpace(ItemStack forItemstack)
        {
            if (IsCookingContainer) return 0;
            if (Empty && forItemstack?.ItemAttributes?.KeyExists("cookingContainerSlots") == true) return 1;
            return base.GetRemainingSlotSpace(forItemstack);
        }

        public ItemSlotInput(InventoryBase inventory, int outputSlotId) : base(inventory)
        {
            this.outputSlotId = outputSlotId;
        }


        public override int TryPutInto(ItemSlot sinkSlot, ref ItemStackMoveOperation op)
        {
            return base.TryPutInto(sinkSlot, ref op);
        }

        public override bool TryFlipWith(ItemSlot itemSlot)
        {
            if (!itemSlot.Empty && itemSlot.Itemstack?.ItemAttributes?.KeyExists("cookingContainerSlots") == true && itemSlot.StackSize > 1) return false;

            return base.TryFlipWith(itemSlot);
        }

        public override bool CanHold(ItemSlot slot)
        {
            return CanBeStackedWithOutputSlotItem(slot as ItemSlot);
        }

        public override bool CanTake()
        {
            return true;
        }

        public override bool CanTakeFrom(ItemSlot sourceSlot, EnumMergePriority priority = EnumMergePriority.AutoMerge)
        {
            return CanBeStackedWithOutputSlotItem(sourceSlot as ItemSlot) && base.CanTakeFrom(sourceSlot, priority);
        }


        public bool CanBeStackedWithOutputSlotItem(ItemSlot sourceSlot, bool notifySlot = true)
        {
            ItemSlot outslot = inventory[outputSlotId];
            if (outslot.Empty) return true;

            ItemStack compareStack = sourceSlot.Itemstack.Collectible.CombustibleProps?.SmeltedStack?.ResolvedItemstack;
            if (compareStack == null) compareStack = sourceSlot.Itemstack;
            
            if (!outslot.Itemstack.Equals(inventory.Api.World, compareStack, GlobalConstants.IgnoredStackAttributes))
            {
                outslot.Inventory.PerformNotifySlot(outputSlotId);
                return false;
            }

            return true;
        }


    }
}
