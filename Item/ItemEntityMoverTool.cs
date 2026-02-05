using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent;

public class ItemEntityMoverTool : Item
{
    protected const string distanceAttribute = "distance";
    protected const string offsetAttribute = "offset";
    protected const string entityIdAttribute = "entity-id";
    protected readonly Dictionary<long, long> affectedEntities = [];



    public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
    {
        if (byEntity.Api.Side == EnumAppSide.Client)
        {
            OnHeldInteractStartClient(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
        }
        else
        {
            OnHeldInteractStartServer(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
        }
    }

    public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
    {
        if (slot.Itemstack?.Item == null)
        {
            return false;
        }

        if (byEntity.Api.Side == EnumAppSide.Server)
        {
            OnHeldInteractStepServer(secondsUsed, slot, byEntity, blockSel, entitySel);
        }

        return true;
    }

    public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
    {
        if (byEntity.Api.Side == EnumAppSide.Server)
        {
            OnHeldInteractStopServer(secondsUsed, slot, byEntity, blockSel, entitySel);
        }
    }



    protected virtual void OnHeldInteractStartClient(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
    {
        if (entitySel?.Entity != null && slot.Itemstack != null)
        {
            handling = EnumHandHandling.PreventDefaultAction;
        }
    }

    protected virtual void OnHeldInteractStartServer(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
    {
        if (entitySel.Entity == null || slot.Itemstack == null)
        {
            return;
        }

        handling = EnumHandHandling.PreventDefaultAction;

        double distance = byEntity.Pos.Add(byEntity.LocalEyePos.ToVec3f()).DistanceTo(entitySel.Entity.Pos.XYZ);
        slot.Itemstack.Attributes.SetDouble(distanceAttribute, distance);

        Vec3d offset = entitySel.Entity.Pos.XYZ - GetViewPosition(byEntity, distance);
        slot.Itemstack.Attributes.SetDouble(offsetAttribute + "X", offset.X);
        slot.Itemstack.Attributes.SetDouble(offsetAttribute + "Y", offset.Y);
        slot.Itemstack.Attributes.SetDouble(offsetAttribute + "Z", offset.Z);

        slot.Itemstack.Attributes.SetLong(entityIdAttribute, entitySel.Entity.EntityId);

        if (affectedEntities.TryGetValue(byEntity.EntityId, out long targetId))
        {
            Entity? previousTarget = byEntity.Api.World.GetEntityById(targetId);
            if (previousTarget != null)
            {
                StopDisableEntityMovement(previousTarget);
            }
            affectedEntities.Remove(byEntity.EntityId);
        }

        StartDisableEntityMovement(entitySel.Entity);
        affectedEntities.Add(byEntity.EntityId, entitySel.Entity.EntityId);
    }

    protected virtual void OnHeldInteractStepServer(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
    {
        if (slot.Itemstack == null)
        {
            return;
        }

        double distance = slot.Itemstack.Attributes.GetDouble(distanceAttribute);
        Vec3d offset = new();
        offset.X = slot.Itemstack.Attributes.GetDouble(offsetAttribute + "X");
        offset.Y = slot.Itemstack.Attributes.GetDouble(offsetAttribute + "Y");
        offset.Z = slot.Itemstack.Attributes.GetDouble(offsetAttribute + "Z");

        Vec3d targetPosition = GetViewPosition(byEntity, distance);

        byEntity.Api.World.SpawnParticles(1, ColorUtil.WhiteArgb, targetPosition, targetPosition, new(), new(), 0.1f, 0, scale: 0.5f, model: EnumParticleModel.Cube);

        long entityId = slot.Itemstack.Attributes.GetLong(entityIdAttribute);

        Entity target = byEntity.Api.World.GetEntityById(entityId);

        target?.Pos.SetPos(targetPosition);

        target?.GetBehavior<EntityBehaviorControlledPhysics>()?.SetState(new(targetPosition.X, targetPosition.Y, targetPosition.Z), 0);

        target?.GetBehavior<EntityBehaviorTaskAI>()?.TaskManager?.StopTasks();
    }

    protected virtual void OnHeldInteractStopServer(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
    {
        if (affectedEntities.TryGetValue(byEntity.EntityId, out long targetId))
        {
            Entity? target = byEntity.Api.World.GetEntityById(targetId);
            if (target != null)
            {
                StopDisableEntityMovement(target);
            }
            affectedEntities.Remove(byEntity.EntityId);
        }
    }

    protected virtual void StartDisableEntityMovement(Entity target)
    {
        if (target.GetBehavior<EntityBehaviorTaskAI>() is EntityBehaviorTaskAI aiTaskBehavior)
        {
            aiTaskBehavior.TaskManager.OnShouldExecuteTask += PreventAiTask;
        }
        if (target.GetBehavior<EntityBehaviorControlledPhysics>() is EntityBehaviorControlledPhysics physicsBehavior)
        {
            physicsBehavior.EnableModulesApplication = false;
        }
    }

    protected virtual void StopDisableEntityMovement(Entity target)
    {
        if (target.GetBehavior<EntityBehaviorTaskAI>() is EntityBehaviorTaskAI aiTaskBehavior)
        {
            aiTaskBehavior.TaskManager.OnShouldExecuteTask -= PreventAiTask;
        }
        if (target.GetBehavior<EntityBehaviorControlledPhysics>() is EntityBehaviorControlledPhysics physicsBehavior)
        {
            physicsBehavior.EnableModulesApplication = true;
        }
    }

    protected virtual bool PreventAiTask(IAiTask task)
    {
        return false;
    }

    protected virtual Vec3d GetViewPosition(EntityAgent byEntity, double distance)
    {
        Vec3d eyePos = byEntity.Pos.XYZ.Add(byEntity.LocalEyePos.ToVec3f());

        return eyePos + byEntity.Pos.GetViewVector().ToVec3d().Normalize() * distance;
    }
}
