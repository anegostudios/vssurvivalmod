using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;

namespace Vintagestory.GameContent
{
    public enum EnumTransactionResult
    {
        PlayerNotEnoughAssets,
        TraderNotEnoughAssets,
        Failure,
        Success,
    };

    public class InventoryTrader : InventoryBase
    {
        EntityTrader traderEntity;
        
        // Slots 0..15: Selling slots
        // Slots 16..19: Buying cart
        // Slots 20..35: Buying slots
        // Slots 36..39: Selling cart
        // Slot 40: Money slot
        ItemSlot[] slots;
        


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

        public InventoryTrader(string inventoryID, ICoreAPI api) : base(inventoryID, api)
        {
            slots = GenEmptySlots(Count);
        }

        public InventoryTrader(string className, string instanceID, ICoreAPI api) : base(className, instanceID, api)
        {
            slots = GenEmptySlots(Count);
        }


        internal void LateInitialize(string id, ICoreAPI api, EntityTrader traderEntity)
        {
            base.LateInitialize(id, api);
            this.traderEntity = traderEntity;

            if (traderEntity?.TradeProps != null)
            {
                for (int slotId = 0; slotId < slots.Length; slotId++)
                {
                    if (!(slots[slotId] is ItemSlotTrade) || slots[slotId].Empty) continue;

                    string name = (slots[slotId] as ItemSlotTrade).TradeItem?.Name;

                    if (slotId < 20)
                    {
                        (slots[slotId] as ItemSlotTrade).TradeItem = GetTradeItemByName(name, traderEntity.TradeProps.Selling);
                    }
                    else
                    {
                        (slots[slotId] as ItemSlotTrade).TradeItem = GetTradeItemByName(name, traderEntity.TradeProps.Buying);
                    }

                }
            }
        }

        public override object ActivateSlot(int slotId, ItemSlot mouseSlot, ref ItemStackMoveOperation op)
        {
            // Player clicked an item from the selling list, move to buying cart
            if (slotId <= 15)
            {
                AddToBuyingCart(slots[slotId] as ItemSlotTrade);
                return InvNetworkUtil.GetActivateSlotPacket(slotId, op); ;
            }

            // Player clicked an item in the buying cart, remove it
            if (slotId <= 19)
            {
                ItemSlotTrade cartSlot = slots[slotId] as ItemSlotTrade;

                if (op.MouseButton == EnumMouseButton.Right)
                {
                    // Just remove one batch on right mouse    
                    cartSlot.TakeOut(cartSlot.TradeItem.Stack.StackSize);
                    cartSlot.MarkDirty();
                } else
                {
                    cartSlot.Itemstack = null;
                    cartSlot.MarkDirty();
                }
                
                return InvNetworkUtil.GetActivateSlotPacket(slotId, op); ;
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

                if (slot.Itemstack.Equals(sellingSlot.Itemstack) && slot.Itemstack.StackSize + sellingSlot.TradeItem.Stack.StackSize <= slot.Itemstack.Collectible.MaxStackSize)
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


        private ResolvedTradeItem GetTradeItemByName(string name, TradeList tradeList)
        {
            if (name == null) return null;

            for (int i = 0; i < tradeList.List.Length; i++)
            {
                TradeItem item = tradeList.List[i];
                string itemname = item.Name == null ? i + "" : item.Name;
                if (itemname.Equals(name)) return item.Resolve(Api.World);
            }

            return null;
        }



        public override void FromTreeAttributes(ITreeAttribute tree)
        {
            slots = SlotsFromTreeAttributes(tree);
            ITreeAttribute tradeItems = tree.GetTreeAttribute("tradeItems");

            if (tradeItems == null) return;

            for (int slotId = 0; slotId < slots.Length; slotId++)
            {
                if (!(slots[slotId] is ItemSlotTrade) || slots[slotId].Empty) continue;

                (slots[slotId] as ItemSlotTrade).TradeItem = new ResolvedTradeItem(tradeItems.GetTreeAttribute(slotId + ""));
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
                (slots[i] as ItemSlotTrade).TradeItem?.ToTreeAttributes(subtree);
                tradeItemTree[i + ""] = subtree;
            }

            tree["tradeItems"] = tradeItemTree;
        }


        internal EnumTransactionResult TryBuySell(IPlayer buyingPlayer)
        {
            if (!HasPlayerEnoughAssets(buyingPlayer)) return EnumTransactionResult.PlayerNotEnoughAssets;
            if (!HasTraderEnoughAssets()) return EnumTransactionResult.TraderNotEnoughAssets;

            if (Api.Side == EnumAppSide.Client)
            {
                for (int i = 0; i < 4; i++) GetBuyingCartSlot(i).Itemstack = null;
                return EnumTransactionResult.Success;
            }

            // Take care of they moneys first
            if (!HandleMoneyTransaction(buyingPlayer)) return EnumTransactionResult.Failure;

            // Now hand over buying cart contents
            for (int i = 0; i < 4; i++)
            {
                ItemSlotTrade slot = GetBuyingCartSlot(i);
                if (slot.Itemstack == null) continue;

                GiveOrDrop(buyingPlayer, slot.Itemstack);

                slot.TradeItem.Stock -= slot.Itemstack.StackSize / slot.TradeItem.Stack.StackSize;
                slot.Itemstack = null;
                slot.MarkDirty();
            }

            // And delete selling cart contents
            for (int i = 0; i < 4; i++)
            {
                ItemSlot slot = GetSellingCartSlot(i);
                if (slot.Itemstack == null) continue;

                ResolvedTradeItem tradeitem = GetBuyingConditions(slot.Itemstack);
                if (tradeitem == null) continue;

                slot.Itemstack = null;
                slot.MarkDirty();
            }

            return EnumTransactionResult.Success;
        }


        public bool HasPlayerEnoughAssets(IPlayer buyingPlayer)
        {
            int playerAssets = GetPlayerAssets(buyingPlayer);
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
            int playerAssets = GetPlayerAssets(buyingPlayer);
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
                DeductFromPlayer(buyingPlayer, deduct);
                GiveToTrader(deduct);
            } else
            {
                GiveOrDropToPlayer(buyingPlayer, new ItemStack(Api.World.GetItem(new AssetLocation("gear-rusty"))), -deduct);
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


        public void DeductFromPlayer(IPlayer buyingPlayer, int totalUnitsToDeduct)
        {
            SortedDictionary<int, List<ItemSlot>> moneys = new SortedDictionary<int, List<ItemSlot>>();

            buyingPlayer.Entity.WalkInventory((invslot) =>
            {
                if (invslot is ItemSlotCreative) return true;
                if (invslot.Itemstack == null || invslot.Itemstack.Collectible.Attributes == null) return true;

                JsonObject obj = invslot.Itemstack.Collectible.Attributes["currency"];
                if (obj.Exists && obj["value"].Exists)
                {
                    int pieceValue = obj["value"].AsInt(0);

                    List<ItemSlot> slots = null;
                    if (!moneys.TryGetValue(pieceValue, out slots)) slots = new List<ItemSlot>();

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

                        totalUnitsToDeduct -= removeUnits;
                    }

                    if (totalUnitsToDeduct <= 0) break;
                }
            }

            // ...and return single value gears 
            if (totalUnitsToDeduct < 0)
            {
                GiveOrDropToPlayer(buyingPlayer, new ItemStack(Api.World.GetItem(new AssetLocation("gear-rusty"))), -totalUnitsToDeduct);
            }
        }

        public void GiveOrDrop(IPlayer buyingPlayer, ItemStack stack)
        {
            if (stack == null) return;

            GiveOrDropToPlayer(buyingPlayer, stack, stack.StackSize);
        }

        public void GiveOrDropToPlayer(IPlayer buyingPlayer, ItemStack stack, int quantity)
        {
            if (stack == null) return;

            while (quantity > 0)
            {
                int stacksize = Math.Min(quantity, stack.Collectible.MaxStackSize);
                if (stacksize <= 0) return;

                ItemStack stackPart = stack.Clone();
                stackPart.StackSize = stacksize;

                if (!buyingPlayer.InventoryManager.TryGiveItemstack(stackPart, true))
                {
                    Api.World.SpawnItemEntity(stackPart, buyingPlayer.Entity.Pos.XYZ);
                }

                quantity -= stacksize;
            }
        }


        public int GetPlayerAssets(IPlayer player)
        {
            int totalAssets = 0;

            player.Entity.WalkInventory((invslot) =>
            {
                if (invslot is ItemSlotCreative) return true;
                if (invslot.Itemstack == null || invslot.Itemstack.Collectible.Attributes == null) return true;
                if (!(invslot.Inventory is InventoryBasePlayer)) return true;

                JsonObject obj = invslot.Itemstack.Collectible.Attributes["currency"];
                if (obj.Exists && obj["value"].Exists)
                {
                    totalAssets += obj["value"].AsInt(0) * invslot.StackSize;
                }

                return true;
            });

            return totalAssets;
        }


        public int GetTraderAssets()
        {
            int totalAssets = 0;
            if (MoneySlot.Empty) return 0;

            JsonObject obj = MoneySlot.Itemstack.Collectible.Attributes["currency"];
            if (obj.Exists && obj["value"].Exists)
            {
                totalAssets += obj["value"].AsInt(0) * MoneySlot.StackSize;
            }

            return totalAssets;
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

                ResolvedTradeItem tradeitem = GetBuyingConditions(sellSlot.Itemstack);

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

            return new ItemSlotSurvival(this);
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

        public ResolvedTradeItem GetBuyingConditions(ItemStack forStack)
        {
            for (int i = 0; i < 4*4; i++)
            {
                ItemSlotTrade slot = GetBuyingSlot(i);
                if (slot.Itemstack == null) continue;

                if (slot.Itemstack.Equals(Api.World, forStack, GlobalConstants.IgnoredStackAttributes))
                {
                    return slot.TradeItem;
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

    }
}
