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

namespace Vintagestory.GameContent
{
    public enum EnumTradeDirection
    {
        Buy, Sell
    }

    public interface ITalkUtil
    {
        EntityTalkUtil TalkUtil { get; }
    }

    public class EntityTrader : EntityDressedHumanoid, ITalkUtil
    {
        public static OrderedDictionary<string, TraderPersonality> Personalities = new OrderedDictionary<string, TraderPersonality>()
        {
            { "formal", new TraderPersonality(1 * 1.5f, 1, 0.9f) },
            { "balanced", new TraderPersonality(1.2f * 1.5f, 0.9f, 1.1f) },
            { "lazy", new TraderPersonality(1.65f * 1.5f, 0.7f, 0.9f) },
            { "rowdy", new TraderPersonality(0.75f * 1.5f, 1f, 1.8f) },
        };

        public InventoryTrader Inventory;
        public TradeProperties TradeProps;


        public EntityPlayer tradingWithPlayer;
        GuiDialog dlg;

        public EntityTalkUtil talkUtil;
        EntityBehaviorConversable ConversableBh => GetBehavior<EntityBehaviorConversable>();

        public string Personality
        {
            get { return WatchedAttributes.GetString("personality", "formal"); }
            set {
                WatchedAttributes.SetString("personality", value);
                talkUtil?.SetModifiers(Personalities[value].ChordDelayMul, Personalities[value].PitchModifier, Personalities[value].VolumneModifier);
            }
        }


        public EntityTalkUtil TalkUtil => talkUtil;

        public EntityTrader()
        {
            AnimManager = new PersonalizedAnimationManager();
        }

        public override void Initialize(EntityProperties properties, ICoreAPI api, long InChunkIndex3d)
        {
            base.Initialize(properties, api, InChunkIndex3d);
            var bh = GetBehavior<EntityBehaviorConversable>();
            if (bh != null)
            {
                bh.onControllerCreated += (controller) =>
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
                try
                {
                    TradeProps = Properties.Attributes["tradeProps"].AsObject<TradeProperties>();
                } catch (Exception e)
                {
                    api.World.Logger.Error("Failed deserializing TradeProperties for trader {0}, exception logged to verbose debug", properties.Code);
                    api.World.Logger.Error(e);
                    api.World.Logger.VerboseDebug("Failed deserializing TradeProperties:");
                    api.World.Logger.VerboseDebug("=================");
                    api.World.Logger.VerboseDebug("Tradeprops json:");
                    api.World.Logger.VerboseDebug("{0}", Properties.Server.Attributes["tradeProps"].ToJsonToken());
                }


            } else
            {
                talkUtil = new EntityTalkUtil(api as ICoreClientAPI, this, false);
            }

            try
            {
                Inventory.LateInitialize("traderInv-" + EntityId, api, this);
            } catch (Exception e)
            {
                api.World.Logger.Error("Failed initializing trader inventory. Will recreate. Exception logged to verbose debug");
                api.World.Logger.Error(e);
                api.World.Logger.VerboseDebug("Failed initializing trader inventory. Will recreate.");

                WatchedAttributes.RemoveAttribute("traderInventory");
                Inventory = new InventoryTrader("traderInv", "" + EntityId, api);
                Inventory.LateInitialize("traderInv-" + EntityId, api, this);

                RefreshBuyingSellingInventory();
            }

            (AnimManager as PersonalizedAnimationManager).Personality = this.Personality;
            this.Personality = this.Personality; // to update the talkutil
        }


        public override void OnEntitySpawn()
        {
            base.OnEntitySpawn();

            if (World.Api.Side == EnumAppSide.Server)
            {
                setupTaskBlocker();

                if (TradeProps != null)
                {
                    RefreshBuyingSellingInventory();
                    WatchedAttributes.SetDouble("lastRefreshTotalDays", World.Calendar.TotalDays - World.Rand.NextDouble() * 6);
                    Inventory.GiveToTrader((int)TradeProps.Money.nextFloat(1f, World.Rand));
                } else
                {
                    World.Logger.Warning("Trader TradeProps not set during trader entity spawn. Won't have any items for sale/purchase.");
                }

                Personality = Personalities.GetKeyAtIndex(World.Rand.Next(Personalities.Count));
                (AnimManager as PersonalizedAnimationManager).Personality = this.Personality;
            }
        }

        public override void OnEntityLoaded()
        {
            base.OnEntityLoaded();

            if (Api.Side == EnumAppSide.Server)
            {
                setupTaskBlocker();
            }
        }

        void setupTaskBlocker()
        {
            EntityBehaviorTaskAI taskAi = GetBehavior<EntityBehaviorTaskAI>();

            taskAi.TaskManager.OnShouldExecuteTask +=
               (task) => (ConversableBh == null || ConversableBh.ControllerByPlayer.Count == 0 || (task is AiTaskIdle || task is AiTaskSeekEntity || task is AiTaskGotoEntity)) && tradingWithPlayer == null;
        }

        private void RefreshBuyingSellingInventory(float refreshChance = 1.1f)
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

        private void replaceTradeItems(Stack<TradeItem> newItems, ItemSlotTrade[] slots, int quantity, float refreshChance, EnumTradeDirection tradeDir)
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

        public override void OnInteract(EntityAgent byEntity, ItemSlot slot, Vec3d hitPosition, EnumInteractMode mode)
        {
            if (ConversableBh != null)
            {
                ConversableBh.GetOrCreateController(byEntity as EntityPlayer);
            }

            base.OnInteract(byEntity, slot, hitPosition, mode);
        }

        private int Dialog_DialogTriggers(EntityAgent triggeringEntity, string value, JsonObject data)
        {
            if (value == "opentrade")
            {
                ConversableBh.Dialog?.TryClose();
                TryOpenTradeDialog(triggeringEntity);
                tradingWithPlayer = triggeringEntity as EntityPlayer;
                return 0;
            }

            return -1;
        }

        void TryOpenTradeDialog(EntityAgent forEntity)
        {
            if (!Alive) return;
            if (World.Side == EnumAppSide.Client)
            {
                EntityPlayer entityplr = forEntity as EntityPlayer;
                IPlayer player = World.PlayerByUid(entityplr.PlayerUID);

                ICoreClientAPI capi = (ICoreClientAPI)Api;

                if (forEntity.Pos.SquareDistanceTo(this.Pos) <= 5 && dlg?.IsOpened() != true)
                {
                    // Will break all kinds of things if we allow multiple concurrent of these dialogs
                    if (capi.Gui.OpenedGuis.FirstOrDefault(dlg => dlg is GuiDialogTrader && dlg.IsOpened()) == null)
                    {
                        capi.Network.SendEntityPacket(this.EntityId, 1001);
                        player.InventoryManager.OpenInventory(Inventory);

                        dlg = new GuiDialogTrader(Inventory, this, World.Api as ICoreClientAPI);
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
                    capi.Network.SendPacketClient(capi.World.Player.InventoryManager.CloseInventory(Inventory));
                }
            }
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
                    (Api as ICoreServerAPI).WorldManager.GetChunk(ServerPos.AsBlockPos)?.MarkModified();

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
        }

        public override void OnReceivedServerPacket(int packetid, byte[] data)
        {
            base.OnReceivedServerPacket(packetid, data);

            if (packetid == (int)EntityServerPacketId.Hurt)
            {
                if (!Alive) return;
                talkUtil.Talk(EnumTalkType.Hurt);
            }
            if (packetid == (int)EntityServerPacketId.Death)
            {
                talkUtil.Talk(EnumTalkType.Death);
            }
            if (packetid == 1234)
            {
                TreeAttribute tree = new TreeAttribute();
                tree.FromBytes(data);
                Inventory.FromTreeAttributes(tree);
            }
        }



        int tickCount = 0;


        protected double doubleRefreshIntervalDays = 7;

        public double NextRefreshTotalDays()
        {
            double lastRefreshTotalDays = WatchedAttributes.GetDouble("lastRefreshTotalDays", World.Calendar.TotalDays - 10);

            return doubleRefreshIntervalDays - (World.Calendar.TotalDays - lastRefreshTotalDays);
        }

        public override void OnGameTick(float dt)
        {
            base.OnGameTick(dt);


            if (Alive && AnimManager.ActiveAnimationsByAnimCode.Count == 0)
            {
                AnimManager.StartAnimation(new AnimationMetaData() { Code = "idle", Animation = "idle", EaseOutSpeed = 10000, EaseInSpeed = 10000 });
            }

            if (World.Side == EnumAppSide.Client) {
                talkUtil.OnGameTick(dt);
            } else
            {
                if (tickCount++ > 200)
                {
                    double lastRefreshTotalDays = WatchedAttributes.GetDouble("lastRefreshTotalDays", World.Calendar.TotalDays - 10);
                    int maxRefreshes = 10;

                    while (World.Calendar.TotalDays - lastRefreshTotalDays > doubleRefreshIntervalDays && tradingWithPlayer == null && maxRefreshes-- > 0)
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

            if (tradingWithPlayer != null && (tradingWithPlayer.Pos.SquareDistanceTo(Pos) > 5 || Inventory.openedByPlayerGUIds.Count == 0 || !Alive))
            {
                dlg?.TryClose();
                IPlayer tradingPlayer = tradingWithPlayer?.Player;
                if (tradingPlayer != null) Inventory.Close(tradingPlayer);
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

            (AnimManager as PersonalizedAnimationManager).Personality = this.Personality;
        }

        public override void ToBytes(BinaryWriter writer, bool forClient)
        {
            Inventory.ToTreeAttributes(GetOrCreateTradeStore());

            base.ToBytes(writer, forClient);
        }


        public override void Revive()
        {
            base.Revive();

            if (Attributes.HasAttribute("spawnX"))
            {
                ServerPos.X = Attributes.GetDouble("spawnX");
                ServerPos.Y = Attributes.GetDouble("spawnY");
                ServerPos.Z = Attributes.GetDouble("spawnZ");
            }
        }

        public override void Die(EnumDespawnReason reason = EnumDespawnReason.Death, DamageSource damageSourceForDeath = null)
        {
            base.Die(reason, damageSourceForDeath);
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

        public override void PlayEntitySound(string type, IPlayer dualCallByPlayer = null, bool randomizePitch = true, float range = 24)
        {
            if (type == "hurt" && World.Side == EnumAppSide.Server)
            {
                (World.Api as ICoreServerAPI).Network.BroadcastEntityPacket(this.EntityId, (int)EntityServerPacketId.Hurt);
                return;
            }
            if (type == "death" && World.Side == EnumAppSide.Server)
            {
                (World.Api as ICoreServerAPI).Network.BroadcastEntityPacket(this.EntityId, (int)EntityServerPacketId.Death);
                return;
            }

            base.PlayEntitySound(type, dualCallByPlayer, randomizePitch, range);
        }



    }

}
