using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class AiTaskButterflyChase: AiTaskButterflyWander
    {
        internal EntityButterfly targetEntity;
        internal Vec3d targetPos = new Vec3d();


        protected float chaseTime;

        protected bool fleeState;

        protected float seekingRange = 3;

        public JsonObject taskConfig;

        public AiTaskButterflyChase(EntityAgent entity) : base(entity)
        {

        }

        public AiTaskButterflyChase(EntityAgent entity, EntityButterfly chaseTarget) : base(entity)
        {
            
            chaseTime = (float)entity.World.Rand.NextDouble() * 7 + 6;
            targetEntity = chaseTarget;
            targetPos.Set(targetEntity.ServerPos.X, targetEntity.ServerPos.Y, targetEntity.ServerPos.Z);
        }

        public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig)
        {
            this.taskConfig = taskConfig;

            base.LoadConfig(taskConfig, aiConfig);
        }

        public override bool ShouldExecute()
        {
            if (entity.World.Rand.NextDouble() > 0.03) return false;
            if (cooldownUntilMs > entity.World.ElapsedMilliseconds) return false;
            if (cooldownUntilTotalHours > entity.World.Calendar.TotalHours) return false;
            if (whenInEmotionState != null && !entity.HasEmotionState(whenInEmotionState)) return false;
            if (whenNotInEmotionState != null && entity.HasEmotionState(whenNotInEmotionState)) return false;

            targetEntity = (EntityButterfly)entity.World.GetNearestEntity(entity.ServerPos.XYZ, seekingRange, seekingRange, (e) => {
                if (!e.Alive || e.EntityId == this.entity.EntityId) return false;

                if (e is EntityButterfly)
                {
                    return true;
                }

                return false;
            });

            if (targetEntity != null)
            {
                chaseTime = (float)entity.World.Rand.NextDouble() * 7 + 6;
                targetPos.Set(targetEntity.ServerPos.X, targetEntity.ServerPos.Y, targetEntity.ServerPos.Z);

                // Tell the other butterfly to chase us
                AiTaskManager manager = targetEntity.GetBehavior<EntityBehaviorTaskAI>().taskManager;
                AiTaskButterflyChase othertask = manager.GetTask<AiTaskButterflyChase>();
                othertask.targetEntity = this.entity as EntityButterfly;
                othertask.targetPos.Set(entity.ServerPos.X, entity.ServerPos.Y, entity.ServerPos.Z);
                othertask.chaseTime = (float)entity.World.Rand.NextDouble() * 7 + 6;

                manager.ExecuteTask<AiTaskButterflyChase>();
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
            } else
            {
                pathTraverser.WalkTowards(targetPos, moveSpeed, 1f, OnGoalReached, OnStuck);
            }
        }

        public override bool ContinueExecute(float dt)
        {
            targetPos.Set(targetEntity.ServerPos.X, targetEntity.ServerPos.Y + (fleeState ? 1 : 0), targetEntity.ServerPos.Z);

            return (chaseTime -= dt) >= 0;
        }


        public override void FinishExecute(bool cancelled)
        {
            pathTraverser.Stop();
            base.FinishExecute(cancelled);
        }


    }
}
