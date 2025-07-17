using Newtonsoft.Json;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace Vintagestory.GameContent;

// @TODO add description
/// <summary>
/// 
/// </summary>
[JsonObject(MemberSerialization = MemberSerialization.OptIn)]
public class AiTaskFollowLeadHolderConfig : AiTaskBaseTargetableConfig
{
    /// <summary>
    /// Entity moving speed.
    /// </summary>
    [JsonProperty] public float MoveSpeed = 0.3f;

    /// <summary>
    /// Min generation of entity for this task to start.<br/>
    /// If entity has attribute 'tamed' set to 'true', then its generation is considered 10 higher.
    /// </summary>
    [JsonProperty] public int MinGeneration = 0;

    /// <summary>
    /// Cooldown between successfully reaching target and new attempt of task execution.
    /// </summary>
    [JsonProperty] public int GoalReachedCooldownMs = 1000;

    /// <summary>
    /// Min distance to target from which start to walk towards target.
    /// </summary>
    [JsonProperty] public float MaxDistanceToTarget = 2;

    /// <summary>
    /// Extra distance to target, at which target is considered reached. It is added to average entity and target sizes.
    /// </summary>
    [JsonProperty] public float ExtraMinDistanceToTarget = 1f;

    /// <summary>
    /// Affects pathfinding, see <see cref="EnumAICreatureType"/>.
    /// </summary>
    [JsonProperty] public EnumAICreatureType AiCreatureType = EnumAICreatureType.Default;
}

public class AiTaskFollowLeadHolderR : AiTaskBaseTargetableR
{
    private AiTaskFollowLeadHolderConfig Config => GetConfig<AiTaskFollowLeadHolderConfig>();

    protected ClothManager clothManager;
    protected EntityBehaviorRopeTieable? ropeTieableBehavior;

    protected long lastGoalReachedMs;
    protected ClothSystem? clothSystem;

    public AiTaskFollowLeadHolderR(EntityAgent entity, JsonObject taskConfig, JsonObject aiConfig) : base(entity, taskConfig, aiConfig)
    {
        clothManager = entity.World.Api.ModLoader.GetModSystem<ClothManager>();
        ropeTieableBehavior = entity.GetBehavior<EntityBehaviorRopeTieable>();

        baseConfig = LoadConfig<AiTaskFollowLeadHolderConfig>(entity, taskConfig, aiConfig);
    }

    public override void AfterInitialize()
    {
        base.AfterInitialize();

        ropeTieableBehavior = entity.GetBehavior<EntityBehaviorRopeTieable>();
    }

    public override bool ShouldExecute()
    {
        if (!PreconditionsSatisficed()) return false;

        if (GetOwnGeneration() < Config.MinGeneration) return false;
        if (entity.World.ElapsedMilliseconds - lastGoalReachedMs < Config.GoalReachedCooldownMs) return false;

        int[]? clothIds = ropeTieableBehavior?.ClothIds?.value;

        if (clothIds == null) return false;

        foreach (int clothId in clothIds)
        {
            clothSystem = clothManager.GetClothSystem(clothId);
            if (clothSystem == null) continue;

            ClothPoint? point = GetPinnedToPoint(entity);
            if (point != null)
            {
                targetEntity = point.PinnedToEntity;
                if (targetEntity.ServerPos.DistanceTo(entity.ServerPos) < Config.MaxDistanceToTarget) return false;
                if (!IsTargetableEntity(targetEntity, Config.SeekingRange)) return false;
                return true;
            }
        }

        return false;
    }

    public override void StartExecute()
    {
        if (targetEntity == null) return;

        pathTraverser.WalkTowards(targetEntity.ServerPos.XYZ, Config.MoveSpeed, MinDistanceToTarget(Config.ExtraMinDistanceToTarget), OnGoalReached, OnStuck, Config.AiCreatureType);

        base.StartExecute();
    }

    public override bool ContinueExecute(float dt)
    {
        if (targetEntity == null || clothSystem == null) return false;
        if (!ContinueExecute(dt)) return false;

        float minDistance = MinDistanceToTarget();
        double distance = targetEntity.ServerPos.DistanceTo(entity.ServerPos.XYZ);

        if (distance > Config.MaxDistanceToTarget)
        {
            pathTraverser.WalkTowards(targetEntity.ServerPos.XYZ, Config.MoveSpeed, minDistance, OnGoalReached, OnStuck, Config.AiCreatureType);
        }

        if (distance < minDistance) return false;

        ClothPoint? point = GetPinnedToPoint(entity);
        if (point == null) return false;

        return true;
    }

    protected virtual void OnGoalReached()
    {
        lastGoalReachedMs = entity.World.ElapsedMilliseconds;
    }

    protected virtual void OnStuck()
    {

    }

    protected virtual ClothPoint? GetPinnedToPoint(EntityAgent entity)
    {
        if (clothSystem == null) return null;

        if (clothSystem.FirstPoint.PinnedToEntity != null && clothSystem.FirstPoint.PinnedToEntity.EntityId != entity.EntityId)
        {
            return clothSystem.FirstPoint;
        }
        if (clothSystem.LastPoint.PinnedToEntity != null && clothSystem.LastPoint.PinnedToEntity.EntityId != entity.EntityId)
        {
            return clothSystem.LastPoint;
        }

        return null;
    }
}
