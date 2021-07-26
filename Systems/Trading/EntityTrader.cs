using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API;
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

    public class EntityTrader : EntityHumanoid
    {
        public static OrderedDictionary<string, TraderPersonality> Personalities = new OrderedDictionary<string, TraderPersonality>()
        {
            { "formal", new TraderPersonality(1, 1, 0.9f) },
            { "balanced", new TraderPersonality(1.2f, 0.9f, 1.1f) },
            { "lazy", new TraderPersonality(1.65f, 0.7f, 0.9f) },
            { "rowdy", new TraderPersonality(0.75f, 1f, 1.8f) },
        };

        public InventoryTrader Inventory;
        public TradeProperties TradeProps;

        
        public EntityPlayer tradingWith;
        GuiDialog dlg;

        public EntityTalkUtil talkUtil;

        public string Personality
        {
            get { return WatchedAttributes.GetString("personality", "formal"); }
            set {
                WatchedAttributes.SetString("personality", value);
                talkUtil?.SetModifiers(Personalities[value].TalkSpeedModifier, Personalities[value].PitchModifier, Personalities[value].VolumneModifier);
            }
        }

        public string[] OutfitCodes
        {
            get { return (WatchedAttributes["outfitcodes"] as StringArrayAttribute)?.value; }
            set {
                if (value == null) WatchedAttributes.RemoveAttribute("outfitcodes");
                WatchedAttributes["outfitcodes"] = new StringArrayAttribute(value); 
            }
        }


        public EntityTrader()
        {
            AllowDespawn = false;
            AnimManager = new TraderAnimationManager();
        }

        public override void Initialize(EntityProperties properties, ICoreAPI api, long InChunkIndex3d)
        {
            base.Initialize(properties, api, InChunkIndex3d);

            if (Inventory == null)
            {
                Inventory = new InventoryTrader("traderInv", "" + EntityId, api);
            }

            if (api.Side == EnumAppSide.Server)
            {
                try
                {
                    string json = Properties.Server.Attributes["tradeProps"].ToJsonToken();
                    TradeProps = new JsonObject(json).AsObject<TradeProperties>();
                } catch (Exception e)
                {
                    api.World.Logger.Error("Failed deserializing TradeProperties for trader {0}, exception logged to verbose debug", properties.Code);
                    api.World.Logger.VerboseDebug("Failed deserializing TradeProperties: {0}", e);
                    api.World.Logger.VerboseDebug("=================");
                    api.World.Logger.VerboseDebug("Tradeprops json:");
                    api.World.Logger.VerboseDebug("{0}", Properties.Server.Attributes["tradeProps"].ToJsonToken());
                }

                if (OutfitCodes == null)
                {
                    OutfitCodes = api.ModLoader.GetModSystem<TraderOutfits>().GetRandomOutfit();
                }
                
            } else
            {
                talkUtil = new EntityTalkUtil(api as ICoreClientAPI, this);

            }
            
            try
            {
                Inventory.LateInitialize("traderInv-" + EntityId, api, this);
            } catch (Exception e)
            {
                api.World.Logger.Error("Failed initializing trader inventory. Will recreate. Exception logged to verbose debug");
                api.World.Logger.VerboseDebug("Failed initializing trader inventory. Will recreate. Exception {0}", e);

                WatchedAttributes.RemoveAttribute("traderInventory");
                Inventory = new InventoryTrader("traderInv", "" + EntityId, api);
                Inventory.LateInitialize("traderInv-" + EntityId, api, this);

                RefreshBuyingSellingInventory();
            }

            (AnimManager as TraderAnimationManager).Personality = this.Personality;
            this.Personality = this.Personality; // to update the talkutil
        }


        public override void OnEntitySpawn()
        {
            base.OnEntitySpawn();

            if (World.Api.Side == EnumAppSide.Server)
            {
                EntityBehaviorTaskAI taskAi = GetBehavior<EntityBehaviorTaskAI>();

                taskAi.taskManager.ShouldExecuteTask =
                    (task) => tradingWith == null || (task is AiTaskIdle || task is AiTaskSeekEntity || task is AiTaskGotoEntity);

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
                (AnimManager as TraderAnimationManager).Personality = this.Personality;
            }
        }

        public override void OnEntityLoaded()
        {
            base.OnEntityLoaded();

            if (Api.Side == EnumAppSide.Server)
            {
                EntityBehaviorTaskAI taskAi = GetBehavior<EntityBehaviorTaskAI>();
                taskAi.taskManager.ShouldExecuteTask =
                    (task) => tradingWith == null || (task is AiTaskIdle || task is AiTaskSeekEntity || task is AiTaskGotoEntity);
            }
        }


        public override void OnTesselation(ref Shape entityShape, string shapePathForLogging)
        {
            base.OnTesselation(ref entityShape, shapePathForLogging);

            // Make a copy so we don't mess up the original
            Shape newShape = entityShape.Clone();
            newShape.ResolveAndLoadJoints("head");
            entityShape = newShape;

            string[] outfitCodes = OutfitCodes;
            TexturedWeightedCompositeShape[] cshapes = Api.ModLoader.GetModSystem<TraderOutfits>().Outfit2Shapes(OutfitCodes);

            for (int i = 0; i < outfitCodes.Length; i++)
            {
                if (cshapes[i].Base == null) continue;

                addGearToShape(outfitCodes[i], cshapes[i], entityShape, shapePathForLogging, null, cshapes[i].Textures);
            }
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
                item.Resolve(World, "tradeItem resolver");
                
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
                item.Resolve(World, "tradeItem resolver");

                bool alreadySelling = buyingSlots.Any((slot) => slot?.Itemstack != null && slot.TradeItem.Stock > 0 && item.ResolvedItemstack?.Equals(World, slot.Itemstack, ignoredAttributes) == true);

                if (!alreadySelling)
                {
                    newBuyItems.Push(item);
                }
            }
            #endregion

            replaceTradeItems(newBuyItems, buyingSlots, buyingQuantity, refreshChance);
            replaceTradeItems(newsellItems, sellingSlots, sellingQuantity, refreshChance);

            ITreeAttribute tree = GetOrCreateTradeStore();
            Inventory.ToTreeAttributes(tree);
            WatchedAttributes.MarkAllDirty();
        }

        private void replaceTradeItems(Stack<TradeItem> newItems, ItemSlotTrade[] slots, int quantity, float refreshChance)
        {
            HashSet<int> refreshedSlots = new HashSet<int>();

            for (int i = 0; i < quantity; i++)
            {
                if (World.Rand.NextDouble() > refreshChance) continue;
                if (newItems.Count == 0) break;

                TradeItem newTradeItem = newItems.Pop();

                int slotIndex = slots.IndexOf((bslot) => bslot.Itemstack != null && bslot.TradeItem.Stock == 0 && newTradeItem.ResolvedItemstack.Equals(World, bslot.Itemstack, GlobalConstants.IgnoredStackAttributes));

                ItemSlotTrade intoSlot;

                // The trader already sells this but is out of stock - replace
                if (slotIndex != -1)
                {
                    intoSlot = slots[slotIndex];
                    refreshedSlots.Add(slotIndex);
                }
                else
                {
                    while (refreshedSlots.Contains(i)) i++;
                    if (i >= slots.Length) break;
                    intoSlot = slots[i];
                    refreshedSlots.Add(i);
                }

                //if (newTradeItem.Name == null) newTradeItem.Name = i + "";

                intoSlot.SetTradeItem(newTradeItem.Resolve(World));
                intoSlot.MarkDirty();
            }
        }

        public override void OnInteract(EntityAgent byEntity, ItemSlot slot, Vec3d hitPosition, EnumInteractMode mode)
        {
            if (mode != EnumInteractMode.Interact || !(byEntity is EntityPlayer))
            {
                base.OnInteract(byEntity, slot, hitPosition, mode);
                return;
            }

            if (!Alive) return;

            EntityPlayer entityplr = byEntity as EntityPlayer;
            IPlayer player = World.PlayerByUid(entityplr.PlayerUID);
            player.InventoryManager.OpenInventory(Inventory);

            tradingWith = entityplr;


            if (World.Side == EnumAppSide.Client)
            {
                if (tradingWith.Pos.SquareDistanceTo(this.Pos) <= 5 && dlg?.IsOpened() != true)
                {
                    dlg = new GuiDialogTrader(Inventory, this, World.Api as ICoreClientAPI);
                    dlg.TryOpen();
                }
                else
                {
                    // Ensure inventory promptly closed server-side if the client didn't open the GUI
                    ICoreClientAPI capi = (ICoreClientAPI)Api;
                    capi.Network.SendPacketClient(capi.World.Player.InventoryManager.CloseInventory(Inventory));
                }

                talkUtil.Talk(EnumTalkType.Meet);
            }

            if (World.Side == EnumAppSide.Server)
            {
                // Make the trader walk towards the player
                AiTaskManager tmgr = GetBehavior<EntityBehaviorTaskAI>().taskManager;
                tmgr.StopTask(typeof(AiTaskWander));

                AiTaskGotoEntity task = new AiTaskGotoEntity(this, entityplr);
                if (task.TargetReached())
                {
                    tmgr.ExecuteTask(new AiTaskLookAtEntity(this, entityplr), 1);
                }
                else
                {
                    tmgr.ExecuteTask(task, 1);
                }

                AnimManager.StartAnimation(new AnimationMetaData() { Animation = "welcome", Code = "welcome", Weight = 10, EaseOutSpeed = 10000, EaseInSpeed = 10000 });
                AnimManager.StopAnimation("idle");
            }
        }




        public override void OnReceivedClientPacket(IServerPlayer player, int packetid, byte[] data)
        {
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
        }

        public override void OnReceivedServerPacket(int packetid, byte[] data)
        {
            base.OnReceivedServerPacket(packetid, data);

            if (packetid == 1001)
            {
                if (!Alive) return;
                talkUtil.Talk(EnumTalkType.Hurt);
            }
            if (packetid == 1002)
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

                    while (World.Calendar.TotalDays - lastRefreshTotalDays > doubleRefreshIntervalDays && tradingWith == null && maxRefreshes-- > 0)
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

            if (tradingWith != null && (tradingWith.Pos.SquareDistanceTo(this.Pos) > 5 || Inventory.openedByPlayerGUIds.Count == 0 || !Alive))
            {
                dlg?.TryClose();
                IPlayer tradingPlayer = tradingWith?.Player;
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

            (AnimManager as TraderAnimationManager).Personality = this.Personality;
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
                (World.Api as ICoreServerAPI).Network.BroadcastEntityPacket(this.EntityId, 1001);
                return;
            }
            if (type == "death" && World.Side == EnumAppSide.Server)
            {
                (World.Api as ICoreServerAPI).Network.BroadcastEntityPacket(this.EntityId, 1002);
                return;
            }

            base.PlayEntitySound(type, dualCallByPlayer, randomizePitch, range);
        }

        

    }

}
