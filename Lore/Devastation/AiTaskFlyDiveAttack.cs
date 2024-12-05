using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class AiTaskFlyDiveAttack : AiTaskBaseTargetable
    {
        public EnumDamageType damageType = EnumDamageType.BluntAttack;
        public int damageTier = 0;

        protected long lastCheckOrAttackMs;
        protected float damage = 2f;
        protected float knockbackStrength = 1f;
        protected float seekingRangeVer = 25f;
        protected float seekingRangeHor = 25f;
        protected float damageRange = 5f;
        protected float moveSpeed = 0.04f;
        protected HashSet<long> didDamageEntity = new HashSet<long>();
        protected EntityPos targetPos;
        protected Vec3d beginAttackPos;

        protected float diveRange = 20;
        protected float requireMinRange = 30;

        public bool Enabled = true;

        public AiTaskFlyDiveAttack(EntityAgent entity) : base(entity)
        {
        }

        public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig)
        {
            base.LoadConfig(taskConfig, aiConfig);

            moveSpeed = taskConfig["moveSpeed"].AsFloat(0.04f);
            damage = taskConfig["damage"].AsFloat(2);
            knockbackStrength = taskConfig["knockbackStrength"].AsFloat(GameMath.Sqrt(damage / 2f));
            seekingRangeHor = taskConfig["seekingRangeHor"].AsFloat(25);
            seekingRangeVer = taskConfig["seekingRangeVer"].AsFloat(25);
            damageRange = taskConfig["damageRange"].AsFloat(2);
            string strdt = taskConfig["damageType"].AsString();
            if (strdt != null)
            {
                this.damageType = (EnumDamageType)Enum.Parse(typeof(EnumDamageType), strdt, true);
            }
            this.damageTier = taskConfig["damageTier"].AsInt(0);
        }

        public override void OnEntityLoaded() { }
        public override bool ShouldExecute()
        {
            if (!Enabled) return false;

            long ellapsedMs = entity.World.ElapsedMilliseconds;
            if (cooldownUntilMs > ellapsedMs)
            {
                return false;
            }

            // Don't try too often
            cooldownUntilMs = entity.World.ElapsedMilliseconds + 1500;

            if (!PreconditionsSatisifed()) return false;

            Vec3d pos = entity.ServerPos.XYZ.Add(0, entity.SelectionBox.Y2 / 2, 0).Ahead(entity.SelectionBox.XSize / 2, 0, entity.ServerPos.Yaw);

            if (entity.World.ElapsedMilliseconds - attackedByEntityMs > 30000)
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
            }
            
            lastCheckOrAttackMs = entity.World.ElapsedMilliseconds;

            bool targetOk = targetEntity != null && entity.ServerPos.Y - targetEntity.ServerPos.Y > 9 && entity.ServerPos.HorDistanceTo(targetEntity.ServerPos) > 20;
            return targetOk;
        }


        public override void StartExecute()
        {
            didDamageEntity.Clear();
            targetPos = targetEntity.ServerPos;
            diving = false;
            impact = false;
            base.StartExecute();
        }

        float damageAccum = 0f;
        bool diving = false;
        bool impact = false;

        public override bool ContinueExecute(float dt)
        {
            // Update target position for as long as the target is in the same dimension
            if (targetEntity.Pos.Dimension == entity.Pos.Dimension)
            {
                targetPos = targetEntity.ServerPos;
            }

            if (!impact)
            {
                var hordist = entity.ServerPos.HorDistanceTo(targetPos);
                if (!diving && entity.ServerPos.Y - targetPos.Y < hordist * 1.35f)
                {
                    entity.ServerPos.Motion.Y = 0.15f;
                    entity.ServerPos.Motion.X *= 0.9f;
                    entity.ServerPos.Motion.Z *= 0.9f;
                    return true;
                }

                if (!diving)
                {
                    entity.AnimManager.StopAnimation("fly-idle");
                    entity.AnimManager.StopAnimation("fly-flapactive");
                    entity.AnimManager.StopAnimation("fly-flapcruise");
                    entity.AnimManager.StartAnimation("dive");
                }

                diving = true;


                var offs = (targetPos.XYZ - entity.ServerPos.XYZ);
                var dir = offs.Normalize();
                entity.ServerPos.Motion.X = dir.X * moveSpeed * 10;
                entity.ServerPos.Motion.Y = dir.Y * moveSpeed * 10;
                entity.ServerPos.Motion.Z = dir.Z * moveSpeed * 10;

                double speed = entity.ServerPos.Motion.Length();
                entity.ServerPos.Roll = (float)Math.Asin(GameMath.Clamp(-dir.Y / speed, -1, 1));
                entity.ServerPos.Yaw = (float)Math.Atan2(offs.X, offs.Z);


                damageAccum += dt;
                if (damageAccum > 0.2f)
                {
                    damageAccum = 0;

                    List<Entity> attackableEntities = new List<Entity>();
                    var ep = entity.Api.ModLoader.GetModSystem<EntityPartitioning>();
                    ep.GetNearestEntity(entity.ServerPos.XYZ, damageRange + 1, (e) =>
                    {
                        if (IsTargetableEntity(e, damageRange) && hasDirectContact(e, damageRange, damageRange) && !didDamageEntity.Contains(entity.EntityId))
                        {
                            attackableEntities.Add(e);
                        }
                        return false;
                    }, EnumEntitySearchType.Creatures);


                    foreach (var attackEntity in attackableEntities)
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


                if (entity.Collided)
                {
                    entity.AnimManager.StopAnimation("dive");
                    entity.AnimManager.StartAnimation("slam");
                    impact = true;
                }
            }

            if (impact)
            {
                entity.ServerPos.Roll = 0;
                entity.ServerPos.Motion.Set(0, 0, 0);
            }
            if (!impact) return true;

            var state = entity.AnimManager.GetAnimationState("slam");

            if (state != null && state.AnimProgress > 0.5f)
            {
                entity.AnimManager.StartAnimation("takeoff");
            }

            return state == null || state.AnimProgress < 0.6f;
        }

        public override void FinishExecute(bool cancelled)
        {
            pathTraverser.Stop();
            entity.AnimManager.StartAnimation("fly-flapactive-fast");

            base.FinishExecute(cancelled);
        }


        int approachPoints;
        protected virtual Vec3d[] getSwoopPath(Entity target, int its)
        {
            bool withDive = true;
            var targetPos = target.ServerPos.XYZ.AddCopy(target.LocalEyePos);
            var selfPos = entity.ServerPos.XYZ;

            // Approach
            var deltaVec = (targetPos - entity.ServerPos.XYZ);
            var approachDist = deltaVec.HorLength();
            var unitDist = deltaVec.Normalize();

            int apprinterval = 3;
            approachPoints = Math.Max(0, (int)((approachDist - diveRange*0.8f) / apprinterval));

            //int outits = 0;// (simplifiedOut ? its / 3 : its);
            Vec3d[] points = new Vec3d[(withDive ? its : 0) + approachPoints];

            // Get within 20 blocks first
            for (int i = 0; i < approachPoints; i++)
            {
                float p = (float)i / approachPoints;
                points[i] = new Vec3d(selfPos.X + unitDist.X*i*apprinterval, targetPos.Y + 30*p, selfPos.Z + unitDist.Z*i*apprinterval);
            }

            // Swoop in
            if (withDive)
            {
                var start1 = approachPoints <= 0 ? selfPos : points[approachPoints - 1];
                var end1 = new Vec3d(targetPos.X, selfPos.Y, targetPos.Z);

                var start2 = end1;
                var end2 = targetPos;

                var delta1 = end1 - start1;
                var delta2 = end2 - start2;


                for (int i = 0; i < its; i++)
                {
                    double p = (double)i / its;

                    var mid1 = start1 + p * delta1;
                    var mid2 = start2 + p * delta2;

                    points[approachPoints + i] = (1 - p) * mid1 + p * mid2;
                }
            }

            // Swoop out
            /*start1 = points[its - 1];
            var offs = (target.ServerPos.XYZ - entity.ServerPos.XYZ) * 1;
            end1 = new Vec3d(targetPos.X + offs.X, targetPos.Y, targetPos.Z + offs.Z);

            start2 = end1;
            end2 = new Vec3d(targetPos.X + offs.X * 1.3f, targetPos.Y + (beginAttackPos.Y - targetPos.Y) * 0.5f, targetPos.Z + offs.Z * 1.3f);

            delta1 = end1 - start1;
            delta2 = end2 - start2;


            for (int i = 0; i < outits; i++)
            {
                double p = (double)i / outits;

                var mid1 = start1 + p * delta1;
                var mid2 = start2 + p * delta2;

                points[approachPoints+its + i] = (1 - p) * mid1 + p * mid2;
            }
            */
#if DEBUG
            var zero = Vec3f.Zero;
            for (int i = 0; i < points.Length; i++)
            {
                entity.World.SpawnParticles(1, ColorUtil.WhiteArgb, points[i], points[i], zero, zero, 3, 0, 1);
            }
#endif

            return points;
        }
    }
}
