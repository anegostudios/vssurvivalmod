using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
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
        float sprintIntoDamage = 1;
        float fallIntoDamageMul = 30;
        HashSet<AssetLocation> immuneCreatures;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            sprintIntoDamage = Attributes["sprintIntoDamage"].AsFloat(1);
            fallIntoDamageMul = Attributes["fallIntoDamageMul"].AsFloat(30);
            immuneCreatures = new HashSet<AssetLocation>(Attributes["immuneCreatures"].AsObject<AssetLocation[]>(new AssetLocation[0], this.Code.Domain));
        }

        public override void OnEntityInside(IWorldAccessor world, Entity entity, BlockPos pos)
        {
            if (world.Side == EnumAppSide.Server && entity is EntityAgent && (entity as EntityAgent).ServerControls.Sprint && entity.ServerPos.Motion.LengthSq() > 0.001)
            {
                if (immuneCreatures.Contains(entity.Code)) return;

                if (world.Rand.NextDouble() > 0.05)
                {
                    entity.ReceiveDamage(new DamageSource() { Source = EnumDamageSource.Block, SourceBlock = this, Type = EnumDamageType.PiercingAttack, SourcePos = pos.ToVec3d() }, sprintIntoDamage);
                    entity.ServerPos.Motion.Set(0, 0, 0);
                }
            }
            base.OnEntityInside(world, entity, pos);
        }

        public override void OnEntityCollide(IWorldAccessor world, Entity entity, BlockPos pos, BlockFacing facing, Vec3d collideSpeed, bool isImpact)
        {
            if (world.Side == EnumAppSide.Server && isImpact && Math.Abs(collideSpeed.Y * 30) >= 0.25)
            {
                if (immuneCreatures.Contains(entity.Code)) return;

                entity.ReceiveDamage(
                    new DamageSource() { Source = EnumDamageSource.Block, SourceBlock = this, Type = EnumDamageType.PiercingAttack, SourcePos = pos.ToVec3d() },
                    (float)Math.Abs(collideSpeed.Y * fallIntoDamageMul)
                );
            }
        }
    }
}
