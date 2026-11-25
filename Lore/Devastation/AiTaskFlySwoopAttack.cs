using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent;

public class AiTaskFlySwoopAttack : AiTaskBaseTargetable
{
    [JsonProperty]
    protected EnumDamageType damageType = EnumDamageType.BluntAttack;
    [JsonProperty]
    protected int damageTier = 0;
    [JsonProperty]
    protected float damage = 2f;
    protected float knockbackStrength = 1f;
    [JsonProperty]
    protected float seekingRangeVer = 25f;
    [JsonProperty]
    protected float seekingRangeHor = 25f;
    [JsonProperty]
    protected float damageRange = 2f;
    [JsonProperty]
    protected float moveSpeed = 0.04f;
    protected TimeSpan attemptToExecuteCooldownMs = TimeSpan.FromMilliseconds(1500);
    protected TimeSpan targetRetentionTime = TimeSpan.FromSeconds(30);
    [JsonProperty]
    protected float firstTimeSwitchDistance = 0.90f;
    [JsonProperty]
    protected float secondTimeSwitchDistance = 0.25f;
    [JsonProperty]
    protected float secondTimeSwitchMinimumDistance = 0.10f;
    [JsonProperty]
    protected float timeSwitchHealthThreshold = 0.75f;
    [JsonProperty]
    protected float minVerticalDistance = 9;
    [JsonProperty]
    protected float minHorizontalDistance = 25;
    protected const float sensePlayerRange = 15;
    protected const float pathRefreshCooldown = 1;
    protected const float pathStopRefreshThreshold = 5;
    [JsonProperty]
    protected bool timeSwitchAtTheStart = false;
    [JsonProperty]
    protected int pathLength = 35;
    [JsonProperty]
    protected float speedThresholdForDamage = 0.3f;
    [JsonProperty]
    protected float timeSwitchProbability = 0.5f;
    [JsonProperty]
    protected long globalAttackCooldownMs = 3000;

    protected long lastCheckOrAttackMs;
    protected HashSet<long> didDamageEntity = new();
    protected Vec3d beginAttackPos = null!;
    protected List<Vec3d> swoopPath = null!;
    protected EntityBehaviorHealth? healthBehavior;
    protected float pathRefreshAccum = 0;
    protected float pathStopRefreshAccum = 0;
    protected float damageAccum = 0f;
    protected double initialDistanceToTarget = 0;
    protected int intendedDimension = 0;
    protected NatFloat timeSwitchRandom = null!;
    protected bool shouldUseTimeSwitchThisTime;

    protected int CurrentDimension => entity.Pos.Dimension;
    protected int TargetDimension => targetEntity?.Pos.Dimension ?? CurrentDimension;

    public AiTaskFlySwoopAttack(EntityAgent entity, JsonObject taskConfig, JsonObject aiConfig) : base(entity, taskConfig, aiConfig)
    {
        healthBehavior = entity.GetBehavior<EntityBehaviorHealth>();

        timeSwitchRandom = new NatFloat(0.5f, 0.5f, EnumDistribution.UNIFORM);

        knockbackStrength = taskConfig["knockbackStrength"].AsFloat(GameMath.Sqrt(damage / 2f));
        attemptToExecuteCooldownMs = TimeSpan.FromMilliseconds(taskConfig["attemptToExecuteCooldownMs"].AsInt(1500));
        targetRetentionTime = TimeSpan.FromSeconds(taskConfig["targetRetentionTimeSec"].AsInt(30));
    }

    public override bool ShouldExecute()
    {
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
        if (!targetOk) return false;

        beginAttackPos = entity.Pos.XYZ;
        swoopPath = new List<Vec3d>(getSwoopPath(targetEntity!, pathLength, false));
        return pathClear(swoopPath);
    }

    public override void StartExecute()
    {
        didDamageEntity.Clear();

        swoopPath.Clear();
        swoopPath.AddRange(getSwoopPath(targetEntity!, pathLength, true));
        pathTraverser.FollowRoute(swoopPath, moveSpeed, 8, null, null);

        pathStopRefreshAccum = 0;
        pathRefreshAccum = 0;

        initialDistanceToTarget = distanceToTarget();
        intendedDimension = CurrentDimension;

        shouldUseTimeSwitchThisTime = timeSwitchRandom.nextFloat() < timeSwitchProbability;

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

        pathStopRefreshAccum += dt;
        pathRefreshAccum += dt;
        if (pathRefreshAccum > pathRefreshCooldown && pathStopRefreshAccum < pathStopRefreshThreshold && (targetEntity!.Pos.Dimension == entity.Pos.Dimension))
        {
            refreshPath();
            pathRefreshAccum = 0;
        }


        if (distanceToTarget() > Math.Max(seekingRangeHor, seekingRangeVer) * 2) return false;

        damageAccum += dt;
        if (damageAccum > 0.2f && entity.Pos.Motion.Length() > speedThresholdForDamage)
        {
            damageAccum = 0;

            attackEntities();
        }

        double speed = entity.Pos.Motion.Length();
        if (speed > 0.01)
        {
            entity.Pos.Roll = (float)Math.Asin(GameMath.Clamp(-entity.Pos.Motion.Y / speed, -1, 1));
        }

        if (!pathTraverser.Active)
        {
            return false;
        }

        return true;
    }

    public override void FinishExecute(bool cancelled)
    {
        pathTraverser.Stop();

        if (entity is EntityErel erel)
        {
            erel.LastAttackTime = entity.World.ElapsedMilliseconds;
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

    protected bool pathClear(List<Vec3d> swoopPath)
    {
        int skipPoints = 2;
        Vec3d tmppos = new();

        for (int i = 0; i < swoopPath.Count; i += skipPoints)
        {
            tmppos.Set(swoopPath[i]);
            tmppos.Y--;
            if (world.CollisionTester.IsColliding(entity.World.BlockAccessor, entity.CollisionBox, tmppos))
            {
#if DEBUG
                /*Vec3f zero = Vec3f.Zero;
                for (int k = i; k < swoopPath.Count; k++)
                {
                    entity.World.SpawnParticles(1, ColorUtil.ColorFromRgba(0, 0, 255, 255), swoopPath[k], swoopPath[k], zero, zero, 3, 0, 1);
                }*/
#endif
                return false;
            }
        }

        return true;
    }
    protected virtual Vec3d[] getSwoopPath(Entity target, int its, bool simplifiedOut)
    {
        // Path traverser do not account for dimension in Y data and assumes it is local to dimension
        EntityPos targetEntityPos = target.Pos.Copy();
        targetEntityPos.Dimension = Dimensions.NormalWorld;

        EntityPos thisEntityPos = entity.Pos;
        thisEntityPos.Dimension = Dimensions.NormalWorld;

        Vec3d targetPos = targetEntityPos.XYZ.AddCopy(target.LocalEyePos);

        // Swoop in
        Vec3d start1 = entity.Pos.XYZ;
        Vec3d end1 = new(start1.X, targetPos.Y + 10, start1.Z);

        Vec3d start2 = end1;
        Vec3d end2 = targetPos;


        Vec3d delta1 = end1 - start1;
        Vec3d delta2 = end2 - start2;

        int outits = (simplifiedOut ? its / 3 : its);
        Vec3d[] points = new Vec3d[its + outits];

        for (int i = 0; i < its; i++)
        {
            double p = (double)i / its;

            Vec3d mid1 = start1 + p * delta1;
            Vec3d mid2 = start2 + p * delta2;

            points[i] = (1 - p) * mid1 + p * mid2;
        }

        // Swoop out
        start1 = points[its - 1];
        Vec3d offs = (targetEntityPos.XYZ - thisEntityPos.XYZ) * 1;
        end1 = new Vec3d(targetPos.X + offs.X, targetPos.Y, targetPos.Z + offs.Z);

        start2 = end1;
        end2 = new Vec3d(targetPos.X + offs.X * 1.3f, targetPos.Y + (beginAttackPos.Y - targetPos.Y) * 0.5f, targetPos.Z + offs.Z * 1.3f);

        delta1 = end1 - start1;
        delta2 = end2 - start2;


        for (int i = 0; i < outits; i++)
        {
            double p = (double)i / outits;

            Vec3d mid1 = start1 + p * delta1;
            Vec3d mid2 = start2 + p * delta2;

            points[its + i] = (1 - p) * mid1 + p * mid2;
        }

#if DEBUG
        /*Vec3f zero = Vec3f.Zero;
        for (int i = 0; i < points.Length; i++)
        {
            entity.World.SpawnParticles(1, ColorUtil.WhiteArgb, points[i], points[i], zero, zero, 3, 0, 1);
        }*/
#endif

        return points;
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

    protected void updateSwoopPathDimension(int fromDimension, int toDimension)
    {
        double difference = (toDimension - fromDimension) * BlockPos.DimensionBoundary;
        foreach (Vec3d point in swoopPath)
        {
            point.Y += difference;
        }
    }

    protected void refreshPath()
    {
        ArgumentNullException.ThrowIfNull(targetEntity);
        Vec3d[] path = getSwoopPath(targetEntity, pathLength, true);
        if (pathClear(new List<Vec3d>(path)))
        {
            swoopPath.Clear();
            swoopPath.AddRange(path);
        }
    }
}
