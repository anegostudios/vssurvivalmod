using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class ResolvedTradeItem
    {
        public string Name;
        public ItemStack Stack;
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
            Name = tree.GetString("name");
            Stack = tree.GetItemstack("stack");
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
            tree.SetString("name", Name);
            tree.SetItemstack("stack", Stack);
            tree.SetInt("price", Price);
            tree.SetInt("stock", Stock);
            tree.SetFloat("restockHourDelay", Restock.HourDelay);
            tree.SetFloat("restockQuantity", Restock.Quantity);

            tree.SetFloat("supplyDemandPriceChangePerDay", SupplyDemand.PriceChangePerDay);
            tree.SetFloat("supplyDemandPriceChangePerPurchase", SupplyDemand.PriceChangePerPurchase);
        }
    }
}
