using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent;

public class BlockBehaviorDamageOnTouch(Block block) : BlockBehavior(block)
{

    /// <summary>
    /// How much damage to apply when the entity sprints while touching the block
    /// </summary>
    [DocumentAsJson("Optional", "1")]
    public float SprintIntoDamage = 1;

    /// <summary>
    /// How much to multiply the speed the entity collided with the block to determine the damage
    /// </summary>
    [DocumentAsJson("Optional", "15")]
    public float FallIntoDamageMul = 15;

    /// <summary>
    /// Entities with any of these codes are immune to this damage
    /// </summary>
    [DocumentAsJson("Optional", "")]
    public HashSet<AssetLocation>? ImmuneCreatures;

    /// <summary>
    /// Entities with any of these tags are immune to this damage
    /// </summary>
    [DocumentAsJson("Optional", "")]
    [JsonProperty] public TagSetFast? ImmuneTags;

    /// <summary>
    /// The type of damage to apply
    /// </summary>
    [DocumentAsJson("Optional", "PiercingAttack")]
    public EnumDamageType DamageType = EnumDamageType.PiercingAttack;

    /// <summary>
    /// The damage tier inflicted
    /// </summary>
    [DocumentAsJson("Optional", "0")]
    public int DamageTier = 0;

    /// <summary>
    /// How fast an entity needs to be falling to take damage from the block
    /// </summary>
    [DocumentAsJson("Optional", "0.3")]
    public double CollisionSpeedThreshold = 0.3;

    /// <summary>
    /// The probability that the entity will take damage while it's inside the block
    /// </summary>
    [DocumentAsJson("Optional", "0.2")]
    public double OnEntityInsideDamageProbability = 0.2;

    private JsonObject? rawTags;

    public override void Initialize(JsonObject properties)
    {
        base.Initialize(properties);
        SprintIntoDamage = properties["sprintIntoDamage"].AsFloat(SprintIntoDamage);
        FallIntoDamageMul = properties["fallIntoDamageMul"].AsFloat(FallIntoDamageMul);
        ImmuneCreatures = properties["immuneCreatures"].AsObject(ImmuneCreatures, block.Code.Domain);
        rawTags = properties["immuneTags"];
        DamageType = Enum.Parse<EnumDamageType>(properties["damageType"].AsString("PiercingAttack"));
        DamageTier = properties["damageTier"].AsInt(DamageTier);
        CollisionSpeedThreshold = properties["collisionSpeedThreshold"].AsFloat((float)CollisionSpeedThreshold);
        OnEntityInsideDamageProbability = properties["onEntityInsideDamageProbability"].AsFloat((float)OnEntityInsideDamageProbability);
    }

    public override void OnLoaded(ICoreAPI api)
    {
        base.OnLoaded(api);
        if (rawTags != null && rawTags.Exists)
        {
            ImmuneTags = api.EntityTagRegistry.CreateTagSet(rawTags.AsArray<string>()!);
        }
    }

    public override void OnEntityInside(IWorldAccessor world, Entity entity, BlockPos pos, ref EnumHandling handling)
    {
        if (world.Side != EnumAppSide.Server || entity is not EntityAgent entityAgent || !entityAgent.ServerControls.Sprint || entityAgent.Pos.Motion.LengthSq() <= 0.001) return;

        if (ImmuneTags?.Overlaps(entityAgent.Tags) == true) return;
        if (ImmuneCreatures?.Contains(entityAgent.Code) == true) return;

        if (world.Rand.NextDouble() >= OnEntityInsideDamageProbability) return;

        entityAgent.ReceiveDamage(new DamageSource
        {
            Source = EnumDamageSource.Block,
            SourceBlock = block,
            Type = DamageType,
            DamageTier = DamageTier,
            SourcePos = pos.ToVec3d()
        }, SprintIntoDamage);

        entityAgent.Pos.Motion.Set(0, 0, 0);
    }

    public override void OnEntityCollide(IWorldAccessor world, Entity entity, BlockPos pos, BlockFacing facing, Vec3d collideSpeed, bool isImpact, ref EnumHandling handling)
    {
        if (world.Side != EnumAppSide.Server || !isImpact || -collideSpeed.Y < CollisionSpeedThreshold) return;

        if (ImmuneTags?.Overlaps(entity.Tags) == true) return;
        if (ImmuneCreatures?.Contains(entity.Code) == true) return;

        entity.ReceiveDamage(
            new DamageSource
            {
                Source = EnumDamageSource.Block,
                SourceBlock = block,
                Type = DamageType,
                DamageTier = DamageTier,
                SourcePos = pos.ToVec3d()
            }, (float)Math.Abs(collideSpeed.Y * FallIntoDamageMul)
        );
    }

}
