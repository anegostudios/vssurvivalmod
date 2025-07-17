using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

#nullable disable

namespace Vintagestory.GameContent
{
    public class ResolvedTradeItem
    {
        //public string Name;
        public ItemStack Stack;
        /// <summary>
        /// Comma-separated string listing any custom attribute keys to ignore (for this TradeItem only, and only when the trader is buying) - for example, shield color
        /// </summary>
        public string AttributesToIgnore;
        public int Price;
        public int Stock;
        public RestockOpts Restock = new RestockOpts()
        {
            HourDelay = 24,
            Quantity = 1
        };
        public SupplyDemandOpts SupplyDemand;


        public ResolvedTradeItem() { }

        public ResolvedTradeItem(ITreeAttribute treeAttribute)
        {
            if (treeAttribute == null) return;

            FromTreeAttributes(treeAttribute);
        }

        public void FromTreeAttributes(ITreeAttribute tree)
        {
            //Name = tree.GetString("name");
            Stack = tree.GetItemstack("stack");
            AttributesToIgnore = tree.GetString("attributesToIgnore", null);
            Price = tree.GetInt("price");
            Stock = tree.GetInt("stock");
            Restock = new RestockOpts()
            {
                HourDelay = tree.GetFloat("restockHourDelay"),
                Quantity = tree.GetFloat("restockQuantity")
            };
            SupplyDemand = new SupplyDemandOpts()
            {
                PriceChangePerDay = tree.GetFloat("supplyDemandPriceChangePerDay"),
                PriceChangePerPurchase = tree.GetFloat("supplyDemandPriceChangePerPurchase")
            };

        }

        public void ToTreeAttributes(ITreeAttribute tree)
        {
            //tree.SetString("name", Name);
            tree.SetItemstack("stack", Stack);
            if (AttributesToIgnore != null) tree.SetString("attributesToIgnore", AttributesToIgnore);
            tree.SetInt("price", Price);
            tree.SetInt("stock", Stock);
            tree.SetFloat("restockHourDelay", Restock.HourDelay);
            tree.SetFloat("restockQuantity", Restock.Quantity);

            tree.SetFloat("supplyDemandPriceChangePerDay", SupplyDemand.PriceChangePerDay);
            tree.SetFloat("supplyDemandPriceChangePerPurchase", SupplyDemand.PriceChangePerPurchase);
        }
    }
}
