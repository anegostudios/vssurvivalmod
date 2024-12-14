using Vintagestory.API.Common;

namespace Vintagestory.GameContent
{
    public interface ITradeableCollectible
    {
        bool ShouldTrade(EntityTrader eTrader, TradeItem tradeIdem, EnumTradeDirection tradeDir);

        EnumTransactionResult OnTryTrade(EntityTrader eTrader, ItemSlot tradeSlot, EnumTradeDirection tradeDir);
        bool OnDidTrade(EntityTrader eTrader, ItemStack stack, EnumTradeDirection tradeDir);
    }
}
