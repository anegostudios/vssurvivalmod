using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.ServerMods;

namespace Vintagestory.GameContent
{
    public class BlockMetalSpikes : Block
    {
        public override void OnEntityInside(IWorldAccessor world, Entity entity, BlockPos pos)
        {
            if (world.Side == EnumAppSide.Server && entity is EntityAgent && (entity as EntityAgent).ServerControls.Sprint && entity.ServerPos.Motion.LengthSq() > 0.001)
            {
                if (world.Rand.NextDouble() > 0.05)
                {
                    entity.ReceiveDamage(new DamageSource() { Source = EnumDamageSource.Block, sourceBlock = this, Type = EnumDamageType.PiercingAttack, sourcePos = pos.ToVec3d() }, 1);
                    entity.ServerPos.Motion.Set(0, 0, 0);
                }
            }
            base.OnEntityInside(world, entity, pos);
        }

        public override void OnEntityCollide(IWorldAccessor world, Entity entity, BlockPos pos, BlockFacing facing, Vec3d collideSpeed, bool isImpact)
        {
            if (world.Side == EnumAppSide.Server && isImpact && Math.Abs(collideSpeed.Y * 30) >= 0.25)
            {
                if (entity.Code.Path == "locust") return;

                entity.ReceiveDamage(
                    new DamageSource() { Source = EnumDamageSource.Block, sourceBlock = this, Type = EnumDamageType.PiercingAttack, sourcePos = pos.ToVec3d() },
                    (float)Math.Abs(collideSpeed.Y * 30)
                );
            }
        }
    }
}
