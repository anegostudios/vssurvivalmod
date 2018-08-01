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
    public class TradeItemSlot : ItemSlot
    {
        public ResolvedTradeItem TradeItem;

        public void SetTradeItem(ResolvedTradeItem tradeItem)
        {
            this.TradeItem = tradeItem;
            this.itemstack = tradeItem.Stack;
        }

        public TradeItemSlot(InventoryBase inventory) : base(inventory)
        {
        }

        public override bool CanTake()
        {
            return false;
        }

        public override bool CanTakeFrom(IItemSlot sourceSlot)
        {
            return false;
        }

        public override bool CanHold(IItemSlot sourceSlot)
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

        public override void TryPutInto(IItemSlot sinkSlot, ref ItemStackMoveOperation op)
        {
            
        }

        public override void TryPutInto(IWorldAccessor world, IItemSlot sinkSlot)
        {
            
        }

        public override string GetStackDescription(IClientWorldAccessor world, bool extendedDebugInfo)
        {
            if (TradeItem == null) return base.GetStackDescription(world, extendedDebugInfo);

            return Lang.Get("Price: {0} gears\nIn Stock: {1}", TradeItem.Price, TradeItem.Stock) + "\n\n" + base.GetStackDescription(world, extendedDebugInfo);
        }
    }
}
