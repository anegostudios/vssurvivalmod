using System;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Vintagestory.GameContent
{
    public class EntityBehaviorMilkable : EntityBehavior
    {
        double lastMilkedTotalHours;
        
        float aggroChance;
        bool aggroTested;
        bool clientCanContinueMilking;

        EntityBehaviorMultiply bhmul;

        float lactatingDaysAfterBirth = 21;
        float yieldLitres = 10f;

        long lastIsMilkingStateTotalMs;

        ILoadedSound milkSound;

        public bool IsBeingMilked => entity.World.ElapsedMilliseconds - lastIsMilkingStateTotalMs < 1000;

        public EntityBehaviorMilkable(Entity entity) : base(entity)
        {

        }
        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            base.Initialize(properties, attributes);

            yieldLitres = attributes["yieldLitres"].AsFloat(10);
        }

        public override string PropertyName()
        {
            return "milkable";
        }

        public override void OnEntityLoaded()
        {
            init();
        }

        public override void OnEntitySpawn()
        {
            init();
        }

        void init()
        {
            lastMilkedTotalHours = entity.WatchedAttributes.GetFloat("lastMilkedTotalHours");
            if (entity.World.Side == EnumAppSide.Client) return;

            EntityBehaviorTaskAI taskAi = entity.GetBehavior<EntityBehaviorTaskAI>();
            taskAi.TaskManager.OnShouldExecuteTask += (task) => !IsBeingMilked;

            // Make sure TotalDaysLastBirth is not a future date (e.g. when exported from an old world and imported into a new world)
            bhmul = entity.GetBehavior<EntityBehaviorMultiply>();
            bhmul.TotalDaysLastBirth = Math.Min(bhmul.TotalDaysLastBirth, entity.World.Calendar.TotalDays);

            lastMilkedTotalHours = Math.Min(lastMilkedTotalHours, entity.World.Calendar.TotalHours);
        }

        public bool TryBeginMilking()
        {
            lastIsMilkingStateTotalMs = entity.World.ElapsedMilliseconds;

            bhmul = entity.GetBehavior<EntityBehaviorMultiply>();

            if (!CanMilk()) return false;

            int generation = entity.WatchedAttributes.GetInt("generation", 0);
            aggroChance = Math.Min(1 - generation / 3f, 0.95f);
            aggroTested = false;
            clientCanContinueMilking = true;


            if (entity.World.Side == EnumAppSide.Server)
            {
                AiTaskManager tmgr = entity.GetBehavior<EntityBehaviorTaskAI>().TaskManager;
                tmgr.StopTask(typeof(AiTaskWander));
                tmgr.StopTask(typeof(AiTaskSeekEntity));
                tmgr.StopTask(typeof(AiTaskSeekFoodAndEat));
                tmgr.StopTask(typeof(AiTaskStayCloseToEntity));
            }
            else
            {
                if (entity.World is IClientWorldAccessor cworld)
                {
                    milkSound?.Dispose();
                    milkSound = cworld.LoadSound(new SoundParams()
                    {
                        DisposeOnFinish = true,
                        Location = new AssetLocation("sounds/creature/sheep/milking.ogg"),
                        Position = entity.Pos.XYZFloat,
                        SoundType = EnumSoundType.Sound
                    });

                    milkSound.Start();
                }
            }

            return true;
        }


        protected bool CanMilk()
        {
            bhmul = entity.GetBehavior<EntityBehaviorMultiply>();
            // Can not be milked when stressed (= caused by aggressive or fleeing emotion states)
            float stressLevel = entity.WatchedAttributes.GetFloat("stressLevel");
            if (stressLevel > 0.1)
            {
                if (entity.World.Api is ICoreClientAPI capi)
                {
                    capi.TriggerIngameError(this, "notready", Lang.Get("Currently too stressed to be milkable"));
                }
                return false;
            }

            // Can only be milked for 21 days after giving birth
            double daysSinceBirth = Math.Max(0, entity.World.Calendar.TotalDays - bhmul.TotalDaysLastBirth);
            if (bhmul != null && daysSinceBirth >= lactatingDaysAfterBirth) return false;

            // Can only be milked once every day
            if (entity.World.Calendar.TotalHours - lastMilkedTotalHours < entity.World.Calendar.HoursPerDay) return false;

            return true;
        }

        public bool CanContinueMilking(IPlayer milkingPlayer, float secondsUsed)
        {
            if (!CanMilk()) return false;

            lastIsMilkingStateTotalMs = entity.World.ElapsedMilliseconds;
            
            if (entity.World.Side == EnumAppSide.Client)
            {
                if (!clientCanContinueMilking)
                {
                    milkSound?.Stop();
                    milkSound?.Dispose();
                } else
                {
                    milkSound.SetPosition((float)entity.Pos.X, (float)entity.Pos.Y, (float)entity.Pos.Z);
                }

                return clientCanContinueMilking;
            }

            if (secondsUsed > 1 && !aggroTested && entity.World.Side == EnumAppSide.Server)
            {
                aggroTested = true;
                if (entity.World.Rand.NextDouble() < aggroChance)
                {
                    entity.GetBehavior<EntityBehaviorEmotionStates>().TryTriggerState("aggressiveondamage", 1);
                    entity.WatchedAttributes.SetFloat("stressLevel", Math.Max(entity.WatchedAttributes.GetFloat("stressLevel"), 0.25f));

                    if (entity.Properties.Sounds.ContainsKey("hurt"))
                    {
                        entity.World.PlaySoundAt(entity.Properties.Sounds["hurt"].Clone().WithPathPrefixOnce("sounds/").WithPathAppendixOnce(".ogg"), entity);
                    }

                    (entity.Api as ICoreServerAPI).Network.SendEntityPacket(milkingPlayer as IServerPlayer, entity.EntityId, 1337);

                    if (entity.World.Api is ICoreClientAPI capi)
                    {
                        capi.TriggerIngameError(this, "notready", Lang.Get("Became stressed from the milking attempt. Not milkable while stressed."));
                    }

                    return false;
                }
            }

            

            return true;
        }

        public void MilkingComplete(ItemSlot slot, EntityAgent byEntity)
        {
            lastMilkedTotalHours = entity.World.Calendar.TotalHours;
            entity.WatchedAttributes.SetFloat("lastMilkedTotalHours", (float)lastMilkedTotalHours);

            BlockLiquidContainerBase lcblock = slot.Itemstack.Collectible as BlockLiquidContainerBase;
            if (lcblock == null)
            {
                return;
            }

            if (entity.World.Side == EnumAppSide.Server)
            {
                ItemStack contentStack = new ItemStack(byEntity.World.GetItem(new AssetLocation("milkportion")));
                contentStack.StackSize = 999999;

                if (slot.Itemstack.StackSize == 1)
                {
                    lcblock.TryPutLiquid(slot.Itemstack, contentStack, yieldLitres);
                }
                else
                {
                    ItemStack containerStack = slot.TakeOut(1);
                    lcblock.TryPutLiquid(containerStack, contentStack, yieldLitres);

                    if (!byEntity.TryGiveItemStack(containerStack))
                    {
                        byEntity.World.SpawnItemEntity(containerStack, byEntity.Pos.XYZ.Add(0, 0.5, 0));
                    }
                }

                slot.MarkDirty();
            }

            milkSound?.Stop();
            milkSound?.Dispose();            
        }


        public override void OnReceivedServerPacket(int packetid, byte[] data, ref EnumHandling handled)
        {
            if (packetid == 1337)
            {
                clientCanContinueMilking = false;
            }
        }

        public override void GetInfoText(StringBuilder infotext)
        {
            if (!entity.Alive) return;

            bhmul = entity.GetBehavior<EntityBehaviorMultiply>();
            // Can only be milked for 21 days after giving birth
            double lactatingDaysLeft = lactatingDaysAfterBirth - Math.Max(0, entity.World.Calendar.TotalDays - bhmul.TotalDaysLastBirth);

            if (bhmul != null && lactatingDaysLeft > 0)
            {
                if (entity.World.Calendar.TotalHours - lastMilkedTotalHours >= entity.World.Calendar.HoursPerDay)
                {
                    float stressLevel = entity.WatchedAttributes.GetFloat("stressLevel");
                    if (stressLevel > 0.1)
                    {
                        infotext.AppendLine(Lang.Get("Lactating for {0} days, currently too stressed to be milkable.", (int)lactatingDaysLeft));
                        return;
                    }

                    int generation = entity.WatchedAttributes.GetInt("generation", 0);
                    if (generation < 4f)
                    {
                        if (generation == 0)
                        {
                            infotext.AppendLine(Lang.Get("Lactating for {0} days, can be milked, but will become aggressive.", (int)lactatingDaysLeft));
                        } else
                        {
                            infotext.AppendLine(Lang.Get("Lactating for {0} days, can be milked, but might become aggressive.", (int)lactatingDaysLeft));
                        }
                        
                    } else
                    {
                        infotext.AppendLine(Lang.Get("Lactating for {0} days, can be milked.", (int)lactatingDaysLeft));
                    }
                    
                } else
                {
                    infotext.AppendLine(Lang.Get("Lactating for {0} days.", (int)lactatingDaysLeft));
                }
            }
        }
    }
}
