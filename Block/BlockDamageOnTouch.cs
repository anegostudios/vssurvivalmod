using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

#nullable disable
namespace Vintagestory.GameContent;

public class BlockPlantDamageOnTouch : BlockDamageOnTouch
{
    public override bool TryPlaceBlockForWorldGen(IBlockAccessor blockAccessor, BlockPos pos, BlockFacing onBlockFace, IRandom worldgenRandom, BlockPatchAttributes attributes = null)
    {
        Block blockBelow = blockAccessor.GetBlockBelow(pos);
        return blockBelow.Fertility > 0 && base.TryPlaceBlockForWorldGen(blockAccessor, pos, onBlockFace, worldgenRandom, attributes);
    }
}

public class BlockDamageOnTouch : Block
{
    protected float sprintIntoDamage = 1;
    protected float fallIntoDamageMul = 30;
    protected HashSet<AssetLocation> immuneCreatures = new();
    protected EnumDamageType damageType = EnumDamageType.PiercingAttack;
    protected int damageTier = 0;
    protected double collisionSpeedThreshold = 0.3;
    protected double onEntityInsideDamageProbability = 0.2;

    public override void OnLoaded(ICoreAPI api)
    {
        base.OnLoaded(api);
        sprintIntoDamage = Attributes["sprintIntoDamage"].AsFloat(1);
        fallIntoDamageMul = Attributes["fallIntoDamageMul"].AsFloat(15);
        immuneCreatures = new(Attributes["immuneCreatures"].AsObject(Array.Empty<AssetLocation>(), Code.Domain));
        damageType = Enum.Parse<EnumDamageType>(Attributes["damageType"].AsString("PiercingAttack"));
        damageTier = Attributes["damageTier"].AsInt(0);
        collisionSpeedThreshold = Attributes["collisionSpeedThreshold"].AsFloat(0.3f);
        onEntityInsideDamageProbability = Attributes["onEntityInsideDamageProbability"].AsFloat(0.2f);
    }

    public override void OnEntityInside(IWorldAccessor world, Entity entity, BlockPos pos)
    {
        if (world.Side == EnumAppSide.Server && entity is EntityAgent && (entity as EntityAgent).ServerControls.Sprint && entity.Pos.Motion.LengthSq() > 0.001)
        {
            if (immuneCreatures.Contains(entity.Code)) return;

            if (world.Rand.NextDouble() < onEntityInsideDamageProbability)
            {
                entity.ReceiveDamage(new DamageSource() { Source = EnumDamageSource.Block, SourceBlock = this, Type = EnumDamageType.PiercingAttack, SourcePos = pos.ToVec3d() }, sprintIntoDamage);
                entity.Pos.Motion.Set(0, 0, 0);
            }
        }
        base.OnEntityInside(world, entity, pos);
    }

    public override void OnEntityCollide(IWorldAccessor world, Entity entity, BlockPos pos, BlockFacing facing, Vec3d collideSpeed, bool isImpact)
    {
        if (world.Side == EnumAppSide.Server && isImpact && -collideSpeed.Y >= collisionSpeedThreshold)
        {
            if (immuneCreatures.Contains(entity.Code)) return;

            entity.ReceiveDamage(
                new DamageSource() { Source = EnumDamageSource.Block, SourceBlock = this, Type = damageType, DamageTier = damageTier, SourcePos = pos.ToVec3d() },
                (float)Math.Abs(collideSpeed.Y * fallIntoDamageMul)
            );
        }
    }
}
