using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class AiTaskEidolonMeleeAttack : AiTaskMeleeAttack
    {
        public AiTaskEidolonMeleeAttack(EntityAgent entity) : base(entity)
        {
            attackRange = 4.25f;
            turnToTarget = false;
        }


        protected override void attackTarget()
        {
            var pos = entity.Pos.XYZ;
            // Damage and knockback all nearby creatures
            partitionUtil.WalkEntities(pos, 6f, (e) =>
            {
                if (e.EntityId == entity.EntityId || !e.IsInteractable) return true;
                if (!e.Alive) return true;
                if (!hasDirectContact(e, minDist, minVerDist)) return true;

                e.ReceiveDamage(
                    new DamageSource()
                    {
                        Source = EnumDamageSource.Entity,
                        SourceEntity = entity,
                        Type = damageType,
                        DamageTier = damageTier,
                        KnockbackStrength = knockbackStrength / 2f
                    },
                    damage * GlobalConstants.CreatureDamageModifier
                );

                return true;
            }, EnumEntitySearchType.Creatures);


            
        }

        protected override bool hasDirectContact(Entity targetEntity, float minDist, float minVerDist)
        {
            Cuboidd targetBox = targetEntity.SelectionBox.ToDouble().Translate(targetEntity.ServerPos.X, targetEntity.ServerPos.Y, targetEntity.ServerPos.Z);
            tmpPos.Set(entity.ServerPos).Add(0, entity.SelectionBox.Y2 / 2, 0).Ahead(entity.SelectionBox.XSize / 2, 0, entity.ServerPos.Yaw);
            double dist = targetBox.ShortestDistanceFrom(tmpPos);
            double vertDist = Math.Abs(targetBox.ShortestVerticalDistanceFrom(tmpPos.Y));
            if (dist >= minDist || vertDist >= minVerDist) return false;

            return true;
        }
    }
}
