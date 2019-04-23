using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace Vintagestory.GameContent
{
    public class ItemSlotTrade : ItemSlot
    {
        public ResolvedTradeItem TradeItem;

        bool isBuyingSlot;

        public void SetTradeItem(ResolvedTradeItem tradeItem)
        {
            this.TradeItem = tradeItem;
            this.itemstack = tradeItem.Stack;
        }

        public ItemSlotTrade(InventoryBase inventory, bool isBuyingSlot = false) : base(inventory)
        {
            this.isBuyingSlot = isBuyingSlot;
        }

        public override bool CanTake()
        {
            return false;
        }

        public override bool CanTakeFrom(ItemSlot sourceSlot)
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

        public override void TryPutInto(ItemSlot sinkSlot, ref ItemStackMoveOperation op)
        {
            
        }

        public override void TryPutInto(IWorldAccessor world, ItemSlot sinkSlot, int quantity = 1)
        {
            
        }

        public override string GetStackDescription(IClientWorldAccessor world, bool extendedDebugInfo)
        {
            if (TradeItem == null) return base.GetStackDescription(world, extendedDebugInfo);

            if (isBuyingSlot)
            {
                return Lang.Get("Price: {0} gears\nDemand: {1}", TradeItem.Price, TradeItem.Stock) + "\n\n" + base.GetStackDescription(world, extendedDebugInfo);
            } else
            {
                return Lang.Get("Price: {0} gears\nSupply: {1}", TradeItem.Price, TradeItem.Stock) + "\n\n" + base.GetStackDescription(world, extendedDebugInfo);
            }

            
        }
    }
}
