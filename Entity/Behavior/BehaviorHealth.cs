using System;
using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    class EntityBehaviorHealth : EntityBehavior
    {
        ITreeAttribute healthTree;

        internal float Health
        {
            get { return healthTree.GetFloat("currenthealth"); }
            set { healthTree.SetFloat("currenthealth", value); entity.WatchedAttributes.MarkPathDirty("health"); }
        }

        internal float MaxHealth
        {
            get { return healthTree.GetFloat("maxhealth"); }
            set { healthTree.SetFloat("maxhealth", value); entity.WatchedAttributes.MarkPathDirty("health"); }
        }

        internal float HealthLocked
        {
            get { return healthTree.GetFloat("healthlocked"); }
            set { healthTree.SetFloat("healthlocked", value); entity.WatchedAttributes.MarkPathDirty("health"); }
        }


        public EntityBehaviorHealth(Entity entity) : base(entity)
        {
        }

        public override void Initialize(EntityType config, JsonObject typeAttributes)
        {
            healthTree = entity.WatchedAttributes.GetTreeAttribute("health");

            if (healthTree == null)
            {
                entity.WatchedAttributes.SetAttribute("health", healthTree = new TreeAttribute());

                Health = typeAttributes["currenthealth"].AsFloat(20);
                MaxHealth = typeAttributes["maxhealth"].AsFloat(20);
                HealthLocked = typeAttributes["healthlocked"].AsFloat(0);
                return;
            }

            Health = healthTree.GetFloat("currenthealth");
            MaxHealth = healthTree.GetFloat("maxhealth");
        }

        
        public override void OnGameTick(float deltaTime)
        {
            if (entity.Pos.Y < -30)
            {
                entity.ReceiveDamage(new DamageSource()
                {
                    source = EnumDamageSource.Void,
                    type = EnumDamageType.Gravity
                }, 4);
            }
        }

        public override void OnEntityReceiveDamage(DamageSource damageSource, float damage)
        {
            if (damageSource.type == EnumDamageType.Heal)
            {
                Health = Math.Min(Health + damage, MaxHealth - HealthLocked);
                entity.OnHurt(damageSource, damage);
                return;
            }

            if (!entity.Alive) return;

            Health -= damage;
            entity.OnHurt(damageSource, damage);

            if (Health <= 0)
            {
                Health = 0;
                entity.Die(
                    EnumDespawnReason.Death, 
                    damageSource
                );

                float lengthHalf = entity.CollisionBox.X2 - entity.CollisionBox.X1;
                float height = entity.CollisionBox.Y2;
                Random rnd = entity.World.Rand;

                entity.World.SpawnParticles(30,
                    ColorUtil.ColorFromArgb(100, 255, 255, 255),
                    entity.Pos.XYZ.SubCopy(lengthHalf, 0, lengthHalf),
                    entity.Pos.XYZ.AddCopy(lengthHalf, height, lengthHalf), 
                    new Vec3f(0, 0, 0),
                    new Vec3f(2 * (float)rnd.NextDouble() - 1f, 2 * (float)rnd.NextDouble() - 1f, 2 * (float)rnd.NextDouble() - 1f),
                    1f,
                    -0.1f,
                    1f,
                    EnumParticleModel.Quad
                );
            } else
            {
                if (damage > 1f) entity.StartAnimation("hurt");
            }
        }

        public override void OnFallToGround(Vec3d positionBeforeFalling, double withYMotion)
        {
            if (!entity.Type.FallDamage) return;

            double yDistance = Math.Abs(positionBeforeFalling.Y - entity.Pos.Y);

            if (yDistance < 3.5f) return;

            // Experimentally determined - at 3.5 blocks the player has a motion of -0.19
            if (withYMotion > -0.19) return;  

            double fallDamage = yDistance - 3.5f;

            // Some super rough experimentally determined formula that always underestimates
            // the actual ymotion.
            // lets us reduce the fall damage if the player lost in y-motion during the fall
            // will totally break if someone changes the gravity constant
            double expectedYMotion = -0.041f * Math.Pow(fallDamage, 0.75f) - 0.22f;
            double yMotionLoss = Math.Max(0, -expectedYMotion + withYMotion);
            fallDamage -= 20 * yMotionLoss;

            if (fallDamage <= 0) return;

            /*if (fallDamage > 2)
            {
                entity.StartAnimation("heavyimpact");
            }*/

            entity.ReceiveDamage(new DamageSource()
            {
                source = EnumDamageSource.Fall,
                type = EnumDamageType.Gravity
            }, (float)fallDamage);
        }

        public override string PropertyName()
        {
            return "health";
        }
    }
}
