using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent;

#nullable disable

public class AiTaskFlyDiveAttack : AiTaskBaseTargetable
{
    public bool Enabled { get; set; } = true;

    protected float damage = 2f;
    protected EnumDamageType damageType = EnumDamageType.BluntAttack;
    protected int damageTier = 0;
    protected float knockbackStrength = 1f;
    protected long lastCheckOrAttackMs;
    protected float seekingRangeVer = 25f;
    protected float seekingRangeHor = 25f;
    protected float damageRange = 5f;
    protected float moveSpeed = 0.04f;
    protected TimeSpan attemptToExecuteCooldownMs = TimeSpan.FromMilliseconds(1500);
    protected TimeSpan targetRetentionTime = TimeSpan.FromSeconds(30);
    protected const float minVerticalDistance = 9;
    protected const float minHorizontalDistance = 20;
    protected const float sensePlayerRange = 15;
    protected float diveRange = 20;
    protected float requireMinRange = 30;
    protected float diveHeight = 30;
    protected float timeSwitchProbability = 0.5f;
    protected long globalAttackCooldownMs = 3000;

    protected HashSet<long> didDamageEntity = new();
    protected EntityPos targetPos = new();
    protected EntityBehaviorHealth? healthBehavior;
    protected float damageAccum = 0f;
    protected bool diving = false;
    protected bool impacted = false;
    protected double diveDistance = 1;
    protected bool shouldUseTimeSwitchThisTime;

    protected int CurrentDimension => entity.Pos.Dimension;
    protected int TargetDimension => targetEntity?.Pos.Dimension ?? CurrentDimension;


    public AiTaskFlyDiveAttack(EntityAgent entity) : base(entity)
    {
        healthBehavior = entity.GetBehavior<EntityBehaviorHealth>();
    }

    public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig)
    {
        base.LoadConfig(taskConfig, aiConfig);

        moveSpeed = taskConfig["moveSpeed"].AsFloat(0.04f);
        damage = taskConfig["damage"].AsFloat(2);
        knockbackStrength = taskConfig["knockbackStrength"].AsFloat(GameMath.Sqrt(damage / 2f));
        seekingRangeHor = taskConfig["seekingRangeHor"].AsFloat(25);
        seekingRangeVer = taskConfig["seekingRangeVer"].AsFloat(25);
        damageRange = taskConfig["damageRange"].AsFloat(2);
        damageType = Enum.Parse<EnumDamageType>(taskConfig["damageType"].AsString("BluntAttack"));
        damageTier = taskConfig["damageTier"].AsInt(0);
        attemptToExecuteCooldownMs = TimeSpan.FromMilliseconds(taskConfig["attemptToExecuteCooldownMs"].AsInt(1500));
        targetRetentionTime = TimeSpan.FromSeconds(taskConfig["targetRetentionTimeSec"].AsInt(30));
        diveHeight = taskConfig["diveHeight"].AsFloat(30);
        timeSwitchProbability = taskConfig["timeSwitchProbability"].AsFloat(0.5f);
        globalAttackCooldownMs = taskConfig["globalAttackCooldownMs"].AsInt(3000);
    }

    public override bool ShouldExecute()
    {
        if (!Enabled) return false;

        long elapsedMs = entity.World.ElapsedMilliseconds;
        if (cooldownUntilMs > elapsedMs)
        {
            return false;
        }

        cooldownUntilMs = entity.World.ElapsedMilliseconds + (long)attemptToExecuteCooldownMs.TotalMilliseconds;

        if (!PreconditionsSatisifed()) return false;

        if (!checkGlobalAttackCooldown())
        {
            return false;
        }

        Vec3d pos = entity.ServerPos.XYZ.Add(0, entity.SelectionBox.Y2 / 2, 0).Ahead(entity.SelectionBox.XSize / 2, 0, entity.ServerPos.Yaw);

        if (entity.World.ElapsedMilliseconds - attackedByEntityMs > (long)targetRetentionTime.TotalMilliseconds)
        {
            attackedByEntity = null;
        }

        if (retaliateAttacks && attackedByEntity != null && attackedByEntity.Alive && attackedByEntity.IsInteractable && IsTargetableEntity(attackedByEntity, sensePlayerRange, true))
        {
            targetEntity = attackedByEntity;
        }
        else
        {
            targetEntity = entity.World.GetNearestEntity(pos, seekingRangeHor, seekingRangeVer, (e) =>
            {
                return IsTargetableEntity(e, seekingRangeHor) && hasDirectContact(e, seekingRangeHor, seekingRangeVer);
            });
        }

        lastCheckOrAttackMs = entity.World.ElapsedMilliseconds;

        bool targetOk = targetEntity != null && entity.ServerPos.Y - targetEntity.ServerPos.Y > minVerticalDistance && entity.ServerPos.HorDistanceTo(targetEntity.ServerPos) > minHorizontalDistance;
        return targetOk;
    }
    public override void StartExecute()
    {
        didDamageEntity.Clear();
        targetPos.SetFrom(targetEntity.ServerPos);
        diving = false;
        impacted = false;
        base.StartExecute();
    }
    public override bool ContinueExecute(float dt)
    {
        if (timeoutExceeded())
        {
            return false;
        }
        
        updateTargetPosition();

        if (impacted)
        {
            return onImpact();
        }


        if (!diving)
        {
            // If too close, fly up
            if (entity.ServerPos.Y - targetPos.Y < diveHeight)
            {
                entity.ServerPos.Motion.Y = 0.15f;
                entity.ServerPos.Motion.X *= 0.9f;
                entity.ServerPos.Motion.Z *= 0.9f;

                followTargetOnFlyUp();

                return true;
            }

            // Far enough, start dive
            entity.AnimManager.StopAnimation("fly-idle");
            entity.AnimManager.StopAnimation("fly-flapactive");
            entity.AnimManager.StopAnimation("fly-flapcruise");
            entity.AnimManager.StartAnimation("dive");

            diveDistance = distanceToTarget();

            diving = true;
        }

        followTarget();

        if (entity.Collided)
        {
            entity.AnimManager.StopAnimation("dive");
            entity.AnimManager.StartAnimation("slam");
            impacted = true;

            attackEntities();

            return onImpact();
        }

        damageAccum += dt;
        if (damageAccum > 0.2f)
        {
            attackEntities();

            damageAccum = 0;
        }

        return true;

    }
    public override void FinishExecute(bool cancelled)
    {
        pathTraverser.Stop();
        //entity.AnimManager.StartAnimation("fly-flapactive-fast");
        entity.AnimManager.StartAnimation("fly-idle");
        entity.AnimManager.StopAnimation("slam");

        (entity as EntityErel).LastAttackTime = entity.World.ElapsedMilliseconds;

        base.FinishExecute(cancelled);
    }

    protected bool checkGlobalAttackCooldown()
    {
        long lastAttack = (entity as EntityErel).LastAttackTime;
        long currentTime = entity.World.ElapsedMilliseconds;

        return currentTime - lastAttack > globalAttackCooldownMs;
    }
    protected void updateTargetPosition()
    {
        if (targetEntity.Pos.Dimension == entity.Pos.Dimension)
        {
            targetPos.SetFrom(targetEntity.ServerPos);
        }
    }

    protected bool onImpact()
    {
        entity.ServerPos.Roll = 0;
        entity.ServerPos.Motion.Set(0, 0, 0);

        RunningAnimation state = entity.AnimManager.GetAnimationState("slam");

        if (state != null && state.AnimProgress > 0.5f)
        {
            entity.AnimManager.StartAnimation("takeoff");
        }

        return state == null || state.AnimProgress < 0.6f;
    }
    protected void followTargetOnFlyUp()
    {
        Vec3d targetVector = targetPos.XYZ - entity.ServerPos.XYZ;
        Vec3d direction = targetVector.Normalize();

        double speed = entity.ServerPos.Motion.Length();
        entity.ServerPos.Roll = -15 * GameMath.DEG2RAD;// (float)Math.Asin(GameMath.Clamp(direction.Y / speed, -1, 1));
        entity.ServerPos.Yaw = (float)Math.Atan2(targetVector.X, targetVector.Z);
    }
    protected void followTarget()
    {
        Vec3d targetVector = targetPos.XYZ - entity.ServerPos.XYZ;
        Vec3d direction = targetVector.Normalize();
        entity.ServerPos.Motion.X = direction.X * moveSpeed * 10;
        entity.ServerPos.Motion.Y = direction.Y * moveSpeed * 10;
        entity.ServerPos.Motion.Z = direction.Z * moveSpeed * 10;

        double speed = entity.ServerPos.Motion.Length();
        if (speed > 0.01)
        {
            entity.ServerPos.Roll = (float)Math.Asin(GameMath.Clamp(-direction.Y / speed, -1, 1));
        }
        entity.ServerPos.Yaw = (float)Math.Atan2(targetVector.X, targetVector.Z);
    }
    protected void attackEntities()
    {
        List<Entity> attackableEntities = new();
        EntityPartitioning ep = entity.Api.ModLoader.GetModSystem<EntityPartitioning>();
        ep.GetNearestEntity(entity.ServerPos.XYZ, damageRange + 1, (e) =>
        {
            if (IsTargetableEntity(e, damageRange) && hasDirectContact(e, damageRange, damageRange) && !didDamageEntity.Contains(entity.EntityId))
            {
                attackableEntities.Add(e);
            }
            return false;
        }, EnumEntitySearchType.Creatures);


        foreach (Entity attackEntity in attackableEntities)
        {
            attackEntity.ReceiveDamage(
                new DamageSource()
                {
                    Source = EnumDamageSource.Entity,
                    SourceEntity = entity,
                    Type = damageType,
                    DamageTier = damageTier,
                    KnockbackStrength = knockbackStrength
                },
                damage * GlobalConstants.CreatureDamageModifier
            );

            if (entity is IMeleeAttackListener imal)
            {
                imal.DidAttack(attackEntity);
            }

            didDamageEntity.Add(entity.EntityId);
        }
    }

    protected double distanceToTarget()
    {
        double xDistance = entity.ServerPos.X - targetEntity.Pos.X;
        double yDistance = entity.ServerPos.Y - targetEntity.Pos.Y;
        double zDistance = entity.ServerPos.Z - targetEntity.Pos.Z;

        return Math.Sqrt(xDistance * xDistance + yDistance * yDistance + zDistance * zDistance);
    }
}
