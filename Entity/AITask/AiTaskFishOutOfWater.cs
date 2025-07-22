using System;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent
{
    public class AiTaskFishOutOfWater : AiTaskBase
    {
        internal Vec3d targetPos = new Vec3d();
        protected float seekingRange = 2;

        public JsonObject taskConfig;
        private float moveSpeed = 0.03f;

        float searchWaterAccum;
        float outofWaterAccum;

        public float WanderRangeMul
        {
            get { return entity.Attributes.GetFloat("wanderRangeMul", 1); }
            set { entity.Attributes.SetFloat("wanderRangeMul", value); }
        }

        public int FailedConsecutivePathfinds
        {
            get { return entity.Attributes.GetInt("failedConsecutivePathfinds", 0); }
            set { entity.Attributes.SetInt("failedConsecutivePathfinds", value); }
        }

        NatFloat wanderRangeHorizontal = NatFloat.createStrongerInvexp(3, 40);
        NatFloat wanderRangeVertical = NatFloat.createStrongerInvexp(3, 10);


        public AiTaskFishOutOfWater(EntityAgent entity, JsonObject taskConfig, JsonObject aiConfig) : base(entity, taskConfig, aiConfig)
        {
            this.taskConfig = taskConfig;

            moveSpeed = taskConfig["movespeed"].AsFloat(0.03f);
        }

        public override bool ShouldExecute()
        {
            if (!entity.OnGround || entity.Swimming) return false;

            return true;
        }

        private Vec3d nearbyWaterOrRandomTarget()
        {
            int tries = 9;
            Vec4d bestTarget = null;
            Vec4d curTarget = new Vec4d();

            if (FailedConsecutivePathfinds > 10)
            {
                WanderRangeMul = Math.Max(0.1f, WanderRangeMul * 0.9f);
            }
            else
            {
                WanderRangeMul = Math.Min(1, WanderRangeMul * 1.1f);
                if (rand.NextDouble() < 0.05) WanderRangeMul = Math.Min(1, WanderRangeMul * 1.5f);
            }

            float wRangeMul = WanderRangeMul;
            double dx, dy, dz;

            if (rand.NextDouble() < 0.05) wRangeMul *= 3;

            while (tries-- > 0)
            {
                dx = wanderRangeHorizontal.nextFloat() * (rand.Next(2) * 2 - 1) * wRangeMul;
                dy = wanderRangeVertical.nextFloat() * (rand.Next(2) * 2 - 1) * wRangeMul;
                dz = wanderRangeHorizontal.nextFloat() * (rand.Next(2) * 2 - 1) * wRangeMul;

                curTarget.X = entity.ServerPos.X + dx;
                curTarget.Y = entity.ServerPos.InternalY + dy;
                curTarget.Z = entity.ServerPos.Z + dz;
                curTarget.W = 1;

                Block block;

                block = entity.World.BlockAccessor.GetBlockRaw((int)curTarget.X, (int)curTarget.Y, (int)curTarget.Z, BlockLayersAccess.Fluid);
                if (!block.IsLiquid()) curTarget.W = 0;
                else curTarget.W = 1 / Math.Sqrt((dx - 1.0) * (dx - 1.0) + (dz - 1.0) * (dz - 1.0) + 1);  //prefer target approx 1 block away

                if (bestTarget == null || curTarget.W > bestTarget.W)
                {
                    bestTarget = new Vec4d(curTarget.X, curTarget.Y, curTarget.Z, curTarget.W);
                    if (curTarget.W >= 1.0) break;  //have a good enough target, no need for further tries
                }
            }


            if (bestTarget.W > 0)
            {
                FailedConsecutivePathfinds = Math.Max(FailedConsecutivePathfinds - 3, 0);
                return bestTarget.XYZ;
            }

            FailedConsecutivePathfinds++;
            return null;
        }

        public override void StartExecute()
        {
            base.StartExecute();

            searchWaterAccum = 0;
            outofWaterAccum = 0;

            targetPos = nearbyWaterOrRandomTarget();
            if (targetPos != null)
            {
                pathTraverser.WalkTowards(targetPos, moveSpeed, 0.12f, OnGoalReached, OnStuck);
            }
        }

        private void OnStuck()
        {
            
        }

        private void OnGoalReached()
        {
        }

        public override bool 
            ContinueExecute(float dt)
        {
            //Check if time is still valid for task.
            if (!IsInValidDayTimeHours(false)) return false;

            if (entity.Swimming) return false;

            outofWaterAccum += dt;
            if (outofWaterAccum > 30)
            {
                entity.Die(EnumDespawnReason.Death, new DamageSource() { Type = EnumDamageType.Suffocation });
                return false;
            }

            if (targetPos == null)
            {
                searchWaterAccum += dt;
                if (searchWaterAccum >= 2)
                {
                    targetPos = nearbyWaterOrRandomTarget();
                    if (targetPos != null)
                    {
                        pathTraverser.WalkTowards(targetPos, moveSpeed, 0.12f, OnGoalReached, OnStuck);
                    }
                    searchWaterAccum = 0;
                }
            }

            if (targetPos != null && world.Rand.NextDouble() < 0.2)
            {
                pathTraverser.CurrentTarget.X = targetPos.X;
                pathTraverser.CurrentTarget.Y = targetPos.Y;
                pathTraverser.CurrentTarget.Z = targetPos.Z;
            }
            return true;
        }

        public override void FinishExecute(bool cancelled)
        {
            pathTraverser.Stop();
            base.FinishExecute(cancelled);
        }


    }
}
