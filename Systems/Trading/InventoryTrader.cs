using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent
{
    public enum EnumTransactionResult
    {
        PlayerNotEnoughAssets,
        TraderNotEnoughAssets,
        TraderNotEnoughSupplyOrDemand,
        Failure,
        Success,
    };

    public class InventoryTrader : InventoryBase
    {
        EntityTradingHumanoid traderEntity;

        // Slots 0..15: Selling slots
        // Slots 16..19: Buying cart
        // Slots 20..35: Buying slots
        // Slots 36..39: Selling cart
        // Slot 40: Money slot
        ItemSlot[] slots;

        /// <summary>
        /// Returns a new array containing all selling slots
        /// </summary>
        public ItemSlotTrade[] SellingSlots {
            get
            {
                ItemSlotTrade[] sellslots = new ItemSlotTrade[16];
                for (int i = 0; i < 16; i++) sellslots[i] = this.slots[i] as ItemSlotTrade;
                return sellslots;
            }
        }


        /// <summary>
        /// Returns a new array containing all selling slots
        /// </summary>
        public ItemSlotTrade[] BuyingSlots
        {
            get
            {
                ItemSlotTrade[] buyslots = new ItemSlotTrade[16];
                for (int i = 0; i < 16; i++) buyslots[i] = this.slots[20 + i] as ItemSlotTrade;
                return buyslots;
            }
        }


        public int BuyingCartTotalCost
        {
            get
            {
                return 0;
            }

        }

        public ItemSlot MoneySlot
        {
            get { return slots[40]; }
        }

        public override float GetTransitionSpeedMul(EnumTransitionType transType, ItemStack stack)
        {
            return 0;
        }

        public InventoryTrader(string inventoryID, ICoreAPI api) : base(inventoryID, api)
        {
            slots = GenEmptySlots(Count);
        }

        public InventoryTrader(string className, string instanceID, ICoreAPI api) : base(className, instanceID, api)
        {
            slots = GenEmptySlots(Count);
        }


        internal void LateInitialize(string id, ICoreAPI api, EntityTradingHumanoid traderEntity)
        {
            base.LateInitialize(id, api);
            this.traderEntity = traderEntity;

            // Never gets executed because trade slots are always empty at this point. What is this code good for?
            /*if (traderEntity?.TradeProps != null)
            {
                for (int slotId = 0; slotId < slots.Length; slotId++)
                {
                    if (!(slots[slotId] is ItemSlotTrade) || slots[slotId].Empty) continue;

                    string name = (slots[slotId] as ItemSlotTrade).TradeItem?.Name;

                    ItemSlotTrade tradeSlot = (slots[slotId] as ItemSlotTrade);

                    if (tradeSlot.TradeItem != null) continue;

                    tradeSlot.TradeItem = GetTradeItemByName(name, slotId < 20 ? traderEntity.TradeProps.Selling : traderEntity.TradeProps.Buying);
                }
            }*/
        }

        public override object ActivateSlot(int slotId, ItemSlot mouseSlot, ref ItemStackMoveOperation op)
        {
            // Player clicked an item from the selling list, move to buying cart
            if (slotId <= 15)
            {
                AddToBuyingCart(slots[slotId] as ItemSlotTrade);
                return InvNetworkUtil.GetActivateSlotPacket(slotId, op);
            }

            // Player clicked an item in the buying cart, remove it
            if (slotId <= 19)
            {
                ItemSlotTrade cartSlot = slots[slotId] as ItemSlotTrade;

                if (op.MouseButton == EnumMouseButton.Right)
                {
                    // Just remove one batch on right mouse
                    if (cartSlot.TradeItem?.Stack != null)
                    {
                        cartSlot.TakeOut(cartSlot.TradeItem.Stack.StackSize);
                        cartSlot.MarkDirty();
                    }
                } else
                {
                    cartSlot.Itemstack = null;
                    cartSlot.MarkDirty();
                }

                return InvNetworkUtil.GetActivateSlotPacket(slotId, op);
            }

            // Player clicked an item on the buy slot, ignore it
            if (slotId <= 34)
            {
                return InvNetworkUtil.GetActivateSlotPacket(slotId, op);
            }

            // Player clicked an item in the selling cart, act like a normal slot
            if (slotId <= 39)
            {
                return base.ActivateSlot(slotId, mouseSlot, ref op);
            }

            return InvNetworkUtil.GetActivateSlotPacket(slotId, op);
        }


        private void AddToBuyingCart(ItemSlotTrade sellingSlot)
        {
            if (sellingSlot.Empty) return;

            // Try merge existing first
            for (int i = 0; i < 4; i++)
            {
                ItemSlotTrade slot = slots[16 + i] as ItemSlotTrade;
                if (slot.Empty) continue;

                if (slot.Itemstack.Equals(Api.World, sellingSlot.Itemstack) && slot.Itemstack.StackSize + sellingSlot.TradeItem.Stack.StackSize <= slot.Itemstack.Collectible.MaxStackSize)
                {

                    slot.Itemstack.StackSize += (sellingSlot as ItemSlotTrade).TradeItem.Stack.StackSize;
                    slot.MarkDirty();
                    return;
                }
            }

            // Otherwise find an empty slot
            for (int i = 0; i < 4; i++)
            {
                ItemSlotTrade slot = slots[16 + i] as ItemSlotTrade;
                if (!slot.Empty) continue;

                slot.Itemstack = (sellingSlot as ItemSlotTrade).TradeItem.Stack.Clone();
                slot.Itemstack.ResolveBlockOrItem(Api.World);
                slot.TradeItem = (sellingSlot as ItemSlotTrade).TradeItem;
                slot.MarkDirty();
                return;
            }
        }


        public override int Count
        {
            get { return 4 * 4 + 4 + 4 * 4 + 4 + 1; }
        }

        public override ItemSlot this[int slotId]
        {
            get
            {
                if (slotId < 0 || slotId >= Count) return null;
                return slots[slotId];
            }
            set
            {
                if (slotId < 0 || slotId >= Count) throw new ArgumentOutOfRangeException(nameof(slotId));
                if (value == null) throw new ArgumentNullException(nameof(value));
                slots[slotId] = value;
            }
        }


        public override void FromTreeAttributes(ITreeAttribute tree)
        {
            slots = SlotsFromTreeAttributes(tree, slots);
            ITreeAttribute tradeItems = tree.GetTreeAttribute("tradeItems");

            if (tradeItems == null) return;

            for (int slotId = 0; slotId < slots.Length; slotId++)
            {
                if (!(slots[slotId] is ItemSlotTrade) || slots[slotId].Empty) continue;
                ItemSlotTrade tradeSlot = (slots[slotId] as ItemSlotTrade);

                tradeSlot.TradeItem = new ResolvedTradeItem(tradeItems.GetTreeAttribute(slotId + ""));

            }
        }


        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            SlotsToTreeAttributes(slots, tree);

            TreeAttribute tradeItemTree = new TreeAttribute();

            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i].Itemstack == null || !(slots[i] is ItemSlotTrade)) continue;
                TreeAttribute subtree = new TreeAttribute();
                ResolvedTradeItem tradeitem = (slots[i] as ItemSlotTrade).TradeItem;
                tradeitem?.ToTreeAttributes(subtree);
                tradeItemTree[i + ""] = subtree;
            }

            tree["tradeItems"] = tradeItemTree;
        }


        internal EnumTransactionResult TryBuySell(IPlayer buyingPlayer)
        {
            if (!HasPlayerEnoughAssets(buyingPlayer)) return EnumTransactionResult.PlayerNotEnoughAssets;
            if (!HasTraderEnoughAssets()) return EnumTransactionResult.TraderNotEnoughAssets;

            if (!HasTraderEnoughStock(buyingPlayer)) return EnumTransactionResult.TraderNotEnoughSupplyOrDemand;
            if (!HasTraderEnoughDemand(buyingPlayer)) return EnumTransactionResult.TraderNotEnoughSupplyOrDemand;

            if (Api.Side == EnumAppSide.Client)
            {
                for (int i = 0; i < 4; i++) GetBuyingCartSlot(i).Itemstack = null;
                return EnumTransactionResult.Success;
            }

            // Make sure all buying and selling items can be sold/bought
            for (int i = 0; i < 4; i++)
            {
                ItemSlotTrade slot = GetBuyingCartSlot(i);
                if (slot.Itemstack?.Collectible is ITradeableCollectible itc)
                {
                    var result = itc.OnTryTrade(traderEntity, slot, EnumTradeDirection.Buy);
                    if (result != EnumTransactionResult.Success) return result;
                }

            }

            for (int i = 0; i < 4; i++)
            {
                ItemSlot slot = GetSellingCartSlot(i);
                if (slot.Itemstack?.Collectible is ITradeableCollectible itc)
                {
                    var result = itc.OnTryTrade(traderEntity, slot, EnumTradeDirection.Sell);
                    if (result != EnumTransactionResult.Success) return result;
                }
            }


            // 2. Take care of the money first
            if (!HandleMoneyTransaction(buyingPlayer)) return EnumTransactionResult.Failure;


            // 3. Now hand over buying cart contents
            for (int i = 0; i < 4; i++)
            {
                ItemSlotTrade slot = GetBuyingCartSlot(i);
                if (slot.Itemstack == null) continue;

                GiveOrDrop(buyingPlayer.Entity, slot.Itemstack);

                slot.TradeItem.Stock -= slot.Itemstack.StackSize / slot.TradeItem.Stack.StackSize;
                slot.Itemstack = null;
                slot.MarkDirty();
            }

            // 4. And delete selling cart contents
            for (int i = 0; i < 4; i++)
            {
                ItemSlot slot = GetSellingCartSlot(i);
                if (slot.Itemstack == null) continue;

                ResolvedTradeItem tradeItem = GetBuyingConditionsSlot(slot.Itemstack).TradeItem;
                if (tradeItem == null) continue;

                int q = slot.Itemstack.StackSize / tradeItem.Stack.StackSize;

                tradeItem.Stock -= q;
                var stack = slot.TakeOut(q * tradeItem.Stack.StackSize);

                if (stack.Collectible is ITradeableCollectible itc) itc.OnDidTrade(traderEntity, stack, EnumTradeDirection.Buy);

                slot.MarkDirty();
            }

            return EnumTransactionResult.Success;
        }


        public bool HasTraderEnoughStock(IPlayer player)
        {
            Dictionary<int, int> Stocks = new Dictionary<int, int>();

            for (int i = 0; i < 4; i++)
            {
                ItemSlotTrade slot = GetBuyingCartSlot(i);
                if (slot.Itemstack == null) continue;

                ItemSlotTrade tradeSlot = GetSellingConditionsSlot(slot.Itemstack);

                int tradeslotid = GetSlotId(tradeSlot);
                if (!Stocks.TryGetValue(tradeslotid, out int stock))
                {
                    stock = slot.TradeItem.Stock;
                }

                Stocks[tradeslotid] = stock - slot.Itemstack.StackSize / slot.TradeItem.Stack.StackSize;

                if (Stocks[tradeslotid] < 0)
                {
                    player.InventoryManager.NotifySlot(player, slot);
                    player.InventoryManager.NotifySlot(player, tradeSlot);

                    return false;
                }
            }

            return true;
        }


        public bool HasTraderEnoughDemand(IPlayer player)
        {
            Dictionary<int, int> Stocks = new Dictionary<int, int>();

            for (int i = 0; i < 4; i++)
            {
                ItemSlot slot = GetSellingCartSlot(i);
                if (slot.Itemstack == null) continue;

                ItemSlotTrade tradeSlot = GetBuyingConditionsSlot(slot.Itemstack);
                ResolvedTradeItem tradeItem = tradeSlot?.TradeItem;

                if (tradeItem == null)
                {
                    player.InventoryManager.NotifySlot(player, slot);
                    return false;
                }

                int tradeslotid = GetSlotId(tradeSlot);
                if (!Stocks.TryGetValue(tradeslotid, out int stock))
                {
                    stock = tradeItem.Stock;
                }

                Stocks[tradeslotid] = stock - slot.Itemstack.StackSize / tradeItem.Stack.StackSize;

                if (Stocks[tradeslotid] < 0)
                {
                    player.InventoryManager.NotifySlot(player, tradeSlot);
                    player.InventoryManager.NotifySlot(player, slot);
                    return false;
                }
            }

            return true;
        }


        public bool IsTraderInterestedIn(ItemStack stack)
        {
            ItemSlotTrade tradeSlot = GetBuyingConditionsSlot(stack);
            ResolvedTradeItem tradeItem = tradeSlot?.TradeItem;

            if (tradeItem == null)
            {
                return false;
            }

            if (tradeItem.Stock == 0)
            {
                PerformNotifySlot(GetSlotId(tradeSlot));
            }

            return tradeItem.Stock > 0;
        }




        public bool HasPlayerEnoughAssets(IPlayer buyingPlayer)
        {
            int playerAssets = GetPlayerAssets(buyingPlayer.Entity);
            int totalCost = GetTotalCost();
            int totalGain = GetTotalGain();

            if (playerAssets - totalCost + totalGain < 0) return false;

            return true;
        }

        public bool HasTraderEnoughAssets()
        {
            int traderAssets = GetTraderAssets();
            int totalCost = GetTotalCost();
            int totalGain = GetTotalGain();

            if (traderAssets + totalCost - totalGain < 0) return false;

            return true;
        }

        bool HandleMoneyTransaction(IPlayer buyingPlayer)
        {
            int playerAssets = GetPlayerAssets(buyingPlayer.Entity);
            int traderAssets = GetTraderAssets();
            int totalCost = GetTotalCost();
            int totalGain = GetTotalGain();

            // Player does not have enough money
            if (playerAssets - totalCost + totalGain < 0) return false;

            // Trader does not have enough money
            if (traderAssets + totalCost - totalGain < 0) return false;


            int deduct = totalCost - totalGain;

            if (deduct > 0)
            {
                DeductFromEntity(Api, buyingPlayer.Entity, deduct);
                GiveToTrader(deduct);
            } else
            {
                GiveOrDrop(buyingPlayer.Entity, new ItemStack(Api.World.GetItem(new AssetLocation("gear-rusty"))), -deduct, null);
                DeductFromTrader(-deduct);
            }

            return true;
        }

        public void GiveToTrader(int units)
        {
            if (MoneySlot.Empty)
            {
                MoneySlot.Itemstack = new ItemStack(Api.World.GetItem(new AssetLocation("gear-rusty")), units);
            } else
            {
                MoneySlot.Itemstack.StackSize += units;
            }
            MoneySlot.MarkDirty();
        }


        public void DeductFromTrader(int units)
        {
            MoneySlot.Itemstack.StackSize -= units;
            if (MoneySlot.StackSize <= 0) MoneySlot.Itemstack = null;
            MoneySlot.MarkDirty();
        }


        public static void DeductFromEntity(ICoreAPI api, EntityAgent eagent, int totalUnitsToDeduct)
        {
            SortedDictionary<int, List<ItemSlot>> moneys = new SortedDictionary<int, List<ItemSlot>>();

            eagent.WalkInventory((invslot) =>
            {
                if (invslot is ItemSlotCreative) return true;
                if (invslot.Itemstack == null || invslot.Itemstack.Collectible.Attributes == null) return true;

                int pieceValue = CurrencyValuePerItem(invslot);
                if (pieceValue != 0)
                {
                    if (!moneys.TryGetValue(pieceValue, out List<ItemSlot> slots)) slots = new List<ItemSlot>();

                    slots.Add(invslot);

                    moneys[pieceValue] = slots;
                }

                return true;
            });

            foreach (var val in moneys.Reverse())
            {
                int pieceValue = val.Key;

                foreach (ItemSlot slot in val.Value)
                {
                    int removeUnits = Math.Min(pieceValue * slot.StackSize, totalUnitsToDeduct);

                    removeUnits = (removeUnits / pieceValue) * pieceValue;

                    slot.Itemstack.StackSize -= removeUnits / pieceValue;
                    if (slot.StackSize <= 0) slot.Itemstack = null;
                    slot.MarkDirty();

                    totalUnitsToDeduct -= removeUnits;
                }

                if (totalUnitsToDeduct <= 0) break;
            }


            // Maybe didn't have small moneys? Take a bigger piece....
            if (totalUnitsToDeduct > 0)
            {
                foreach (var val in moneys)
                {
                    int pieceValue = val.Key;

                    foreach (ItemSlot slot in val.Value)
                    {
                        int removeUnits = Math.Max(pieceValue, Math.Min(pieceValue * slot.StackSize, totalUnitsToDeduct));

                        removeUnits = (removeUnits / pieceValue) * pieceValue;

                        slot.Itemstack.StackSize -= removeUnits / pieceValue;

                        if (slot.StackSize <= 0)
                        {
                            slot.Itemstack = null;
                        }
                        slot.MarkDirty();

                        totalUnitsToDeduct -= removeUnits;
                    }

                    if (totalUnitsToDeduct <= 0) break;
                }
            }

            // ...and return single value gears
            if (totalUnitsToDeduct < 0)
            {
                GiveOrDrop(eagent, new ItemStack(api.World.GetItem(new AssetLocation("gear-rusty"))), -totalUnitsToDeduct, null);
            }
        }

        public void GiveOrDrop(EntityAgent eagent, ItemStack stack)
        {
            if (stack == null) return;

            GiveOrDrop(eagent, stack, stack.StackSize, traderEntity);
        }

        public static void GiveOrDrop(EntityAgent eagent, ItemStack stack, int quantity, EntityTradingHumanoid entityTrader)
        {
            if (stack == null) return;

            while (quantity > 0)
            {
                int stacksize = Math.Min(quantity, stack.Collectible.MaxStackSize);
                if (stacksize <= 0) return;

                ItemStack stackPart = stack.Clone();
                stackPart.StackSize = stacksize;

                if (entityTrader != null && stackPart.Collectible is ITradeableCollectible itc) itc.OnDidTrade(entityTrader, stackPart, EnumTradeDirection.Sell);

                if (!eagent.TryGiveItemStack(stackPart))
                {
                    eagent.World.SpawnItemEntity(stackPart, eagent.Pos.XYZ);
                }
                quantity -= stacksize;
            }
        }


        public static int GetPlayerAssets(EntityAgent eagent)
        {
            int totalAssets = 0;

            eagent.WalkInventory((invslot) =>
            {
                if (invslot is ItemSlotCreative || !(invslot.Inventory is InventoryBasePlayer)) return true;

                totalAssets += CurrencyValuePerItem(invslot) * invslot.StackSize;

                return true;
            });

            return totalAssets;
        }


        public int GetTraderAssets()
        {
            int totalAssets = 0;
            if (MoneySlot.Empty) return 0;

            totalAssets += CurrencyValuePerItem(MoneySlot) * MoneySlot.StackSize;

            return totalAssets;
        }


        private static int CurrencyValuePerItem(ItemSlot slot)
        {
            JsonObject obj = slot.Itemstack?.Collectible?.Attributes?["currency"];
            if (obj != null && obj.Exists)
            {
                JsonObject v = obj["value"];
                return v.Exists ? v.AsInt(0) : 0;
            }
            return 0;
        }


        public int GetTotalCost()
        {
            int totalCost = 0;

            for (int i = 0; i < 4; i++)
            {
                ItemSlotTrade buySlot = GetBuyingCartSlot(i);
                ResolvedTradeItem tradeitem = buySlot.TradeItem;

                if (tradeitem != null)
                {
                    int cnt = buySlot.StackSize / tradeitem.Stack.StackSize;
                    totalCost += tradeitem.Price * cnt;
                }
            }

            return totalCost;
        }

        public int GetTotalGain()
        {
            int totalGain = 0;

            for (int i = 0; i < 4; i++)
            {
                ItemSlotSurvival sellSlot = GetSellingCartSlot(i);

                if (sellSlot.Itemstack == null) continue;

                ResolvedTradeItem tradeitem = GetBuyingConditionsSlot(sellSlot.Itemstack)?.TradeItem;

                if (tradeitem != null)
                {
                    int cnt = sellSlot.StackSize / tradeitem.Stack.StackSize;
                    totalGain += tradeitem.Price * cnt;
                }
            }

            return totalGain;
        }


        // Slots 0..15: Selling slots
        // Slots 16..19: Buying cart
        // Slots 20..35: Buying slots
        // Slots 36..39: Selling cart
        protected override ItemSlot NewSlot(int slotId)
        {
            if (slotId < 36) return new ItemSlotTrade(this, slotId > 19 && slotId <= 35);

            return new ItemSlotBuying(this);
        }

        public ItemSlotTrade GetSellingSlot(int index)
        {
            return slots[index] as ItemSlotTrade;
        }

        public ItemSlotTrade GetBuyingSlot(int index)
        {
            return slots[4*4 + 4 + index] as ItemSlotTrade;
        }


        public ItemSlotTrade GetBuyingCartSlot(int index)
        {
            return slots[16 + index] as ItemSlotTrade;
        }

        public ItemSlotSurvival GetSellingCartSlot(int index)
        {
            return slots[36 + index] as ItemSlotSurvival;
        }

        string[] ignoredAttrs = GlobalConstants.IgnoredStackAttributes.Append("backpack", "condition");

        public ItemSlotTrade GetBuyingConditionsSlot(ItemStack forStack)
        {
            for (int i = 0; i < 4*4; i++)
            {
                ItemSlotTrade slot = GetBuyingSlot(i);
                if (slot.Itemstack == null) continue;

                string[] ignoredAttributes = slot.TradeItem.AttributesToIgnore == null ? ignoredAttrs : ignoredAttrs.Append(slot.TradeItem.AttributesToIgnore.Split(','));
                if (slot.Itemstack.Equals(Api.World, forStack, ignoredAttributes) || slot.Itemstack.Satisfies(forStack))
                {
                    if (forStack.Collectible.IsReasonablyFresh(traderEntity.World, forStack)) return slot;
                }
            }

            return null;
        }

        public ItemSlotTrade GetSellingConditionsSlot(ItemStack forStack)
        {
            for (int i = 0; i < 4 * 4; i++)
            {
                ItemSlotTrade slot = GetSellingSlot(i);
                if (slot.Itemstack == null) continue;

                if (slot.Itemstack.Equals(Api.World, forStack, GlobalConstants.IgnoredStackAttributes))
                {
                    return slot;
                }
            }

            return null;
        }


        public override object Close(IPlayer player)
        {
            object p = base.Close(player);

            for (int i = 0; i < 4; i++)
            {
                // Clear buying cart
                slots[i + 16].Itemstack = null;

                // Drop selling cart contents
                Api.World.SpawnItemEntity(slots[i + 36].Itemstack, traderEntity.ServerPos.XYZ);
                slots[i + 36].Itemstack = null;
            }

            return p;
        }

        #region Shift-clicking

        public override WeightedSlot GetBestSuitedSlot(ItemSlot sourceSlot, ItemStackMoveOperation op, List<ItemSlot> skipSlots = null)
        {
            WeightedSlot bestWSlot = new WeightedSlot();

            if (PutLocked || sourceSlot.Inventory == this) return bestWSlot;

            if (!IsTraderInterestedIn(sourceSlot.Itemstack)) return bestWSlot;

            // Don't allow any shift-clicking of currency
            if (CurrencyValuePerItem(sourceSlot) != 0) return bestWSlot;

            // 1. Prefer already filled slots - only allowing shift-clicking into the 4 Selling Cart slots
            for (int i = 0; i < 4; i++)
            {
                ItemSlot slot = GetSellingCartSlot(i);
                if (skipSlots != null && skipSlots.Contains(slot)) continue;

                if (slot.CanTakeFrom(sourceSlot))
                {
                    float curWeight = GetSuitability(sourceSlot, slot, slot.Itemstack != null);

                    if (bestWSlot.slot == null || bestWSlot.weight < curWeight)
                    {
                        bestWSlot.slot = slot;
                        bestWSlot.weight = curWeight;
                    }
                }
            }

            return bestWSlot;
        }

        #endregion

    }
}
