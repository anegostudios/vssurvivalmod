using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
