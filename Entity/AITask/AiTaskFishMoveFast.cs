using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API;
using Vintagestory.API.Server;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Client;
using Vintagestory.API.Datastructures;

namespace Vintagestory.GameContent
{
    /// <summary>
    /// Similar to wander, but faster and shorter-range
    /// </summary>
    public class AiTaskFishMoveFast : AiTaskBase
    {
        //TODO: add a cooldown to temporarily disable this after use (fish is tired)
        //? always include some change of direction

        public Vec3d MainTarget;

        bool done;
        float moveSpeed = 0.06f;
        float wanderChance = 0.04f;
        float maxHeight = 7f;
        float? preferredLightLevel;
        float targetDistance = 0.12f;

        bool awaitReached = true;

        NatFloat wanderRangeHorizontal = NatFloat.createStrongerInvexp(3, 40);
        NatFloat wanderRangeVertical = NatFloat.createStrongerInvexp(3, 10);

        BlockPos tmpPos = new BlockPos();

        public bool TeleportWhenOutOfRange = true;
        public double TeleportInGameHours = 1;

        long lastTimeInRangeMs;

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


        public AiTaskFishMoveFast(EntityAgent entity) : base(entity)
        {
        }

        public override void OnEntityLoaded()
        {

        }

        public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig)
        {
            base.LoadConfig(taskConfig, aiConfig);

            float wanderRangeMin=3, wanderRangeMax=30;

            if (taskConfig["targetDistance"] != null)
            {
                targetDistance = taskConfig["targetDistance"].AsFloat(0.12f);
            }

            if (taskConfig["movespeed"] != null)
            {
                moveSpeed = taskConfig["movespeed"].AsFloat(0.03f);
            }

            if (taskConfig["wanderChance"] != null)
            {
                wanderChance = taskConfig["wanderChance"].AsFloat(0.015f);
            }

            if (taskConfig["wanderRangeMin"] != null)
            {
                wanderRangeMin = taskConfig["wanderRangeMin"].AsFloat(3);
            }
            if (taskConfig["wanderRangeMax"] != null)
            {
                wanderRangeMax = taskConfig["wanderRangeMax"].AsFloat(30);
            }
            wanderRangeHorizontal = NatFloat.createStrongerInvexp(wanderRangeMin, wanderRangeMax);


            if (taskConfig["maxHeight"] != null)
            {
                maxHeight = taskConfig["maxHeight"].AsFloat(7f);
            }

            if (taskConfig["preferredLightLevel"] != null)
            {
                preferredLightLevel = taskConfig["preferredLightLevel"].AsFloat(-99);
                if (preferredLightLevel < 0) preferredLightLevel = null;
            }

            if (taskConfig["awaitReached"] != null)
            {
                awaitReached = taskConfig["awaitReached"].AsBool(true);
            }
            
        }


        public Vec3d loadNextWanderTarget()
        {
            int tries = 9;
            Vec4d bestTarget = null;
            Vec4d curTarget = new Vec4d();
            BlockPos tmpPos = new BlockPos();

            if (FailedConsecutivePathfinds > 10)
            {
                WanderRangeMul = Math.Max(0.1f, WanderRangeMul * 0.9f);
            } else
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
                curTarget.Y = entity.ServerPos.Y + dy;
                curTarget.Z = entity.ServerPos.Z + dz;
                curTarget.W = 1;

                Block block;

                block = entity.World.BlockAccessor.GetLiquidBlock((int)curTarget.X, (int)curTarget.Y, (int)curTarget.Z);
                if (!block.IsLiquid()) curTarget.W = 0;
                else curTarget.W = 1 / (Math.Abs(dy) + 1);  //prefer not too much vertical change when underwater

                //TODO: reject (or de-weight) targets not in direct line of sight (avoiding terrain)


                if (preferredLightLevel != null && curTarget.W != 0)
                {
                    tmpPos.Set((int)curTarget.X, (int)curTarget.Y, (int)curTarget.Z);
                    int lightdiff = Math.Abs((int)preferredLightLevel - entity.World.BlockAccessor.GetLightLevel(tmpPos, EnumLightLevelType.MaxLight));

                    curTarget.W /= Math.Max(1, lightdiff);
                }

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


        public override bool ShouldExecute()
        {
            if (!entity.Swimming) return false;
            if (rand.NextDouble() > wanderChance && !entity.CollidedHorizontally && !entity.CollidedVertically) return false;            

            MainTarget = loadNextWanderTarget();

            return MainTarget != null;
        }


        public override void StartExecute()
        {
            base.StartExecute();

            done = false;
            bool ok = pathTraverser.WalkTowards(MainTarget, moveSpeed, targetDistance, OnGoalReached, OnStuck);
        }

        public override bool ContinueExecute(float dt)
        {
            base.ContinueExecute(dt);

            if (MainTarget.HorizontalSquareDistanceTo(entity.ServerPos.X, entity.ServerPos.Z) < 0.5)
            {
                pathTraverser.Stop();
                return false;
            }

            return !done;
        }

        public override void FinishExecute(bool cancelled)
        {
            base.FinishExecute(cancelled);

            if (cancelled)
            {
                pathTraverser.Stop();
            }
        }

        private void OnStuck()
        {
            done = true;
        }

        private void OnGoalReached()
        {
            done = true;
        }

        
    }
}
