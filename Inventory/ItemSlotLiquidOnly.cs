using Vintagestory.API.Common;

namespace Vintagestory.GameContent
{
    public class ItemSlotLiquidOnly : ItemSlot
    {
        public float CapacityLitres;

        public ItemSlotLiquidOnly(InventoryBase inventory, float capacityLitres) : base(inventory)
        {
            this.CapacityLitres = capacityLitres;
        }

        public override bool CanHold(ItemSlot itemstackFromSourceSlot)
        {
            WaterTightContainableProps props = BlockLiquidContainerBase.GetContainableProps(itemstackFromSourceSlot.Itemstack);

            return props != null;
        }

        public override bool CanTake()
        {
            return true;
        }

        public override bool CanTakeFrom(ItemSlot sourceSlot, EnumMergePriority priority = EnumMergePriority.AutoMerge)
        {
            if (inventory?.PutLocked == true) return false;

            ItemStack sourceStack = sourceSlot.Itemstack;
            if (sourceStack == null) return false;

            WaterTightContainableProps props = BlockLiquidContainerBase.GetContainableProps(sourceStack);

            return props != null && (itemstack == null || itemstack.Collectible.GetMergableQuantity(itemstack, sourceStack, priority) > 0) && GetRemainingSlotSpace(sourceStack) > 0;
        }


        public override void ActivateSlot(ItemSlot sourceSlot, ref ItemStackMoveOperation op)
        {
            if (Empty) return;
            if (sourceSlot.CanHold(this))
            {
                if (sourceSlot.Itemstack != null && sourceSlot.Itemstack != null && sourceSlot.Itemstack.Collectible.GetMergableQuantity(sourceSlot.Itemstack, itemstack, op.CurrentPriority) < itemstack.StackSize) return;

                op.RequestedQuantity = StackSize;

                TryPutInto(sourceSlot, ref op);

                if (op.MovedQuantity > 0)
                {
                    OnItemSlotModified(itemstack);
                }
            }
        }
    }
}
