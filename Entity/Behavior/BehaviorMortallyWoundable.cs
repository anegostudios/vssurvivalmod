using System;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent
{
    public enum EnumEntityHealthState
    {
        Normal,
        Recovering,
        MortallyWounded,
        Dead
    }

    public class EntityBehaviorMortallyWoundable : EntityBehavior
    {
        float remainAliveHours;

        float damageRequiredForFullDeath;
        float healingRequiredForRescue;

        float whenBelowHealth;

        EntityBehaviorHealth ebh;


        public EnumEntityHealthState HealthState
        {
            get { return (EnumEntityHealthState)entity.WatchedAttributes.GetInt("healthState"); }
            set { entity.WatchedAttributes.SetInt("healthState", (int)value); }
        }

        public double MortallyWoundedTotalHours
        {
            get { return entity.WatchedAttributes.GetDouble("mortallyWoundedTotalHours"); }
            set { entity.WatchedAttributes.SetDouble("mortallyWoundedTotalHours", value); }
        }

        public double HealthHealed
        {
            get { return entity.WatchedAttributes.GetDouble("healthHealed"); }
            set { entity.WatchedAttributes.SetDouble("healthHealed", value); }
        }

        public double HealthDamaged
        {
            get { return entity.WatchedAttributes.GetDouble("healthDamaged"); }
            set { entity.WatchedAttributes.SetDouble("healthDamaged", value); }
        }


        public EntityBehaviorMortallyWoundable(Entity entity) : base(entity)
        {
            if (!(entity is EntityAgent)) throw new InvalidOperationException("MortallyWoundable behavior is only possible on entities deriving from EntityAgent");
            (entity as EntityAgent).AllowDespawn = false;
        }

        public override void Initialize(EntityProperties properties, JsonObject typeAttributes)
        {
            this.remainAliveHours = (float)typeAttributes["remainAliveHours"].AsFloat(24);
            this.damageRequiredForFullDeath = (float)typeAttributes["remainingHealth"].AsFloat(10);
            this.healingRequiredForRescue = (float)typeAttributes["healingRequiredForRescue"].AsFloat(15);
            whenBelowHealth = (float)typeAttributes["whenBelowHealth"].AsFloat(5);

            entity.AnimManager.OnStartAnimation += AnimManager_OnStartAnimation;
            entity.AnimManager.OnAnimationReceived += AnimManager_OnAnimationReceived;
        }


        public override void AfterInitialized(bool onFirstSpawn)
        {
            if (entity.World.Side == EnumAppSide.Server)
            {
                ebh = entity.GetBehavior<EntityBehaviorHealth>();

                EntityBehaviorTaskAI taskAi = entity.GetBehavior<EntityBehaviorTaskAI>();

                taskAi.TaskManager.OnShouldExecuteTask += (t) => HealthState != EnumEntityHealthState.MortallyWounded && HealthState != EnumEntityHealthState.Recovering;

                if (HealthState == EnumEntityHealthState.MortallyWounded)
                {
                    setMortallyWounded();
                }
            }

            var seatable = entity.GetBehavior<EntityBehaviorSeatable>();
            if (seatable != null) seatable.CanSit += EntityBehaviorMortallyWoundable_CanSit;
        }

        private bool EntityBehaviorMortallyWoundable_CanSit(EntityAgent eagent, out string errorMessage)
        {
            errorMessage = null;
            return HealthState == EnumEntityHealthState.Normal;
        }

        private bool AnimManager_OnAnimationReceived(ref AnimationMetaData animationMeta, ref EnumHandling handling)
        {
            if (HealthState != EnumEntityHealthState.Normal && animationMeta.Animation == "die")
            {
                animationMeta = entity.Properties.Client.Animations.FirstOrDefault(m => m.Animation == "dead");
            }

            return true;
        }

        private bool AnimManager_OnStartAnimation(ref AnimationMetaData animationMeta, ref EnumHandling handling)
        {
            if (HealthState == EnumEntityHealthState.MortallyWounded && !animationMeta.Animation.StartsWith("wounded-") && animationMeta.Animation != "die")
            {
                handling = EnumHandling.PreventDefault;
                return true;
            }
            if (HealthState != EnumEntityHealthState.Normal && animationMeta.Animation == "die")
            {
                animationMeta = entity.Properties.Client.Animations.FirstOrDefault(m => m.Animation == "dead");
            }

            return false;
        }


        float accum = 0;
        public override void OnGameTick(float deltaTime)
        {
            if (!entity.Alive || entity.ShouldDespawn || HealthState == EnumEntityHealthState.Normal) return;
            if (entity.World.Side == EnumAppSide.Client) return;

            if (HealthState == EnumEntityHealthState.Recovering)
            {
                if (!entity.AnimManager.IsAnimationActive("wounded-stand") || entity.AnimManager.GetAnimationState("wounded-stand").AnimProgress > 0.9f)
                {
                    HealthState = EnumEntityHealthState.Normal;
                }
            }

            if (entity.World.Rand.NextDouble() < 0.03 && HealthState == EnumEntityHealthState.MortallyWounded && entity.World.Calendar.TotalHours > MortallyWoundedTotalHours + remainAliveHours)
            {
                Die();
            }

            if ((entity.World.Calendar.TotalHours - MortallyWoundedTotalHours) / remainAliveHours > 0.83f)
            {
                if (entity.AnimManager.IsAnimationActive("wounded-idle"))
                {
                    entity.AnimManager.StopAnimation("wounded-idle");
                    entity.AnimManager.StartAnimation("wounded-resthead");
                }
            }
            else
            {

                accum += deltaTime;

                if (accum > 7 && entity.World.Rand.NextDouble() < 0.005)
                {
                    string[] anims = new string[] { "wounded-look", "wounded-spasm", "wounded-trystand" };
                    entity.AnimManager.StartAnimation(anims[entity.World.Rand.Next(anims.Length)]);
                    accum = 0;
                }
            }
        }

        public override void OnEntityReceiveDamage(DamageSource damageSource, ref float damage)
        {
            if (entity.World.Side == EnumAppSide.Client) return;

            if (ebh.Health - damage <= 0 && HealthState == EnumEntityHealthState.Normal)
            {
                damage = ebh.Health - whenBelowHealth;
                HealthState = EnumEntityHealthState.MortallyWounded;
                MortallyWoundedTotalHours = entity.World.Calendar.TotalHours;
                setMortallyWounded();
                return;
            }

            if (HealthState == EnumEntityHealthState.MortallyWounded && damageSource.Type == EnumDamageType.Heal)
            {
                HealthHealed += damage;
                if (HealthHealed > healingRequiredForRescue)
                {
                    recover();
                    HealthHealed = 0;
                }
            }
        }

        private void recover()
        {
            HealthState = EnumEntityHealthState.Recovering;
            entity.WatchedAttributes.SetFloat("regenSpeed", 1);
            entity.AnimManager?.StopAnimation("wounded-idle");
            entity.AnimManager?.StopAnimation("wounded-resthead");
            entity.AnimManager?.StartAnimation("wounded-stand");
            entity.AnimManager?.StartAnimation("idle");
            var tasks = entity.GetBehavior<EntityBehaviorTaskAI>()?.TaskManager?.AllTasks;
            if (tasks != null)
            {
                foreach (var task in tasks)
                {
                    (task as AiTaskBaseTargetable)?.ClearAttacker();
                }
            }
            entity.GetBehavior<EntityBehaviorEmotionStates>()?.ClearStates();
        }

        private void setMortallyWounded()
        {            
            entity.GetBehavior<EntityBehaviorTaskAI>().TaskManager.StopTasks();
            (entity as EntityAgent).Controls.StopAllMovement();

            entity.GetBehavior<EntityBehaviorRideable>()?.UnmnountPassengers();

            entity.AnimManager?.StartAnimation("wounded-idle");

            entity.WatchedAttributes.SetFloat("regenSpeed", 0);
        }

        private void Die()
        {
            HealthState = EnumEntityHealthState.Dead;
            entity.Die();
        }
        
        public override void OnEntityDeath(DamageSource damageSourceForDeath)
        {
            HealthState = EnumEntityHealthState.Dead;
            base.OnEntityDeath(damageSourceForDeath);
        }


        public override void GetInfoText(StringBuilder infotext)
        {
            if (HealthState == EnumEntityHealthState.MortallyWounded && entity.Alive)
            {
                double hoursleft = MortallyWoundedTotalHours + remainAliveHours - entity.World.Calendar.TotalHours;

                if (hoursleft < 1) {
                    infotext.AppendLine(Lang.Get("Mortally wounded, alive for less than one hour."));
                } else
                {
                    infotext.AppendLine(Lang.Get("Mortally wounded, alive for {0} more hours", (int)hoursleft));
                }                
            }
        }

        public override WorldInteraction[] GetInteractionHelp(IClientWorldAccessor world, EntitySelection es, IClientPlayer player, ref EnumHandling handled)
        {
            var wis = base.GetInteractionHelp(world, es, player, ref handled);
            if (HealthState == EnumEntityHealthState.MortallyWounded && entity.Alive)
            {
                double hoursleft = MortallyWoundedTotalHours + remainAliveHours - entity.World.Calendar.TotalHours;
                if (hoursleft > 0)
                {
                    if (wis == null) wis = Array.Empty<WorldInteraction>();
                    wis = wis.Append(EntityBehaviorPlayerRevivable.GetReviveInteractionHelp(world.Api));
                }
            }

            return wis;
        }

        public override string PropertyName()
        {
            return "mortallywoundable";
        }
    }
}
