using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent;

public class AiTaskFlyDiveAttack : AiTaskBaseTargetable
{
    public bool Enabled { get; set; } = true;

    [JsonProperty]
    protected float damage = 2f;
    [JsonProperty]
    protected EnumDamageType damageType = EnumDamageType.BluntAttack;
    [JsonProperty]
    protected int damageTier = 0;
    protected float knockbackStrength = 1f;
    protected long lastCheckOrAttackMs;
    [JsonProperty]
    protected float seekingRangeVer = 25f;
    [JsonProperty]
    protected float seekingRangeHor = 25f;
    [JsonProperty]
    protected float damageRange = 5f;
    [JsonProperty]
    protected float moveSpeed = 0.04f;
    protected TimeSpan attemptToExecuteCooldownMs = TimeSpan.FromMilliseconds(1500);
    protected TimeSpan targetRetentionTime = TimeSpan.FromSeconds(30);
    protected const float minVerticalDistance = 9;
    protected const float minHorizontalDistance = 20;
    protected const float sensePlayerRange = 15;
    protected float diveRange = 20;
    protected float requireMinRange = 30;
    [JsonProperty]
    protected float diveHeight = 30;
    [JsonProperty]
    protected float timeSwitchProbability = 0.5f;
    [JsonProperty]
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


    public AiTaskFlyDiveAttack(EntityAgent entity, JsonObject taskConfig, JsonObject aiConfig) : base(entity, taskConfig, aiConfig)
    {
        healthBehavior = entity.GetBehavior<EntityBehaviorHealth>();
        knockbackStrength = taskConfig["knockbackStrength"].AsFloat(GameMath.Sqrt(damage / 2f));
        attemptToExecuteCooldownMs = TimeSpan.FromMilliseconds(taskConfig["attemptToExecuteCooldownMs"].AsInt(1500));
        targetRetentionTime = TimeSpan.FromSeconds(taskConfig["targetRetentionTimeSec"].AsInt(30));
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

        if (!PreconditionsSatisfied()) return false;

        if (!checkGlobalAttackCooldown())
        {
            return false;
        }

        Vec3d pos = entity.Pos.XYZ.Add(0, entity.SelectionBox.Y2 / 2, 0).Ahead(entity.SelectionBox.XSize / 2, 0, entity.Pos.Yaw);

        if (entity.World.ElapsedMilliseconds - attackedByEntityMs > (long)targetRetentionTime.TotalMilliseconds)
        {
            attackedByEntity = null;
        }

        if (ShouldRetaliateForRange(sensePlayerRange))
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

        bool targetOk = targetEntity != null && entity.Pos.Y - targetEntity.Pos.Y > minVerticalDistance && entity.Pos.HorDistanceTo(targetEntity.Pos) > minHorizontalDistance;
        return targetOk;
    }
    public override void StartExecute()
    {
        didDamageEntity.Clear();
        targetPos.SetFrom(targetEntity!.Pos);
        diving = false;
        impacted = false;
        base.StartExecute();
    }

    public override bool ContinueExecute(float dt)
    {
        //Check if time is still valid for task.
        if (!IsInValidDayTimeHours(false)) return false;

        if (timeoutExceeded())
        {
            return false;
        }

        updateTargetPosition();

        if (impacted)
        {
            entity.GetBehavior<EntityBehaviorTaskAI>()?.PathTraverser?.Stop();
            return onImpact();
        }


        if (!diving)
        {
            // If too close, fly up
            if (entity.Pos.Y - targetPos.Y < diveHeight)
            {
                entity.Pos.Motion.Y = 0.15f;
                entity.Pos.Motion.X *= 0.9f;
                entity.Pos.Motion.Z *= 0.9f;

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

        if (entity is EntityErel erel)
        {
            erel.LastAttackTime = entity.World.ElapsedMilliseconds;
            erel.StandingOnGround = false;
        }


        base.FinishExecute(cancelled);
    }

    protected bool checkGlobalAttackCooldown()
    {
        EntityErel erel = (EntityErel)entity;
        long lastAttack = erel.LastAttackTime;
        long currentTime = entity.World.ElapsedMilliseconds;

        return currentTime - lastAttack > globalAttackCooldownMs;
    }
    protected void updateTargetPosition()
    {
        ArgumentNullException.ThrowIfNull(targetEntity);
        if (targetEntity.Pos.Dimension == entity.Pos.Dimension)
        {
            targetPos.SetFrom(targetEntity.Pos);
        }
    }

    protected bool onImpact()
    {
        entity.Pos.Roll = 0;
        entity.Pos.Motion.Set(0, 0, 0);

        RunningAnimation state = entity.AnimManager.GetAnimationState("slam");

        if (state != null && state.AnimProgress > 0.9f)
        {
            entity.AnimManager.StartAnimation("takeoff");
        }

        var ee = (entity as EntityErel);
        if (ee != null) ee.StandingOnGround = true;

        return state == null || state.AnimProgress < 0.99f;
    }
    protected void followTargetOnFlyUp()
    {
        Vec3d targetVector = targetPos.XYZ - entity.Pos.XYZ;

        entity.Pos.Roll = -15 * GameMath.DEG2RAD;// (float)Math.Asin(GameMath.Clamp(direction.Y / speed, -1, 1));
        entity.Pos.Yaw = (float)Math.Atan2(targetVector.X, targetVector.Z);
    }
    protected void followTarget()
    {
        Vec3d targetVector = targetPos.XYZ - entity.Pos.XYZ;
        Vec3d direction = targetVector.Normalize();
        entity.Pos.Motion.X = direction.X * moveSpeed * 10;
        entity.Pos.Motion.Y = direction.Y * moveSpeed * 10;
        entity.Pos.Motion.Z = direction.Z * moveSpeed * 10;

        double speed = entity.Pos.Motion.Length();
        if (speed > 0.01)
        {
            entity.Pos.Roll = (float)Math.Asin(GameMath.Clamp(-direction.Y / speed, -1, 1));
        }
        entity.Pos.Yaw = (float)Math.Atan2(targetVector.X, targetVector.Z);
    }
    protected void attackEntities()
    {
        List<Entity> attackableEntities = new();
        EntityPartitioning ep = entity.Api.ModLoader.GetModSystem<EntityPartitioning>();
        ep.GetNearestEntity(entity.Pos.XYZ, damageRange + 1, (e) =>
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
        ArgumentNullException.ThrowIfNull(targetEntity);
        double xDistance = entity.Pos.X - targetEntity.Pos.X;
        double yDistance = entity.Pos.Y - targetEntity.Pos.Y;
        double zDistance = entity.Pos.Z - targetEntity.Pos.Z;

        return Math.Sqrt(xDistance * xDistance + yDistance * yDistance + zDistance * zDistance);
    }
}
