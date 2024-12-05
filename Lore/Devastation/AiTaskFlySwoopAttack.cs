using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class AiTaskFlySwoopAttack : AiTaskBaseTargetable
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
        protected Vec3d targetPos;
        protected Vec3d beginAttackPos;
        protected List<Vec3d> swoopPath;


        public AiTaskFlySwoopAttack(EntityAgent entity) : base(entity)
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
        public override bool ShouldExecute() {
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

            bool targetOk = targetEntity != null && entity.ServerPos.Y - targetEntity.ServerPos.Y > 9 && entity.ServerPos.HorDistanceTo(targetEntity.ServerPos) > 25;
            if (!targetOk) return false;

            beginAttackPos = entity.ServerPos.XYZ;
            swoopPath = new List<Vec3d>(getSwoopPath(targetEntity as EntityAgent, 35, false));
            return pathClear(swoopPath);
        }

        private bool pathClear(List<Vec3d> swoopPath)
        {
            int skipPoints = 2;
            Vec3d tmppos = new();

            for (int i = 0; i < swoopPath.Count; i+=skipPoints)
            {
                tmppos.Set(swoopPath[i]);
                tmppos.Y--;
                if (world.CollisionTester.IsColliding(entity.World.BlockAccessor, entity.CollisionBox, tmppos))
                {
#if DEBUG
                    var zero = Vec3f.Zero;
                    for (int k = i; k < swoopPath.Count; k++)
                    {
                        entity.World.SpawnParticles(1, ColorUtil.ColorFromRgba(0,0,255,255), swoopPath[k], swoopPath[k], zero, zero, 3, 0, 1);
                    }
#endif
                    return false;
                }
            }

            return true;
        }

        public override void StartExecute()
        {
            didDamageEntity.Clear();
            targetPos = targetEntity.ServerPos.XYZ;

            swoopPath.Clear();
            swoopPath.AddRange(getSwoopPath(targetEntity as EntityAgent, 35, true));
            pathTraverser.FollowRoute(swoopPath, moveSpeed, 8, null, null);

            execAccum = 0;
            refreshAccum = 0;

            base.StartExecute();
        }

        float refreshAccum = 0;
        float execAccum = 0;
        float damageAccum = 0f;

        public override bool ContinueExecute(float dt)
        {
            execAccum += dt;
            refreshAccum += dt;
            if (refreshAccum > 1 && execAccum < 5)
            {
                var path = getSwoopPath(targetEntity as EntityAgent, 35, true);
                if (pathClear(new List<Vec3d>(path)))
                {
                    swoopPath.Clear();
                    swoopPath.AddRange(path);
                }                
                refreshAccum = 0;
            }

            var nowdir = targetPos - entity.ServerPos.XYZ;
            double distance = nowdir.Length();
            if (distance > Math.Max(seekingRangeHor, seekingRangeVer) * 2) return false;

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

           
                foreach (var attackEntity in attackableEntities) {
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
                        
            return pathTraverser.Active && targetEntity.Pos.Dimension == entity.Pos.Dimension;
        }

        public override void FinishExecute(bool cancelled)
        {
            pathTraverser.Stop();

            base.FinishExecute(cancelled);
        }


        protected virtual Vec3d[] getSwoopPath(Entity target, int its, bool simplifiedOut)
        {
            var targetPos = target.ServerPos.XYZ.AddCopy(target.LocalEyePos);

            // Swoop in
            var start1 = entity.ServerPos.XYZ;
            var end1 = new Vec3d(start1.X, targetPos.Y + 10, start1.Z);

            var start2 = end1;
            var end2 = targetPos;


            var delta1 = end1 - start1;
            var delta2 = end2 - start2;

            int outits = (simplifiedOut ? its / 3 : its);
            Vec3d[] points = new Vec3d[its + outits];

            for (int i = 0; i < its; i++)
            {
                double p = (double)i / its;

                var mid1 = start1 + p * delta1;
                var mid2 = start2 + p * delta2;

                points[i] = (1 - p) * mid1 + p * mid2;
            }

            // Swoop out
            start1 = points[its - 1];
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

                points[its+i] = (1 - p) * mid1 + p * mid2;
            }

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
