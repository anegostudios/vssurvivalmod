using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class AiTaskButterflyFlee : AiTaskButterflyWander
    {
        internal Entity fleeFromEntity;
        internal Vec3d targetPos = new Vec3d();


        protected float fleeTime;

        protected bool fleeState;

        protected float seekingRange = 5;

        public JsonObject taskConfig;

        public AiTaskButterflyFlee(EntityAgent entity) : base(entity)
        {

        }

        public AiTaskButterflyFlee(EntityAgent entity, EntityButterfly chaseTarget) : base(entity)
        {

            fleeTime = (float)entity.World.Rand.NextDouble() * 7 + 6;
            fleeFromEntity = chaseTarget;
            targetPos.Set(fleeFromEntity.ServerPos.X, fleeFromEntity.ServerPos.Y, fleeFromEntity.ServerPos.Z);
        }

        public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig)
        {
            this.taskConfig = taskConfig;

            base.LoadConfig(taskConfig, aiConfig);
        }

        public override bool ShouldExecute()
        {
            if (entity.World.Rand.NextDouble() > 0.05) return false;
            if (cooldownUntilMs > entity.World.ElapsedMilliseconds) return false;
            if (cooldownUntilTotalHours > entity.World.Calendar.TotalHours) return false;
            if (whenInEmotionState != null && bhEmo?.IsInEmotionState(whenInEmotionState) != true) return false;
            if (whenNotInEmotionState != null && bhEmo?.IsInEmotionState(whenNotInEmotionState) == true) return false;

            fleeFromEntity = entity.World.GetNearestEntity(entity.ServerPos.XYZ, seekingRange, seekingRange, (e) => {
                if (!e.Alive || e.EntityId == this.entity.EntityId) return false;

                if (e is EntityPlayer && (e as EntityPlayer).Player?.WorldData.CurrentGameMode != EnumGameMode.Creative && (e as EntityPlayer).Player?.WorldData.CurrentGameMode != EnumGameMode.Spectator)
                {
                    return true;
                }

                return false;
            });

            if (fleeFromEntity != null)
            {
                fleeTime = (float)entity.World.Rand.NextDouble() * 3.5f + 3;
                updateTargetPos();
                return true;
            }

            return false;
        }

        public override void StartExecute()
        {
            base.StartExecute();

            pathTraverser.WalkTowards(targetPos, moveSpeed, 0.0001f, OnGoalReached, OnStuck);
        }

        private void OnStuck()
        {
            //stuck = true;
        }

        private void OnGoalReached()
        {
            fleeState = !fleeState;
            if (fleeState)
            {
                pathTraverser.WalkTowards(targetPos.Add(0, 1, 0), moveSpeed, 1f, OnGoalReached, OnStuck);
            }
            else
            {
                pathTraverser.WalkTowards(targetPos, moveSpeed, 1f, OnGoalReached, OnStuck);
            }
        }

        public override bool ContinueExecute(float dt)
        {
            if (world.Rand.NextDouble() < 0.2)
            {
                updateTargetPos();
                pathTraverser.CurrentTarget.X = targetPos.X;
                pathTraverser.CurrentTarget.Y = targetPos.Y;
                pathTraverser.CurrentTarget.Z = targetPos.Z;
            }


            if (entity.ServerPos.SquareDistanceTo(fleeFromEntity.ServerPos.XYZ) > 5*5) //fleeingDistance * fleeingDistance)
            {
                return false;
            }

            return (fleeTime -= dt) >= 0;
        }


        Vec3d tmpVec = new Vec3d();
        private void updateTargetPos()
        {
            float yaw = (float)Math.Atan2(fleeFromEntity.ServerPos.X - entity.ServerPos.X, fleeFromEntity.ServerPos.Z - entity.ServerPos.Z);



            // Some simple steering behavior, works really suxy
            tmpVec = tmpVec.Set(entity.ServerPos.X, entity.ServerPos.Y, entity.ServerPos.Z);
            tmpVec.Ahead(0.9, 0, yaw + GameMath.PI / 2);

            // Running into wall?
            if (traversable(tmpVec))
            {
                //yawOffset = 0;
                targetPos.Set(entity.ServerPos.X, entity.ServerPos.Y, entity.ServerPos.Z).Ahead(10, 3, yaw + GameMath.PI / 2);
                return;
            }

            // Try 90 degrees left
            tmpVec = tmpVec.Set(entity.ServerPos.X, entity.ServerPos.Y, entity.ServerPos.Z);
            tmpVec.Ahead(0.9, 0, yaw + GameMath.PI);
            if (traversable(tmpVec))
            {
                targetPos.Set(entity.ServerPos.X, entity.ServerPos.Y, entity.ServerPos.Z).Ahead(10, 3, yaw + GameMath.PI);
                return;
            }

            // Try 90 degrees right
            tmpVec = tmpVec.Set(entity.ServerPos.X, entity.ServerPos.Y, entity.ServerPos.Z);
            tmpVec.Ahead(0.9, 0, yaw);
            if (traversable(tmpVec))
            {
                targetPos.Set(entity.ServerPos.X, entity.ServerPos.Y, entity.ServerPos.Z).Ahead(10, 3, yaw);
                return;
            }

            // Run towards target o.O
            tmpVec = tmpVec.Set(entity.ServerPos.X, entity.ServerPos.Y, entity.ServerPos.Z);
            tmpVec.Ahead(0.9, 0, -yaw);
            //if (traversable(tmpVec))
            {
                targetPos.Set(entity.ServerPos.X, entity.ServerPos.Y, entity.ServerPos.Z).Ahead(10, 0, -yaw);
                return;
            }
        }


        
        bool traversable(Vec3d pos)
        {
            return !world.CollisionTester.IsColliding(world.BlockAccessor, entity.SelectionBox, pos, false);
        }


        public override void FinishExecute(bool cancelled)
        {
            pathTraverser.Stop();
            base.FinishExecute(cancelled);
        }


    }
}
