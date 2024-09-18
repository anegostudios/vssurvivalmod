using Vintagestory.API.Common;

namespace Vintagestory.GameContent
{
    // The 4 trader slots for selling stuff to the trader
    public class ItemSlotBuying : ItemSlotSurvival
    {
        InventoryTrader inv;

        public ItemSlotBuying(InventoryTrader inventory) : base(inventory)
        {
            this.inv = inventory;
        }

        public override bool CanHold(ItemSlot itemstackFromSourceSlot)
        {
            var bag = itemstackFromSourceSlot.Itemstack?.Collectible.GetCollectibleInterface<IHeldBag>() ?? null;

            return base.CanHold(itemstackFromSourceSlot) && (bag == null || bag.IsEmpty(itemstackFromSourceSlot.Itemstack)) && IsTraderInterested(itemstackFromSourceSlot);
        }

        private bool IsTraderInterested(ItemSlot slot)
        {
            return slot.Itemstack != null && inv.IsTraderInterestedIn(slot.Itemstack);
        }
    }
}
