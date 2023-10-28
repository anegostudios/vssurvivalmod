using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace Vintagestory.GameContent
{
    public class ItemSlotTrade : ItemSlot
    {
        public ResolvedTradeItem TradeItem;

        public bool IsBuyingSlot { get; private set; }

        public override bool DrawUnavailable
        {
            get
            {
                return TradeItem?.Stock == 0;
            }
            set { }
        }

        public void SetTradeItem(ResolvedTradeItem tradeItem)
        {
            this.TradeItem = tradeItem;
            this.itemstack = tradeItem.Stack;
        }

        public ItemSlotTrade(InventoryBase inventory, bool isBuyingSlot = false) : base(inventory)
        {
            this.IsBuyingSlot = isBuyingSlot;
        }

        public override bool CanTake()
        {
            return false;
        }

        public override bool CanTakeFrom(ItemSlot sourceSlot, EnumMergePriority priority = EnumMergePriority.AutoMerge)
        {
            return false;
        }

        public override bool CanHold(ItemSlot sourceSlot)
        {
            return false;
        }

        
        public override bool TryFlipWith(ItemSlot itemSlot)
        {
            return false;
        }

        protected override void FlipWith(ItemSlot withSlot)
        {
            return;
        }

        public override void ActivateSlot(ItemSlot sourceSlot, ref ItemStackMoveOperation op)
        {
            
        }

        public override int TryPutInto(ItemSlot sinkSlot, ref ItemStackMoveOperation op)
        {
            return 0;
        }

        public override int TryPutInto(IWorldAccessor world, ItemSlot sinkSlot, int quantity = 1)
        {
            return 0;
        }

        public override string GetStackDescription(IClientWorldAccessor world, bool extendedDebugInfo)
        {
            if (TradeItem == null) return base.GetStackDescription(world, extendedDebugInfo);

            if (IsBuyingSlot)
            {
                if (itemstack.Collectible.GetMaxDurability(itemstack) > 1)
                {
                    return Lang.Get("tradeitem-demand-withdurability", TradeItem.Price, TradeItem.Stock) + "\n\n" + base.GetStackDescription(world, extendedDebugInfo);
                } else
                {
                    return Lang.Get("tradeitem-demand", TradeItem.Price, TradeItem.Stock) + "\n\n" + base.GetStackDescription(world, extendedDebugInfo);
                }
                
            } else
            {
                return Lang.Get("tradeitem-supply", TradeItem.Price, TradeItem.Stock) + "\n\n" + base.GetStackDescription(world, extendedDebugInfo);
            }
        }
    }
}
