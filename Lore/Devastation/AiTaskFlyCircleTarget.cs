using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using VSEssentialsMod.Entity.AI.Task;

namespace Vintagestory.GameContent;

public class AiTaskFlyCircleTarget : AiTaskFlyCircle
{
    protected float seekingRangeVer = 25f;
    protected float seekingRangeHor = 25f;
    protected TimeSpan cooldownTime = TimeSpan.FromMilliseconds(1000);
    protected TimeSpan targetRetentionTime = TimeSpan.FromSeconds(30);

    protected int CurrentDimension => entity.Pos.Dimension;
    protected int TargetDimension => targetEntity?.Pos.Dimension ?? CurrentDimension;
    protected int OtherDimension => CurrentDimension == Dimensions.NormalWorld ? 2 : Dimensions.NormalWorld;

    public AiTaskFlyCircleTarget(EntityAgent entity) : base(entity)
    {
    }

    public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig)
    {
        base.LoadConfig(taskConfig, aiConfig);

        seekingRangeHor = taskConfig["seekingRangeHor"].AsFloat(25);
        seekingRangeVer = taskConfig["seekingRangeVer"].AsFloat(25);
        cooldownTime = TimeSpan.FromMilliseconds(taskConfig["cooldownMs"].AsInt(1000));
        targetRetentionTime = TimeSpan.FromSeconds(taskConfig["targetRetentionTimeSec"].AsInt(30));
    }

    public override bool ShouldExecute()
    {
        long elapsedMs = entity.World.ElapsedMilliseconds;
        if (cooldownUntilMs > elapsedMs)
        {
            return false;
        }

        // Don't try more than once a second
        cooldownUntilMs = entity.World.ElapsedMilliseconds + (long)cooldownTime.TotalMilliseconds;

        if (!PreconditionsSatisifed()) return false;

        Vec3d pos = entity.ServerPos.XYZ.Add(0, entity.SelectionBox.Y2 / 2, 0).Ahead(entity.SelectionBox.XSize / 2, 0, entity.ServerPos.Yaw);

        if (entity.World.ElapsedMilliseconds - attackedByEntityMs > (long)targetRetentionTime.TotalMilliseconds)
        {
            attackedByEntity = null;
        }
        if (retaliateAttacks && attackedByEntity != null && attackedByEntity.Alive && attackedByEntity.IsInteractable && IsTargetableEntity(attackedByEntity, 15, true))
        {
            targetEntity = attackedByEntity;
        }
        else
        {
            targetEntity = entity.World.GetNearestEntity(pos, seekingRangeHor, seekingRangeVer, (e) =>
            {
                return IsTargetableEntity(e, seekingRangeHor) && hasDirectContact(e, seekingRangeHor, seekingRangeVer);
            });

            if (targetEntity == null)
            {
                pos.Y += dimensionOffset(CurrentDimension, OtherDimension);

                targetEntity = entity.World.GetNearestEntity(pos, seekingRangeHor, seekingRangeVer, (e) =>
                {
                    return IsTargetableEntity(e, seekingRangeHor) && hasDirectContact(e, seekingRangeHor, seekingRangeVer);
                });
            }
        }

        return targetEntity != null && base.ShouldExecute();
    }

    public override void StartExecute()
    {
        timeSwitchToNormalWorld();

        base.StartExecute();
        CenterPos = targetEntity.ServerPos.XYZ;
        CenterPos.Y += dimensionOffset(TargetDimension, CurrentDimension);
    }

    public override bool 
        ContinueExecute(float dt)
    {
        CenterPos = targetEntity.ServerPos.XYZ;
        CenterPos.Y += dimensionOffset(TargetDimension, CurrentDimension);
        return base.ContinueExecute(dt);
    }

    public override bool CanSensePlayer(EntityPlayer eplr, double range)
    {
        if (!friendlyTarget && AggressiveTargeting)
        {
            if (creatureHostility == EnumCreatureHostility.NeverHostile) return false;
            if (creatureHostility == EnumCreatureHostility.Passive && (bhEmo == null || (!IsInEmotionState("aggressiveondamage") && !IsInEmotionState("aggressivearoundentities")))) return false;
        }

        float rangeMul = eplr.Stats.GetBlended("animalSeekingRange");
        IPlayer player = eplr.Player;

        // Sneaking reduces the detection range
        if (eplr.Controls.Sneak && eplr.OnGround)
        {
            rangeMul *= 0.6f;
        }

        EntityPos correctedToDimensionPos = eplr.Pos.Copy();
        correctedToDimensionPos.Dimension = CurrentDimension;

        if ((rangeMul == 1 || entity.ServerPos.DistanceTo(correctedToDimensionPos) < range * rangeMul)
            && targetablePlayerMode(player)) return true;

        return false;
    }

    protected double dimensionOffset(int fromDimension, int toDimension) => (toDimension - fromDimension) * BlockPos.DimensionBoundary;

    protected override bool hasDirectContact(Entity targetEntity, float minDist, float minVerDist)
    {
        EntityPos correctedToDimensionPos = targetEntity.Pos.Copy();
        correctedToDimensionPos.Dimension = entity.Pos.Dimension;

        Cuboidd targetBox = targetEntity.SelectionBox.ToDouble().Translate(targetEntity.ServerPos.X, targetEntity.ServerPos.Y, targetEntity.ServerPos.Z);
        tmpPos.Set(entity.ServerPos).Add(0, entity.SelectionBox.Y2 / 2, 0).Ahead(entity.SelectionBox.XSize / 2, 0, entity.ServerPos.Yaw);
        double dist = targetBox.ShortestDistanceFrom(tmpPos);
        double vertDist = Math.Abs(targetBox.ShortestVerticalDistanceFrom(tmpPos.Y));
        if (dist >= minDist || vertDist >= minVerDist) return false;

        rayTraceFrom.Set(entity.ServerPos);
        rayTraceFrom.Y += 1 / 32f;
        rayTraceTo.Set(correctedToDimensionPos);
        rayTraceTo.Y += 1 / 32f;
        bool directContact = false;

        entity.World.RayTraceForSelection(this, rayTraceFrom, rayTraceTo, ref blockSel, ref entitySel);
        directContact = blockSel == null;

        if (!directContact)
        {
            rayTraceFrom.Y += entity.SelectionBox.Y2 * 7 / 16f;
            rayTraceTo.Y += targetEntity.SelectionBox.Y2 * 7 / 16f;
            entity.World.RayTraceForSelection(this, rayTraceFrom, rayTraceTo, ref blockSel, ref entitySel);
            directContact = blockSel == null;
        }

        if (!directContact)
        {
            rayTraceFrom.Y += entity.SelectionBox.Y2 * 7 / 16f;
            rayTraceTo.Y += targetEntity.SelectionBox.Y2 * 7 / 16f;
            entity.World.RayTraceForSelection(this, rayTraceFrom, rayTraceTo, ref blockSel, ref entitySel);
            directContact = blockSel == null;
        }

        if (!directContact) return false;

        return true;
    }

    protected void timeSwitchToNormalWorld()
    {
        Timeswitch? system = entity.Api.ModLoader.GetModSystem<Timeswitch>();

        if (entity.ServerPos.Dimension != Dimensions.NormalWorld)
        {
            (entity as EntityErel)?.ChangeDimension(Dimensions.NormalWorld);
            system?.ChangeEntityDimensionOnClient(entity, Dimensions.NormalWorld);
        }
    }
}
