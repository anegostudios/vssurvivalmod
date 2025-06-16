using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent
{
    public class RestockOpts
    {
        public float Quantity = 1;
        public float HourDelay = 24;
    }

    public class SupplyDemandOpts
    {
        public float PriceChangePerPurchase = 0.1f;
        public float PriceChangePerDay = -0.1f;
    }

    public class TradeList
    {
        public int MaxItems;
        public TradeItem[] List;
    }

    public class TradeProperties
    {
        public NatFloat Money;
        public TradeList Buying;
        public TradeList Selling;
    }
}
