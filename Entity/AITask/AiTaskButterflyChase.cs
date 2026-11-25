using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

#nullable disable

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

        public AiTaskButterflyChase(EntityAgent entity, JsonObject taskConfig, JsonObject aiConfig) : base(entity, taskConfig, aiConfig)
        {
            this.taskConfig = taskConfig;
        }

        public AiTaskButterflyChase(EntityAgent entity, EntityButterfly chaseTarget) : base(entity, JsonObject.FromJson("{}"), JsonObject.FromJson("{}"))
        {

            chaseTime = (float)entity.World.Rand.NextDouble() * 7 + 6;
            targetEntity = chaseTarget;
            targetPos.Set(targetEntity.Pos.X, targetEntity.Pos.Y, targetEntity.Pos.Z);

            this.taskConfig = JsonObject.FromJson("{}");
        }

        protected override void SetDefaultValues()
        {
            base.SetDefaultValues();
            ExecutionChance = 0.03;
        }

        public override bool ShouldExecute()
        {
            if (entity.World.Rand.NextDouble() > ExecutionChance) return false;
            if (cooldownUntilMs > entity.World.ElapsedMilliseconds) return false;
            if (entity.FeetInLiquid) return false;
            if (cooldownUntilTotalHours > entity.World.Calendar.TotalHours) return false;
            if (!PreconditionsSatisfied()) return false;

            targetEntity = (EntityButterfly)entity.World.GetNearestEntity(entity.Pos.XYZ, seekingRange, seekingRange, (e) => {
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
                targetPos.Set(targetEntity.Pos.X, targetEntity.Pos.Y, targetEntity.Pos.Z);

                // Tell the other butterfly to chase us
                AiTaskManager manager = targetEntity.GetBehavior<EntityBehaviorTaskAI>().TaskManager;
                AiTaskButterflyChase othertask = manager.GetTask<AiTaskButterflyChase>();
                othertask.targetEntity = this.entity as EntityButterfly;
                othertask.targetPos.Set(entity.Pos.X, entity.Pos.Y, entity.Pos.Z);
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
            //Check if time is still valid for task.
            if (!IsInValidDayTimeHours(false)) return false;

            if (targetEntity == null) return false;

            targetPos.Set(targetEntity.Pos.X, targetEntity.Pos.Y + (fleeState ? 1 : 0), targetEntity.Pos.Z);

            return (chaseTime -= dt) >= 0;
        }


        public override void FinishExecute(bool cancelled)
        {
            pathTraverser.Stop();
            base.FinishExecute(cancelled);
        }


    }
}
