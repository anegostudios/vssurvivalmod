using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class ProjectileConfig
    {
        public AssetLocation Code;
        public NatFloat Quantity;
        public float Damage;
        public EnumDamageType DamageType;
        public JsonItemStack CollectibleStack;

        public EntityProperties EntityType;

        public int LeftToFire;
    }

    public class AiTaskFireFeathersAttack : AiTaskTargetableAt
    {
        protected float seekingRangeVer = 25f;
        protected float seekingRangeHor = 25f;
        int fireAfterMs;
        int durationMs;
        ProjectileConfig[] projectileConfigs;

        public bool Enabled = true;

        public AiTaskFireFeathersAttack(EntityAgent entity) : base(entity)
        {
        }

        public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig)
        {
            base.LoadConfig(taskConfig, aiConfig);

            fireAfterMs = taskConfig["fireAfterMs"].AsInt(1000);
            durationMs = taskConfig["durationMs"].AsInt(1000);
            seekingRangeHor = taskConfig["seekingRangeHor"].AsFloat(25);
            seekingRangeVer = taskConfig["seekingRangeVer"].AsFloat(25);
            projectileConfigs = taskConfig["projectileConfigs"].AsObject<ProjectileConfig[]>(null, entity.Code.Domain);

            foreach (var val in projectileConfigs)
            {
                val.EntityType = entity.World.GetEntityType(val.Code);
                if (val.EntityType == null)
                {
                    throw new Exception("No such projectile exists - " + val.Code);
                }

                val.CollectibleStack?.Resolve(entity.World, string.Format("Projectile stack of {0}", entity.Code), true);
            }
        }

        public override bool ShouldExecute()
        {
            if (!Enabled) return false;

            CenterPos = SpawnPos;

            long ellapsedMs = entity.World.ElapsedMilliseconds;
            if (cooldownUntilMs > ellapsedMs)
            {
                return false;
            }

            // Don't try too often
            cooldownUntilMs = entity.World.ElapsedMilliseconds + 1500;

            if (!PreconditionsSatisifed()) return false;


            if (entity.World.ElapsedMilliseconds - attackedByEntityMs > 30000)
            {
                attackedByEntity = null;
            }
            if (retaliateAttacks && attackedByEntity != null && attackedByEntity.Alive && attackedByEntity.IsInteractable && IsTargetableEntity(attackedByEntity, 15, true))
            {
                targetEntity = attackedByEntity;
            }
            else
            {
                targetEntity = entity.World.GetNearestEntity(CenterPos, seekingRangeHor, seekingRangeVer, (e) =>
                {
                    return IsTargetableEntity(e, seekingRangeHor) && hasDirectContact(e, seekingRangeHor, seekingRangeVer);
                });
            }


            bool targetOk = targetEntity != null && entity.ServerPos.Y - targetEntity.ServerPos.Y > 9 && entity.ServerPos.HorDistanceTo(targetEntity.ServerPos) > 25;

            return targetOk;
        }


        private void fireProjectiles()
        {
            var world = entity.World;
            var rnd = world.Rand;

            projectileConfigs = projectileConfigs.Shuffle(rnd);

            foreach (var cfg in projectileConfigs)
            {
                if (cfg.LeftToFire <= 0) continue;

                cfg.LeftToFire--;
                var entitypr = world.ClassRegistry.CreateEntity(cfg.EntityType) as EntityProjectile;
                entitypr.FiredBy = entity;
                entitypr.DamageType = cfg.DamageType;
                entitypr.Damage = cfg.Damage;
                entitypr.ProjectileStack = cfg.CollectibleStack?.ResolvedItemstack?.Clone() ?? new ItemStack(world.GetItem(new AssetLocation("stone-granite")));
                entitypr.NonCollectible = cfg.CollectibleStack?.ResolvedItemstack == null;
                entitypr.World = world;

                Vec3d spawnpos = entity.ServerPos.XYZ.Add(rnd.NextDouble()*6 - 3, rnd.NextDouble() * 5, rnd.NextDouble() * 6 - 3);
                Vec3d targetPos = targetEntity.ServerPos.XYZ.Add(0, targetEntity.LocalEyePos.Y, 0) + targetEntity.ServerPos.Motion * 8;

                double dist = spawnpos.DistanceTo(targetPos);
                double distf = Math.Pow(dist, 0.2);
                Vec3d velocity = (targetPos - spawnpos).Normalize() * GameMath.Clamp(distf - 1f, 0.1f, 1f);
                velocity.Y += (dist - 10) / 150.0;

                velocity.X *= 1 + (rnd.NextDouble() - 0.5) / 3f;
                velocity.Y *= 1 + (rnd.NextDouble() - 0.5) / 5f;
                velocity.Z *= 1 + (rnd.NextDouble() - 0.5) / 3f;

                entitypr.ServerPos.SetPosWithDimension(spawnpos);
                entitypr.Pos.SetFrom(spawnpos);
                entitypr.ServerPos.Motion.Set(velocity);
                entitypr.SetInitialRotation();
                world.SpawnEntity(entitypr);

                break;
            }            
        }


        float accum;
        bool projectilesFired;
        public override void StartExecute()
        {
            base.StartExecute();
            accum = 0;
            projectilesFired = false;
        }

        public override bool ContinueExecute(float dt)
        {
            accum += dt;
            if (accum*1000 > fireAfterMs)
            {
                if (!projectilesFired)
                {
                    foreach (var cfg in projectileConfigs)
                    {
                        cfg.LeftToFire = GameMath.RoundRandom(entity.World.Rand, cfg.Quantity.nextFloat());
                    }
                    world.PlaySoundAt("sounds/creature/erel/fire", entity, null, false, 100);
                }

                fireProjectiles();
                projectilesFired=true;
            }

            return accum * 1000 < durationMs;
        }
    }
}
