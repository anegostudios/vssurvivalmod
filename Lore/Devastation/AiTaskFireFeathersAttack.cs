using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent;

public class ProjectileConfig
{
    public AssetLocation Code;
    public NatFloat Quantity;
    public float Damage;
    public EnumDamageType DamageType;
    public int DamageTier;
    public JsonItemStack CollectibleStack;

    public EntityProperties EntityType;

    public int LeftToFire;
}

public class AiTaskFireFeathersAttack : AiTaskFlyCircle
{
    public bool Enabled = true;

    protected float seekingRangeVer = 25f;
    protected float seekingRangeHor = 25f;
    protected int fireAfterMs;
    protected int durationMs;
    protected ProjectileConfig[] projectileConfigs;
    protected float accum;
    protected bool projectilesFired;
    protected float minVerticalDistance = 5;
    protected float minHorizontalDistance = 10;
    protected long globalAttackCooldownMs = 3000;

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
        minVerticalDistance = taskConfig["minVerticalDistance"].AsFloat(5);
        minHorizontalDistance = taskConfig["minHorizontalDistance"].AsFloat(10);
        globalAttackCooldownMs = taskConfig["globalAttackCooldownMs"].AsInt(3000);

        foreach (ProjectileConfig projectileConfig in projectileConfigs)
        {
            projectileConfig.EntityType = entity.World.GetEntityType(projectileConfig.Code);
            
            if (projectileConfig.EntityType == null)
            {
                throw new Exception("No such projectile exists - " + projectileConfig.Code);
            }

            projectileConfig.CollectibleStack?.Resolve(entity.World, string.Format("Projectile stack of {0}", entity.Code), true);
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

        if (!checkGlobalAttackCooldown())
        {
            return false;
        }

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


        bool targetOk = targetEntity != null && entity.ServerPos.Y - targetEntity.ServerPos.Y > minVerticalDistance && entity.ServerPos.HorDistanceTo(targetEntity.ServerPos) > minHorizontalDistance;

        return targetOk;
    }
    public override void StartExecute()
    {
        base.StartExecute();
        accum = 0;
        projectilesFired = false;
    }
    public override bool ContinueExecute(float dt)
    {
        followTarget();

        accum += dt;
        if (accum * 1000 > fireAfterMs)
        {
            if (!projectilesFired)
            {
                foreach (ProjectileConfig cfg in projectileConfigs)
                {
                    cfg.LeftToFire = GameMath.RoundRandom(entity.World.Rand, cfg.Quantity.nextFloat());
                }
                world.PlaySoundAt("sounds/creature/erel/fire", entity, null, false, 100);
            }

            fireProjectiles();
            projectilesFired = true;
        }

        return base.ContinueExecute(dt) && accum * 1000 < durationMs;
    }
    public override void FinishExecute(bool cancelled)
    {
        (entity as EntityErel).LastAttackTime = entity.World.ElapsedMilliseconds;

        base.FinishExecute(cancelled);
    }

    protected void followTarget()
    {
        Vec3d targetVector = targetEntity.Pos.XYZ - entity.ServerPos.XYZ;
        Vec3d direction = targetVector.Normalize();
        //entity.ServerPos.Motion.X = direction.X * moveSpeed * 10;
        //entity.ServerPos.Motion.Y = direction.Y * moveSpeed * 10;
        //entity.ServerPos.Motion.Z = direction.Z * moveSpeed * 10;

        double speed = entity.ServerPos.Motion.Length();
        if (speed > 0.01)
        {
            //entity.ServerPos.Roll = (float)Math.Asin(GameMath.Clamp(-direction.Y / speed, -1, 1));
        }
        entity.ServerPos.Yaw = (float)Math.Atan2(targetVector.X, targetVector.Z);
    }

    protected bool checkGlobalAttackCooldown()
    {
        long lastAttack = (entity as EntityErel).LastAttackTime;
        long currentTime = entity.World.ElapsedMilliseconds;

        return currentTime - lastAttack > globalAttackCooldownMs;
    }

    protected void fireProjectiles()
    {
        IWorldAccessor world = entity.World;
        Random rnd = world.Rand;

        projectileConfigs = projectileConfigs.Shuffle(rnd);

        foreach (ProjectileConfig cfg in projectileConfigs)
        {
            if (cfg.LeftToFire <= 0) continue;

            cfg.LeftToFire--;
            EntityProjectile entitypr = world.ClassRegistry.CreateEntity(cfg.EntityType) as EntityProjectile;
            entitypr.FiredBy = entity;
            entitypr.DamageType = cfg.DamageType;
            entitypr.Damage = cfg.Damage;
            entitypr.DamageTier = cfg.DamageTier;
            entitypr.ProjectileStack = cfg.CollectibleStack?.ResolvedItemstack?.Clone() ?? new ItemStack(world.GetItem(new AssetLocation("stone-granite")));
            entitypr.NonCollectible = cfg.CollectibleStack?.ResolvedItemstack == null;
            entitypr.World = world;

            Vec3d spawnpos = entity.ServerPos.XYZ.Add(rnd.NextDouble() * 6 - 3, rnd.NextDouble() * 5, rnd.NextDouble() * 6 - 3);
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
}
