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
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public enum EnumTalkType
    {
        Meet,
        Idle,
        Hurt,
        Complain,
        Goodbye
    }

    public class TalkUtil
    {
        int lettersLeftToTalk = 0;
        int totalLettersToTalk = 0;

        int currentLetterInWord = 0;
        int totalLettersTalked = 0;


        float chordDelay = 0f;

        EnumTalkType talkType;

        ICoreClientAPI capi;
        Entity entity;

        AssetLocation soundName = new AssetLocation("sounds/instrument/saxophone");

        List<KeyValuePair<ILoadedSound, float>> slidingPitchSounds = new List<KeyValuePair<ILoadedSound, float>>();


        Dictionary<EnumTalkType, float> TalkSpeed = new Dictionary<EnumTalkType, float>()
        {
            { EnumTalkType.Meet, 0.13f },
            { EnumTalkType.Idle, 0.2f },
            { EnumTalkType.Hurt, 0.07f },
            { EnumTalkType.Goodbye, 0.07f },
            { EnumTalkType.Complain, 0.09f },
        };


        public TalkUtil(ICoreClientAPI capi, Entity atEntity)
        {
            this.capi = capi;
            this.entity = atEntity;
        }

        public Random Rand { get { return capi.World.Rand; } }

        public void OnGameTick(float dt)
        {
            if (lettersLeftToTalk > 0)
            {
                chordDelay -= dt;

                if (chordDelay < 0)
                {
                    chordDelay = TalkSpeed[talkType];

                    switch (talkType)
                    {
                        case EnumTalkType.Goodbye:
                            PlaySound(1.25f - 0.6f * (float)totalLettersTalked / totalLettersToTalk, 0.25f);
                            chordDelay = 0.25f;
                            break;

                        case EnumTalkType.Meet:
                            PlaySound(0.75f + 0.5f * (float)Rand.NextDouble() + (float)totalLettersTalked / totalLettersToTalk / 3, 0.25f);

                            if (currentLetterInWord > 1 && capi.World.Rand.NextDouble() < 0.35)
                            {
                                chordDelay = 0.45f;
                                currentLetterInWord = 0;
                            }
                            break;

                        case EnumTalkType.Complain:
                            PlaySound(0.75f + 0.5f * (float)Rand.NextDouble(), 0.25f);
                            
                            if (currentLetterInWord > 1 && capi.World.Rand.NextDouble() < 0.35)
                            {
                                chordDelay = 0.45f;
                                currentLetterInWord = 0;
                            }

                            break;

                        case EnumTalkType.Idle:

                            PlaySound(0.75f + 0.25f * (float)Rand.NextDouble(), 0.1f);
                            
                            if (currentLetterInWord > 1 && capi.World.Rand.NextDouble() < 0.35)
                            {
                                chordDelay = 0.55f;
                                currentLetterInWord = 0;
                            }


                            break;

                        case EnumTalkType.Hurt:
                            float pitch = 0.75f + 0.5f * (float)Rand.NextDouble() + (1 - (float)totalLettersTalked / totalLettersToTalk);
                            
                            PlaySound(pitch, 0.25f + (1 - (float)totalLettersTalked / totalLettersToTalk) / 2);
                            
                            if (currentLetterInWord > 1 && capi.World.Rand.NextDouble() < 0.35)
                            {
                                chordDelay = 0.25f;
                                currentLetterInWord = 0;
                            }

                            break;
                    }
                    

                    lettersLeftToTalk--;
                    currentLetterInWord++;
                    totalLettersTalked++;
                }

                return;
            }



            if (lettersLeftToTalk == 0 && capi.World.Rand.NextDouble() < 0.00005)
            {
                Talk(EnumTalkType.Idle);
            }
        }


        public void PlaySound(float startpitch, float volume)
        {
            PlaySound(startpitch, startpitch, volume);
        }

        public void PlaySound(float startPitch, float endPitch, float volume)
        {
            SoundParams param = new SoundParams()
            {
                Location = soundName,
                DisposeOnFinish = true,
                Pitch = startPitch,
                Volume = volume,
                Position = entity.Pos.XYZ.ToVec3f().Add(0, (float)entity.EyeHeight, 0),
                ShouldLoop = false,
                Range = 8,
            };

            if (startPitch != endPitch)
            {
                //slidingPitchSounds.Add(new KeyValuePair<ILoadedSound, float>());
            }


            ILoadedSound sound = capi.World.LoadSound(param);
            sound.Start();
        }


        public void Talk(EnumTalkType talkType)
        {
            IClientWorldAccessor world = capi.World as IClientWorldAccessor;

            this.talkType = talkType;
            totalLettersTalked = 0;
            currentLetterInWord = 0;

            chordDelay = TalkSpeed[talkType];

            if (talkType == EnumTalkType.Meet)
            {
                lettersLeftToTalk = 2 + world.Rand.Next(10);
            }

            if (talkType == EnumTalkType.Hurt)
            {
                lettersLeftToTalk = 3 + world.Rand.Next(6);
            }

            if (talkType == EnumTalkType.Idle)
            {
                lettersLeftToTalk = 3 + world.Rand.Next(12);
            }

            if (talkType == EnumTalkType.Complain)
            {
                lettersLeftToTalk = 3 + world.Rand.Next(5);
            }

            if (talkType == EnumTalkType.Goodbye)
            {
                lettersLeftToTalk = 2 + world.Rand.Next(2);
            }

            totalLettersToTalk = lettersLeftToTalk;
        }
    }

    public class EntityTrader : EntityHumanoid
    {
        public InventoryTrader Inventory;
        public TradeProperties TradeProps;

        
        EntityPlayer tradingWith;
        GuiDialog dlg;

        public TalkUtil talkUtil;


        public EntityTrader()
        {
            
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
                    api.World.Logger.Error("Failed deserializing TradeProperties, exception logged to verbose debug");
                    api.World.Logger.VerboseDebug("Failed deserializing TradeProperties: " + e);
                }
                
            } else
            {
                talkUtil = new TalkUtil(api as ICoreClientAPI, this);
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
            

            
        }

        

        public override void OnEntitySpawn()
        {
            base.OnEntitySpawn();

            GetBehavior<EntityBehaviorTaskAI>().taskManager.ShouldExecuteTask =
                (task) => tradingWith == null || (task is AiTaskIdle || task is AiTaskSeekEntity || task is AiTaskGotoEntity);


            if (World.Api.Side == EnumAppSide.Server)
            {
                RefreshBuyingSellingInventory();

                Inventory.GiveToTrader((int)TradeProps.Money.nextFloat(1f, World.Rand));
            }
        }

        private void RefreshBuyingSellingInventory()
        {
            if (TradeProps == null) return;

            TradeProps.Buying.List.Shuffle(World.Rand);
            int quantity = Math.Min(TradeProps.Buying.List.Length, TradeProps.Buying.MaxItems);

            for (int i = 0; i < quantity; i++)
            {
                ItemSlotTrade slot = Inventory.GetBuyingSlot(i);
                TradeItem tradeItem = TradeProps.Buying.List[i];
                if (tradeItem.Name == null) tradeItem.Name = i + "";

                slot.SetTradeItem(tradeItem.Resolve(World));
                slot.MarkDirty();
            }


            TradeProps.Selling.List.Shuffle(World.Rand);
            quantity = Math.Min(TradeProps.Selling.List.Length, TradeProps.Selling.MaxItems);

            //Console.WriteLine("==================");
            //Console.WriteLine("resolving for " + EntityId + ", total items: " +TradeConf.Selling.List.Length + ", on side " + api.Side);


            for (int i = 0; i < quantity; i++)
            {
                ItemSlotTrade slot = Inventory.GetSellingSlot(i);
                TradeItem tradeItem = TradeProps.Selling.List[i];
                if (tradeItem.Name == null) tradeItem.Name = i + "";

                slot.SetTradeItem(tradeItem.Resolve(World));
                slot.MarkDirty();
                //Console.WriteLine("resolved to: " + slot.Itemstack);
            }


            ITreeAttribute tree = GetOrCreateTradeStore();
            Inventory.ToTreeAttributes(tree);
        }


        public override void OnInteract(EntityAgent byEntity, IItemSlot slot, Vec3d hitPosition, int mode)
        {
            if (mode != 1 || !(byEntity is EntityPlayer))
            {
                base.OnInteract(byEntity, slot, hitPosition, mode);
                return;
            }

            EntityPlayer entityplr = byEntity as EntityPlayer;
            IPlayer player = World.PlayerByUid(entityplr.PlayerUID);
            player.InventoryManager.OpenInventory(Inventory);

            tradingWith = entityplr;

            
            if (World.Side == EnumAppSide.Client)
            {
                if (tradingWith.Pos.SquareDistanceTo(this.Pos) <= 5)
                {
                    dlg = new GuiDialogTrader(Inventory, this, World.Api as ICoreClientAPI);
                    dlg.TryOpen();
                    dlg.OnClosed += () => tradingWith = null;
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
                } else
                {
                    tmgr.ExecuteTask(task, 1);
                }

                
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
                Inventory.TryBuySell(player);
            }
        }

        public override void OnReceivedServerPacket(int packetid, byte[] data)
        {
            base.OnReceivedServerPacket(packetid, data);

            if (packetid == 1001)
            {
                talkUtil.Talk(EnumTalkType.Hurt);
            }
        }


        public override void OnGameTick(float dt)
        {
            base.OnGameTick(dt);

            if (World.Side == EnumAppSide.Client) {
                talkUtil.OnGameTick(dt);
            }

            if (tradingWith != null && (tradingWith.Pos.SquareDistanceTo(this.Pos) > 5 || Inventory.openedByPlayerGUIds.Count == 0))
            {
                dlg?.TryClose();
                tradingWith = null;
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
            base.ToBytes(writer, forClient);

            Inventory.ToTreeAttributes(GetOrCreateTradeStore());
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

            base.PlayEntitySound(type, dualCallByPlayer, randomizePitch, range);
        }
    }


    public class RestockOpts
    {
        public float Quantity = 1;
        public float HourDelay = 24;
    }

    public class SupplyDemandOpts
    {
        public float PriceChangePerPurchase = 0.1f;
        public float PriceChangePerDay = -0.1f;
    }

    public class TradeItem : JsonItemStack
    {
        public string Name;
        public NatFloat Price;
        public NatFloat Stock;
        public RestockOpts Restock = new RestockOpts()
        {
            HourDelay = 24,
            Quantity = 1
        };
        public SupplyDemandOpts SupplyDemand = new SupplyDemandOpts()
        {
            PriceChangePerDay = 0.1f,
            PriceChangePerPurchase = 0.1f
        };

        public ResolvedTradeItem Resolve(IWorldAccessor world)
        {
            this.Resolve(world, "TradeItem");

            return new ResolvedTradeItem()
            {
                Stack = this.ResolvedItemstack,
                Name = Name,
                Price = (int)Math.Round(Price.nextFloat(1f, world.Rand)),
                Stock = Stock == null ? 0 : (int)Math.Round(Stock.nextFloat(1f, world.Rand)),
                Restock = Restock,
                SupplyDemand = SupplyDemand
            };
        }
    }

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

    public class TradeList
    {
        public int MaxItems;
        public TradeItem[] List;
    }

    public class TradeProperties
    {
        public NatFloat Money;
        public TradeList Buying;
        public TradeList Selling;
    }
}
