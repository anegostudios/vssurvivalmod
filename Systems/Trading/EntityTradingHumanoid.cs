using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent
{
    public class EntityTradingHumanoid : EntityDressedHumanoid {
        public const int PlayerStoppedInteracting = 1212;

        public InventoryTrader Inventory;
        public TradeProperties TradeProps;
        public List<EntityPlayer> interactingWithPlayer = new List<EntityPlayer>();
        protected GuiDialog dlg;
        protected int tickCount = 0;
        protected double doubleRefreshIntervalDays = 7;

        protected EntityBehaviorConversable ConversableBh => GetBehavior<EntityBehaviorConversable>();

        public virtual EntityTalkUtil TalkUtil { get; }

        public override void Initialize(EntityProperties properties, ICoreAPI api, long InChunkIndex3d)
        {
            base.Initialize(properties, api, InChunkIndex3d);
            var bh = GetBehavior<EntityBehaviorConversable>();
            if (bh != null)
            {
                bh.OnControllerCreated += (controller) =>
                {
                    controller.DialogTriggers += Dialog_DialogTriggers;
                };
            }

            if (Inventory == null)
            {
                Inventory = new InventoryTrader("traderInv", "" + EntityId, api);
            }

            if (api.Side == EnumAppSide.Server)
            {
                var stringpath = Properties.Attributes?["tradePropsFile"].AsString();
                AssetLocation filepath = null;

                try
                {
                    if (stringpath != null)
                    {
                        filepath = stringpath == null ? null : AssetLocation.Create(stringpath, Code.Domain);

                        TradeProps = api.Assets.Get(filepath.WithPathAppendixOnce(".json")).ToObject<TradeProperties>();
                    }
                    else
                    {
                        TradeProps = Properties.Attributes["tradeProps"]?.AsObject<TradeProperties>();
                    }
                }
                catch (Exception e)
                {
                    api.World.Logger.Error("Failed deserializing TradeProperties for trader {0}, exception logged to verbose debug", properties.Code);
                    api.World.Logger.Error(e);
                    api.World.Logger.VerboseDebug("Failed deserializing TradeProperties:");
                    api.World.Logger.VerboseDebug("=================");
                    api.World.Logger.VerboseDebug("Tradeprops json:");
                    if (filepath != null) api.World.Logger.VerboseDebug("File path {0}:", filepath);
                    api.World.Logger.VerboseDebug("{0}", Properties.Server.Attributes["tradeProps"].ToJsonToken());
                }
            }

            try
            {
                Inventory.LateInitialize("traderInv-" + EntityId, api, this);
            }
            catch (Exception e)
            {
                api.World.Logger.Error("Failed initializing trader inventory. Will recreate. Exception logged to verbose debug");
                api.World.Logger.Error(e);
                api.World.Logger.VerboseDebug("Failed initializing trader inventory. Will recreate.");

                WatchedAttributes.RemoveAttribute("traderInventory");
                Inventory = new InventoryTrader("traderInv", "" + EntityId, api);
                Inventory.LateInitialize("traderInv-" + EntityId, api, this);

                RefreshBuyingSellingInventory();
            }
        }


        public override void OnEntitySpawn()
        {
            base.OnEntitySpawn();

            if (World.Api.Side == EnumAppSide.Server)
            {
                setupTaskBlocker();
                reloadTradingList();
            }
        }

        private void reloadTradingList()
        {
            if (TradeProps != null)
            {
                RefreshBuyingSellingInventory();
                WatchedAttributes.SetDouble("lastRefreshTotalDays", World.Calendar.TotalDays - World.Rand.NextDouble() * 6);
                Inventory.MoneySlot.Itemstack = null;
                Inventory.GiveToTrader((int)TradeProps.Money.nextFloat(1f, World.Rand));
            }
            else
            {
                //World.Logger.Warning("Trader TradeProps not set during trader entity spawn. Won't have any items for sale/purchase.");
            }
        }

        bool wasImported;

        public override void DidImportOrExport(BlockPos startPos)
        {
            base.DidImportOrExport(startPos);
            wasImported = true;
        }

        public override void OnEntityLoaded()
        {
            base.OnEntityLoaded();

            if (Api.Side == EnumAppSide.Server)
            {
                setupTaskBlocker();
                if (wasImported) reloadTradingList();
                if (WatchedAttributes.HasAttribute("tradingPlayerUID"))
                {
                    WatchedAttributes.RemoveAttribute("tradingPlayerUID");
                }
            }
        }

        protected void setupTaskBlocker()
        {
            var taskAi = GetBehavior<EntityBehaviorTaskAI>();
            if (taskAi != null) taskAi.TaskManager.OnShouldExecuteTask += (task) =>
            {
                return interactingWithPlayer.Count == 0;
            };

            var actAi = GetBehavior<EntityBehaviorActivityDriven>();
            if (actAi != null) actAi.OnShouldRunActivitySystem += () =>
            {
                return interactingWithPlayer.Count > 0 ? EnumInteruptionType.TradeRequested : EnumInteruptionType.None;
            };
        }

        protected void RefreshBuyingSellingInventory(float refreshChance = 1.1f)
        {
            if (TradeProps == null) return;

            TradeProps.Buying.List.Shuffle(World.Rand);
            int buyingQuantity = Math.Min(TradeProps.Buying.List.Length, TradeProps.Buying.MaxItems);

            TradeProps.Selling.List.Shuffle(World.Rand);
            int sellingQuantity = Math.Min(TradeProps.Selling.List.Length, TradeProps.Selling.MaxItems);

            // Pick quantity items from the trade list that the trader doesn't already sell
            // Slots 0..15: Selling slots
            // Slots 16..19: Buying cart
            // Slots 20..35: Buying slots
            // Slots 36..39: Selling cart
            // Slot 40: Money slot

            Stack<TradeItem> newBuyItems = new Stack<TradeItem>();
            Stack<TradeItem> newsellItems = new Stack<TradeItem>();

            ItemSlotTrade[] sellingSlots = Inventory.SellingSlots;
            ItemSlotTrade[] buyingSlots = Inventory.BuyingSlots;

            #region Avoid duplicate sales

            string[] ignoredAttributes = GlobalConstants.IgnoredStackAttributes.Append("condition");

            for (int i = 0; i < TradeProps.Selling.List.Length; i++)
            {
                if (newsellItems.Count >= sellingQuantity) break;

                TradeItem item = TradeProps.Selling.List[i];
                if (!item.Resolve(World, "tradeItem resolver")) continue;

                bool alreadySelling = sellingSlots.Any((slot) => slot?.Itemstack != null && slot.TradeItem.Stock > 0 && item.ResolvedItemstack?.Equals(World, slot.Itemstack, ignoredAttributes) == true);

                if (!alreadySelling)
                {
                    newsellItems.Push(item);
                }
            }

            for (int i = 0; i < TradeProps.Buying.List.Length; i++)
            {
                if (newBuyItems.Count >= buyingQuantity) break;

                TradeItem item = TradeProps.Buying.List[i];
                if (!item.Resolve(World, "tradeItem resolver")) continue;

                bool alreadySelling = buyingSlots.Any((slot) => slot?.Itemstack != null && slot.TradeItem.Stock > 0 && item.ResolvedItemstack?.Equals(World, slot.Itemstack, ignoredAttributes) == true);

                if (!alreadySelling)
                {
                    newBuyItems.Push(item);
                }
            }
            #endregion

            replaceTradeItems(newBuyItems, buyingSlots, buyingQuantity, refreshChance, EnumTradeDirection.Buy);
            replaceTradeItems(newsellItems, sellingSlots, sellingQuantity, refreshChance, EnumTradeDirection.Sell);

            ITreeAttribute tree = GetOrCreateTradeStore();
            Inventory.ToTreeAttributes(tree);
            WatchedAttributes.MarkAllDirty();
        }

        protected void replaceTradeItems(Stack<TradeItem> newItems, ItemSlotTrade[] slots, int quantity, float refreshChance, EnumTradeDirection tradeDir)
        {
            HashSet<int> refreshedSlots = new HashSet<int>();

            for (int i = 0; i < quantity; i++)
            {
                if (World.Rand.NextDouble() > refreshChance) continue;
                if (newItems.Count == 0) break;

                TradeItem newTradeItem = newItems.Pop();

                if (newTradeItem.ResolvedItemstack.Collectible is ITradeableCollectible itc)
                {
                    if (!itc.ShouldTrade(this, newTradeItem, tradeDir))
                    {
                        i--;
                        continue;
                    }
                }

                int duplSlotIndex = slots.IndexOf((bslot) => bslot.Itemstack != null && bslot.TradeItem.Stock == 0 && newTradeItem?.ResolvedItemstack.Equals(World, bslot.Itemstack, GlobalConstants.IgnoredStackAttributes) == true);

                ItemSlotTrade intoSlot;

                // The trader already sells this but is out of stock - replace
                if (duplSlotIndex != -1)
                {
                    intoSlot = slots[duplSlotIndex];
                    refreshedSlots.Add(duplSlotIndex);
                }
                else
                {
                    while (refreshedSlots.Contains(i)) i++;
                    if (i >= slots.Length) break;
                    intoSlot = slots[i];
                    refreshedSlots.Add(i);
                }

                var titem = newTradeItem.Resolve(World);
                if (titem.Stock > 0)
                {
                    intoSlot.SetTradeItem(titem);
                    intoSlot.MarkDirty();
                }
            }
        }


        protected virtual int Dialog_DialogTriggers(EntityAgent triggeringEntity, string value, JsonObject data)
        {
            if (value == "opentrade")
            {
                if (Alive && triggeringEntity.Pos.SquareDistanceTo(this.Pos) <= 7)
                {
                    if (WatchedAttributes.HasAttribute("tradingPlayerUID"))
                    {
                        if (Api is ICoreClientAPI capi)
                        {
                            capi.TriggerIngameError(this, "alreadyTrading",Lang.Get("trader-trading"));
                        }

                        return 0;
                    }

                    ConversableBh.Dialog?.TryClose();
                    TryOpenTradeDialog(triggeringEntity);
                    if (triggeringEntity is EntityPlayer plr)
                    {
                        interactingWithPlayer.Add(plr);
                        if (Api is ICoreServerAPI)
                        {
                            WatchedAttributes.SetString("tradingPlayerUID", plr.PlayerUID);
                        }
                    }
                } else
                {
                    if (World.Side == EnumAppSide.Server) {
                        var plr = (triggeringEntity as EntityPlayer).Player as IServerPlayer;
                        (Api as ICoreServerAPI).Network.SendEntityPacket(plr, this.EntityId, PlayerStoppedInteracting);
                    }
                    return 0;
                }
            }

            return -1;
        }

        void TryOpenTradeDialog(EntityAgent forEntity)
        {
            if (World.Side != EnumAppSide.Client) return;

            EntityPlayer entityplr = forEntity as EntityPlayer;
            IPlayer player = World.PlayerByUid(entityplr.PlayerUID);

            ICoreClientAPI capi = (ICoreClientAPI)Api;

            if (dlg?.IsOpened() != true)
            {
                // Will break all kinds of things if we allow multiple concurrent of these dialogs
                if (capi.Gui.OpenedGuis.FirstOrDefault(dlg => dlg is GuiDialogTrader && dlg.IsOpened()) == null)
                {
                    capi.Network.SendEntityPacket(this.EntityId, 1001);
                    player.InventoryManager.OpenInventory(Inventory);

                    dlg = new GuiDialogTrader(Inventory, this, World.Api as ICoreClientAPI);
                    dlg.OnClosed += Dlg_OnClosed;
                    dlg.TryOpen();

                }
                else
                {
                    capi.TriggerIngameError(this, "onlyonedialog", Lang.Get("Can only trade with one trader at a time"));
                }
            }
            else
            {
                // Ensure inventory promptly closed server-side if the client didn't open the GUI
                capi.World.Player.InventoryManager.CloseInventoryAndSync(Inventory);
            }
        }

        private void Dlg_OnClosed()
        {
            dlg = null;
            var capi = Api as ICoreClientAPI;
            interactingWithPlayer.Remove(capi.World.Player.Entity);
            capi.Network.SendEntityPacket(EntityId, PlayerStoppedInteracting);
        }

        public override void OnReceivedClientPacket(IServerPlayer player, int packetid, byte[] data)
        {
            base.OnReceivedClientPacket(player, packetid, data);

            if (packetid < 1000)
            {
                Inventory.InvNetworkUtil.HandleClientPacket(player, packetid, data);
                return;
            }
            if (packetid == 1000)
            {
                EnumTransactionResult result = Inventory.TryBuySell(player);
                if (result == EnumTransactionResult.Success)
                {
                    (Api as ICoreServerAPI).WorldManager.GetChunk(Pos.AsBlockPos)?.MarkModified();

                    AnimManager.StopAnimation("idle");
                    AnimManager.StartAnimation(new AnimationMetaData() { Animation = "nod", Code = "nod", Weight = 10, EaseOutSpeed = 10000, EaseInSpeed = 10000 });

                    TreeAttribute tree = new TreeAttribute();
                    Inventory.ToTreeAttributes(tree);
                    (Api as ICoreServerAPI).Network.BroadcastEntityPacket(EntityId, 1234, tree.ToBytes());
                }
            }
            if (packetid == 1001)
            {
                player.InventoryManager.OpenInventory(Inventory);
            }

            if (packetid == PlayerStoppedInteracting)
            {
                interactingWithPlayer.Remove(player.Entity);
                if (WatchedAttributes.GetString("tradingPlayerUID") == player.PlayerUID)
                {
                    WatchedAttributes.RemoveAttribute("tradingPlayerUID");
                }
            }
        }

        public override void OnReceivedServerPacket(int packetid, byte[] data)
        {
            base.OnReceivedServerPacket(packetid, data);

            if (packetid == PlayerStoppedInteracting)
            {
                dlg?.TryClose();
                interactingWithPlayer.Remove((Api as ICoreClientAPI).World.Player.Entity);
                (Api as ICoreClientAPI).World.Player.InventoryManager.CloseInventoryAndSync(Inventory);
            }

            if (packetid == 1234)
            {
                TreeAttribute tree = new TreeAttribute();
                tree.FromBytes(data);
                Inventory.FromTreeAttributes(tree);
            }
        }

        public double NextRefreshTotalDays()
        {
            double lastRefreshTotalDays = WatchedAttributes.GetDouble("lastRefreshTotalDays", World.Calendar.TotalDays - 10);

            return doubleRefreshIntervalDays - (World.Calendar.TotalDays - lastRefreshTotalDays);
        }

        public override void OnGameTick(float dt)
        {
            base.OnGameTick(dt);

            if (World.Side == EnumAppSide.Server && TradeProps != null)
            {
                if (tickCount++ > 200)
                {
                    double lastRefreshTotalDays = WatchedAttributes.GetDouble("lastRefreshTotalDays", World.Calendar.TotalDays - 10);
                    int maxRefreshes = 10;

                    while (World.Calendar.TotalDays - lastRefreshTotalDays > doubleRefreshIntervalDays && interactingWithPlayer.Count == 0 && maxRefreshes-- > 0)
                    {
                        int traderAssets = Inventory.GetTraderAssets();
                        double giveRel = 0.07 + World.Rand.NextDouble() * 0.21;

                        float nowWealth = TradeProps.Money.nextFloat(1f, World.Rand);

                        int toGive = (int)Math.Max(-3, Math.Min(nowWealth, traderAssets + giveRel * (int)nowWealth) - traderAssets);
                        Inventory.GiveToTrader(toGive);

                        RefreshBuyingSellingInventory(0.5f);

                        lastRefreshTotalDays += doubleRefreshIntervalDays;
                        WatchedAttributes.SetDouble("lastRefreshTotalDays", lastRefreshTotalDays);

                        tickCount = 1;
                    }

                    if (maxRefreshes <= 0)
                    {
                        WatchedAttributes.SetDouble("lastRefreshTotalDays", World.Calendar.TotalDays + 1 + World.Rand.NextDouble() * 5);
                    }
                }
            }

            if (interactingWithPlayer.Count > 0)
            {
                for (int i = 0; i < interactingWithPlayer.Count; i++)
                {
                    var eplr = interactingWithPlayer[i];

                    if (!Alive || eplr.Pos.SquareDistanceTo(Pos) > 5)
                    {
                        interactingWithPlayer.Remove(eplr);
                        if (WatchedAttributes.GetString("tradingPlayerUID") == eplr.PlayerUID)
                        {
                            WatchedAttributes.RemoveAttribute("tradingPlayerUID");
                        }
                        Inventory.Close(eplr.Player);
                        i--;
                    }
                }

                if (Api is ICoreClientAPI capi && !interactingWithPlayer.Contains(capi.World.Player.Entity))
                {
                    dlg?.TryClose();
                }
            }
        }


        public override void FromBytes(BinaryReader reader, bool forClient)
        {
            base.FromBytes(reader, forClient);

            if (Inventory == null)
            {
                Inventory = new InventoryTrader("traderInv", "" + EntityId, null);
            }

            Inventory.FromTreeAttributes(GetOrCreateTradeStore());
        }

        public override void ToBytes(BinaryWriter writer, bool forClient)
        {
            Inventory.ToTreeAttributes(GetOrCreateTradeStore());

            base.ToBytes(writer, forClient);
        }


        ITreeAttribute GetOrCreateTradeStore()
        {
            if (!WatchedAttributes.HasAttribute("traderInventory"))
            {
                ITreeAttribute tree = new TreeAttribute();
                Inventory.ToTreeAttributes(tree);

                WatchedAttributes["traderInventory"] = tree;
            }

            return WatchedAttributes["traderInventory"] as ITreeAttribute;
        }
    }
}
