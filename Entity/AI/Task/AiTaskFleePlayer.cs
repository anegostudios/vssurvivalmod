using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class AiTaskFleePlayer : AiTaskBase
    {
        EntityAgent targetEntity;
        Vec3d targetPos;
        float moveSpeed = 0.02f;
        float seekingRange = 25f;
        float executionChance = 0.04f;
        float fleeingDistance = 31f;
        float minDayLight = -1f;
        float fleeDurationMs = 5000;
        bool cancelOnHurt = false;

        long fleeStartMs;
        bool stuck;

        public AiTaskFleePlayer(EntityAgent entity) : base(entity)
        {
        }

        public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig)
        {
            base.LoadConfig(taskConfig, aiConfig);

            if (taskConfig["movespeed"] != null)
            {
                moveSpeed = taskConfig["movespeed"].AsFloat(0.02f);
            }

            if (taskConfig["seekingRange"] != null)
            {
                seekingRange = taskConfig["seekingRange"].AsFloat(25);
            }

            if (taskConfig["executionChance"] != null)
            {
                executionChance = taskConfig["executionChance"].AsFloat(0.04f);
            }

            if (taskConfig["minDayLight"] != null)
            {
                minDayLight = taskConfig["minDayLight"].AsFloat(-1f);
            }

            if (taskConfig["cancelOnHurt"] != null)
            {
                cancelOnHurt = taskConfig["cancelOnHurt"].AsBool(false);
            }

            if (taskConfig["fleeingDistance"] != null)
            {
                fleeingDistance = taskConfig["fleeingDistance"].AsFloat(25f);
            } else fleeingDistance = seekingRange + 6;

            if (taskConfig["fleeDurationMs"] != null)
            {
                fleeDurationMs = taskConfig["fleeDurationMs"].AsInt(5000);
            }
            
        }


        public override bool ShouldExecute()
        {
            if (rand.NextDouble() > executionChance || entity.World.Calendar.DayLightStrength < minDayLight) return false;
            if (whenInEmotionState != null && !entity.HasEmotionState(whenInEmotionState)) return false;

            targetEntity = (EntityAgent)entity.World.GetNearestEntity(entity.ServerPos.XYZ, seekingRange, seekingRange, (e) => { return e is EntityPlayer && e.Alive; });

            if (targetEntity != null)
            {
                updateTargetPos();

                IPlayer player = entity.World.PlayerByUid(((EntityPlayer)targetEntity).PlayerUID);
                if (player != null && (player.WorldData.CurrentGameMode == EnumGameMode.Creative || player.WorldData.CurrentGameMode == EnumGameMode.Spectator))
                {
                    return false;
                }

                return true;
            }

            return false;
        }


        public override void StartExecute()
        {
            base.StartExecute();

            float size = targetEntity.CollisionBox.X2 - targetEntity.CollisionBox.X1;

            entity.PathTraverser.GoTo(targetPos, moveSpeed, size + 0.2f, OnGoalReached, OnStuck);

            fleeStartMs = entity.World.ElapsedMilliseconds;
            stuck = false;

        }

        public override bool ContinueExecute(float dt)
        {
            updateTargetPos();
            
            entity.PathTraverser.CurrentTarget.X = targetPos.X;
            entity.PathTraverser.CurrentTarget.Y = targetPos.Y;
            entity.PathTraverser.CurrentTarget.Z = targetPos.Z;

            if (entity.ServerPos.SquareDistanceTo(targetEntity.ServerPos.XYZ) > fleeingDistance * fleeingDistance)
            {
                return false;
            }

            if (entity.IsActivityRunning("invulnerable")) return false;

            return !stuck && targetEntity.Alive && (entity.World.ElapsedMilliseconds - fleeStartMs < fleeDurationMs);
        }


        private void updateTargetPos()
        {
            Vec3d diff = targetEntity.Pos.XYZ.Sub(entity.ServerPos.X, entity.ServerPos.Y, entity.ServerPos.Z);
            float yaw = (float)Math.Atan2(diff.X, diff.Z);

            targetPos = entity.Pos.XYZ.Ahead(10, 0, yaw - GameMath.PI/2);
        }

        public override void FinishExecute(bool cancelled)
        {
            entity.PathTraverser.Stop();

            base.FinishExecute(cancelled);
        }


        private void OnStuck()
        {
            stuck = true;
        }

        private void OnGoalReached()
        {
            entity.PathTraverser.Active = true;
        }
    }
}
