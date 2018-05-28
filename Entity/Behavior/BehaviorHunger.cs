using System;
using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;

namespace Vintagestory.GameContent
{
    public class EntityBehaviorHunger : EntityBehavior
    {
        ITreeAttribute hungerTree;
        EntityAgent entityAgent;

        float hungerCounter;
        //float lastFatReserves;
        int sprintCounter;

        long listenerId;

        internal float SaturationLossDelay
        {
            get { return hungerTree.GetFloat("saturationlossdelay"); }
            set { hungerTree.SetFloat("saturationlossdelay", value); entity.WatchedAttributes.MarkPathDirty("hunger"); }
        }

        internal float Saturation
        {
            get { return hungerTree.GetFloat("currentsaturation"); }
            set { hungerTree.SetFloat("currentsaturation", value); entity.WatchedAttributes.MarkPathDirty("hunger"); }
        }

        /*internal float FatReserves
        {
            get { return hungerTree.GetFloat("currentfatreserves"); }
            set { hungerTree.SetFloat("currentfatreserves", value); entity.WatchedAttributes.MarkPathDirty("hunger"); }
        }*/

        internal float MaxSaturation
        {
            get { return hungerTree.GetFloat("maxsaturation"); }
            set { hungerTree.SetFloat("maxsaturation", value); entity.WatchedAttributes.MarkPathDirty("hunger"); }
        }

       /* internal float MaxFatReserves
        {
            get { return hungerTree.GetFloat("maxfatreserves"); }
            set { hungerTree.SetFloat("maxfatreserves", value); entity.WatchedAttributes.MarkPathDirty("hunger"); }
        }*/

        internal float HealthLocked
        {
            get { return entity.WatchedAttributes.GetTreeAttribute("health").GetFloat("healthlocked"); }
            set { entity.WatchedAttributes.GetTreeAttribute("health").SetFloat("healthlocked", value); entity.WatchedAttributes.MarkPathDirty("health"); }
        }

        internal float Health
        {
            get { return entity.WatchedAttributes.GetTreeAttribute("health").GetFloat("currenthealth"); }
            set { entity.WatchedAttributes.GetTreeAttribute("health").SetFloat("currenthealth", value); entity.WatchedAttributes.MarkPathDirty("health"); }
        }



        public EntityBehaviorHunger(Entity entity) : base(entity)
        {
            entityAgent = entity as EntityAgent;
        }

        public override void Initialize(EntityType config, JsonObject typeAttributes)
        {
            hungerTree = entity.WatchedAttributes.GetTreeAttribute("hunger");

            if (hungerTree == null)
            {
                entity.WatchedAttributes.SetAttribute("hunger", hungerTree = new TreeAttribute());
                
                Saturation = typeAttributes["currentsaturation"].AsFloat(1200);
                MaxSaturation = typeAttributes["maxsaturation"].AsFloat(1200);
                SaturationLossDelay = typeAttributes["saturationlossdelay"].AsFloat(180);

                //FatReserves = configHungerTree["currentfatreserves"].AsFloat(1000);
                //MaxFatReserves = configHungerTree["maxfatreserves"].AsFloat(1000);
            }

            //lastFatReserves = FatReserves;

            listenerId = entity.World.RegisterGameTickListener(SlowTick, 6000);
        }



        public override void OnEntityDespawn(EntityDespawnReason despawn)
        {
            base.OnEntityDespawn(despawn);

            entity.World.UnregisterGameTickListener(listenerId);
        }

        public override void OnEntityReceiveSaturation(float saturation)
        {
            Saturation = Math.Min(MaxSaturation, Saturation + saturation);
            SaturationLossDelay = 10;
        }

        public override void OnGameTick(float deltaTime)
        {
            if (entity is EntityPlayer)
            {
                EntityPlayer plr = (EntityPlayer)entity;
                EnumGameMode mode = entity.World.PlayerByUid(plr.PlayerUID).WorldData.CurrentGameMode;
                if (mode == EnumGameMode.Creative || mode == EnumGameMode.Spectator) return;
            }

            sprintCounter += entityAgent != null && entityAgent.Controls.Sprint ? 1 : 0;

            //deltaTime *= 10;

            hungerCounter += deltaTime;


            // Once every 10s
            if (hungerCounter > 10)
            {
                if (SaturationLossDelay > 0)
                {
                    SaturationLossDelay -= 10;
                    hungerCounter -= 10;
                    return;
                }

                float prevSaturation = Saturation;

                if (prevSaturation > 0)
                {
                    Saturation = Math.Max(0, prevSaturation - 8 - sprintCounter / 15f);
                    sprintCounter = 0;
                }

                hungerCounter -= 10;

            }
        }


        private void SlowTick(float dt)
        {
            if (entity is EntityPlayer)
            {
                EntityPlayer plr = (EntityPlayer)entity;
                if (entity.World.PlayerByUid(plr.PlayerUID).WorldData.CurrentGameMode == EnumGameMode.Creative) return;
            }

            //dt *= 20;

            if (Saturation <= 0)
            {
                // Let's say a fat reserve of 1000 is depleted in 3 ingame days using the default game speed of 1/60th
                // => 72 ingame hours / 60 = 1.2 irl hours = 4320 irl seconds
                // => 1 irl seconds substracts 1/4.32 fat reserves

                //float sprintLoss = sprintCounter / (15f * 6);
                //FatReserves = Math.Max(0, FatReserves - dt / 4.32f - sprintLoss / 4.32f);

                //if (FatReserves <= 0)
                {
                    entity.ReceiveDamage(new DamageSource() { source = EnumDamageSource.Internal, type = EnumDamageType.Hunger }, 0.25f);
                }

                

                sprintCounter = 0;
            }

            /*if (Saturation >= 0.85 * MaxSaturation)
            {
                // Fat recovery is 6 times slower
                FatReserves = Math.Min(MaxFatReserves, FatReserves + dt / (6 * 4.32f));
            }

            float max = MaxFatReserves;
            float cur = FatReserves / max;

            if (cur <= 0.8 || lastFatReserves <= 0.8)
            {
                float diff = cur - lastFatReserves;
                if (Math.Abs(diff) >= 0.1)
                {
                    HealthLocked += diff > 0 ? -1 : 1;

                    if (diff > 0 || Health > 0)
                    {
                        entity.ReceiveDamage(new DamageSource() { source = EnumDamageSource.Internal, type = (diff > 0) ? EnumDamageType.Heal : EnumDamageType.Hunger }, 1);
                    }

                    lastFatReserves = cur;
                }
            } else
            {
                lastFatReserves = cur;
            } */     
        }

        public override string PropertyName()
        {
            return "hunger";
        }

        public override void OnEntityReceiveDamage(DamageSource damageSource, float damage)
        {
            if (damageSource.type == EnumDamageType.Heal && damageSource.source == EnumDamageSource.Respawn)
            {
                SaturationLossDelay = 60;
                Saturation = MaxSaturation / 2;
            }
        }
    }
    
}
