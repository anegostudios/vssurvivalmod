using Vintagestory.API.Common;

#nullable disable

namespace Vintagestory.GameContent
{
    public interface ITradeableCollectible
    {
        bool ShouldTrade(EntityTradingHumanoid eTrader, TradeItem tradeIdem, EnumTradeDirection tradeDir);
        EnumTransactionResult OnTryTrade(EntityTradingHumanoid eTrader, ItemSlot tradeSlot, EnumTradeDirection tradeDir);
        bool OnDidTrade(EntityTradingHumanoid eTrader, ItemStack stack, EnumTradeDirection tradeDir);
    }
}
