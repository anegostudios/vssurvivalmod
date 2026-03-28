using System;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

#nullable disable

namespace Vintagestory.GameContent
{
    public class EntityBehaviorMilkable : EntityBehavior
    {
        double lastMilkedTotalHours
        {
            get { return entity.WatchedAttributes.GetFloat("lastMilkedTotalHours"); }
            set { entity.WatchedAttributes.SetFloat("lastMilkedTotalHours", (float)value); }
        }

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

            lactatingDaysAfterBirth = attributes["lactatingDaysAfterBirth"].AsFloat(21);
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
            bhmul = entity.GetBehavior<EntityBehaviorMultiply>();

            if (entity.World.Side == EnumAppSide.Client) return;
            entity.GetBehavior<EntityBehaviorTaskAI>().TaskManager.OnShouldExecuteTask += _ => !IsBeingMilked;

            // Make sure TotalDaysLastBirth is not a future date (e.g. when exported from an old world and imported into a new world)
            bhmul?.TotalDaysLastBirth = Math.Min(bhmul.TotalDaysLastBirth, entity.World.Calendar.TotalDays);

            lastMilkedTotalHours = Math.Min(lastMilkedTotalHours, entity.World.Calendar.TotalHours);
        }

        public bool TryBeginMilking()
        {
            lastIsMilkingStateTotalMs = entity.World.ElapsedMilliseconds;

            if (!CanMilk()) return false;

            int generation = entity.WatchedAttributes.GetInt("generation");
            aggroChance = Math.Min(1 - generation / 3f, 0.95f);
            aggroTested = false;
            clientCanContinueMilking = true;

            if (entity.World.Side == EnumAppSide.Server)
            {
                AiTaskManager tmgr = entity.GetBehavior<EntityBehaviorTaskAI>().TaskManager;
                tmgr.StopTask<AiTaskWander>();
                tmgr.StopTask<AiTaskSeekEntity>();
                tmgr.StopTask<AiTaskSeekFoodAndEat>();
                tmgr.StopTask<AiTaskStayCloseToEntity>();
            }
            else if (entity.World is IClientWorldAccessor cworld)
            {
                milkSound?.Dispose();
                milkSound = cworld.LoadSound(new SoundParams
                {
                    DisposeOnFinish = true,
                    Location = new AssetLocation("sounds/creature/sheep/milking.ogg"),
                    Position = entity.Pos.XYZFloat,
                    SoundType = EnumSoundType.Sound
                });

                milkSound.Start();
            }

            return true;
        }


        protected bool CanMilk()
        {
            if (bhmul == null) return false;
            // Can not be milked when stressed (= caused by aggressive or fleeing emotion states)
            float stressLevel = entity.WatchedAttributes.GetFloat("stressLevel");
            if (stressLevel > 0.1)
            {
                (entity.Api as ICoreClientAPI)?.TriggerIngameError(this, "notready", Lang.Get("Currently too stressed to be milkable"));
                return false;
            }

            var cal = entity.World.Calendar;
            // Can only be milked for 21 days after giving birth
            if (Math.Max(0, cal.TotalDays - bhmul.TotalDaysLastBirth) >= lactatingDaysAfterBirth) return false;

            // Can only be milked once every day
            return !(cal.TotalHours - lastMilkedTotalHours < cal.HoursPerDay);
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

            if (secondsUsed > 1 && !aggroTested && entity.Api is ICoreServerAPI sapi)
            {
                aggroTested = true;
                if (entity.World.Rand.NextDouble() < aggroChance)
                {
                    entity.GetBehavior<EntityBehaviorEmotionStates>()?.TryTriggerState("aggressiveondamage", 1);
                    entity.WatchedAttributes.SetFloat("stressLevel", Math.Max(entity.WatchedAttributes.GetFloat("stressLevel"), 0.25f));

                    if (entity.Properties.Sounds.ContainsKey("hurt"))
                    {
                        entity.World.PlaySoundAt(entity.Properties.Sounds["hurt"], entity);
                    }

                    sapi.Network.SendEntityPacket(milkingPlayer as IServerPlayer, entity.EntityId, 1337);

                    return false;
                }
            }

            return true;
        }

        public void MilkingComplete(ItemSlot slot, EntityAgent byEntity)
        {
            if (slot.Itemstack?.Collectible is not BlockLiquidContainerBase lcblock) return;

            if (entity.World.Side == EnumAppSide.Server)
            {
                lastMilkedTotalHours = entity.World.Calendar.TotalHours;
                lcblock.SplitStackAndPerformAction(byEntity, slot, stack => lcblock.TryPutLiquid(stack, GetMilkStack(entity.World), yieldLitres));
            }

            milkSound?.Stop();
            milkSound?.Dispose();
        }


        public override void OnReceivedServerPacket(int packetid, byte[] data, ref EnumHandling handled)
        {
            if (packetid == 1337)
            {
                clientCanContinueMilking = false;
                (entity.Api as ICoreClientAPI)?.TriggerIngameError(this, "notready", Lang.Get("Became stressed from the milking attempt. Not milkable while stressed."));
            }
        }

        public override void GetInfoText(StringBuilder infotext)
        {
            if (!entity.Alive || bhmul == null) return;
            // Can only be milked for a specific amount of time after giving birth
            double lactatingDaysLeft = lactatingDaysAfterBirth - Math.Max(0, entity.World.Calendar.TotalDays - bhmul.TotalDaysLastBirth);

            if (lactatingDaysLeft <= 0) return;

            if (entity.World.Calendar.TotalHours - lastMilkedTotalHours >= entity.World.Calendar.HoursPerDay)
            {
                if (entity.WatchedAttributes.GetFloat("stressLevel") > 0.1)
                {
                    infotext.AppendLine(Lang.Get("Lactating for {0} days, currently too stressed to be milkable.", (int)lactatingDaysLeft));
                    return;
                }

                infotext.AppendLine(Lang.Get(entity.WatchedAttributes.GetInt("generation") switch
                {
                    0 => "Lactating for {0} days, can be milked, but will become aggressive.",
                    < 3 => "Lactating for {0} days, can be milked, but might become aggressive.",
                    _ => "Lactating for {0} days, can be milked."
                }, (int)lactatingDaysLeft));

            } else infotext.AppendLine(Lang.Get("Lactating for {0} days.", (int)lactatingDaysLeft));
        }

        public ItemStack GetMilkStack(IWorldAccessor world)
        {
            JsonItemStack liquidJsonStack = entity.Properties.Attributes["liquidStack"].AsObject(new JsonItemStack
            {
                Type = EnumItemClass.Item,
                Code = "milkportion"
            });

            liquidJsonStack.StackSize = 999999;
            liquidJsonStack.Resolve(world, "milking liquid stack");

            return liquidJsonStack.ResolvedItemStack;
        }
    }
}
