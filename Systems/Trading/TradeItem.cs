using System;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class TradeItem : JsonItemStack
    {
        //public string Name;
        public NatFloat Price;
        public NatFloat Stock;
        public RestockOpts Restock = new RestockOpts()
        {
            HourDelay = 24,
            Quantity = 1
        };
        public SupplyDemandOpts SupplyDemand = new SupplyDemandOpts()
        {
            PriceChangePerDay = 0.1f,
            PriceChangePerPurchase = 0.1f
        };
        public string AttributesToIgnore;

        public ResolvedTradeItem Resolve(IWorldAccessor world)
        {
            this.Resolve(world, "TradeItem");

            foreach (var attr in ResolvedItemstack.Attributes)
            {
                if (attr.Value.Equals("*"))
                {
                    ResolvedItemstack.Attributes.RemoveAttribute(attr.Key);
                    AttributesToIgnore = (AttributesToIgnore == null ? "" : AttributesToIgnore + ",") + attr.Key;
                }
            }

            return new ResolvedTradeItem()
            {
                Stack = this.ResolvedItemstack,
                AttributesToIgnore = this.AttributesToIgnore,
                Price = (int)Math.Max(1, Math.Round(Price.nextFloat(1f, world.Rand))),
                Stock = Stock == null ? 0 : (int)Math.Round(Stock.nextFloat(1f, world.Rand)),
                Restock = Restock,
                SupplyDemand = SupplyDemand
            };
        }
    }
}
