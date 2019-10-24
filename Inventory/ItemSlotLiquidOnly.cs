using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace Vintagestory.GameContent
{
    public class ItemSlotLiquidOnly : ItemSlot
    {
        public ItemSlotLiquidOnly(InventoryBase inventory) : base(inventory)
        {
        }

        public override bool CanHold(ItemSlot itemstackFromSourceSlot)
        {
            WaterTightContainableProps props = BlockLiquidContainerBase.GetInContainerProps(itemstackFromSourceSlot.Itemstack);

            return props != null;
        }

        public override bool CanTake()
        {
            return true;
        }

        public override bool CanTakeFrom(ItemSlot sourceSlot)
        {
            if (inventory?.PutLocked == true) return false;

            ItemStack sourceStack = sourceSlot.Itemstack;
            if (sourceStack == null) return false;

            WaterTightContainableProps props = BlockLiquidContainerBase.GetInContainerProps(sourceStack);

            return props != null && (itemstack == null || itemstack.Collectible.GetMergableQuantity(itemstack, sourceStack) > 0) && RemainingSlotSpace > 0;
        }


        public override void ActivateSlot(ItemSlot sourceSlot, ref ItemStackMoveOperation op)
        {
            if (Empty) return;
            if (sourceSlot.CanHold(this))
            {
                if (sourceSlot.Itemstack != null && sourceSlot.Itemstack != null && sourceSlot.Itemstack.Collectible.GetMergableQuantity(sourceSlot.Itemstack, itemstack) < itemstack.StackSize) return;

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
