using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class AiTaskLookAround : AiTaskBase
    {
        public AiTaskLookAround(EntityAgent entity) : base(entity)
        {
        }


        public int minduration;
        public int maxduration;

        public long idleUntilMs;

        public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig)
        {
            this.minduration = (int)taskConfig["minduration"]?.AsInt(2000);
            this.maxduration = (int)taskConfig["maxduration"]?.AsInt(4000);

            idleUntilMs = entity.World.ElapsedMilliseconds + minduration + entity.World.Rand.Next(maxduration - minduration);

            base.LoadConfig(taskConfig, aiConfig);
        }

        public override bool ShouldExecute()
        {
            return cooldownUntilMs < entity.World.ElapsedMilliseconds;
        }

        public override void StartExecute()
        {
            base.StartExecute();
            idleUntilMs = entity.World.ElapsedMilliseconds + minduration + entity.World.Rand.Next(maxduration - minduration);

            entity.ServerPos.Yaw = (float)GameMath.Clamp(
                entity.World.Rand.NextDouble() * GameMath.TWOPI,
                entity.ServerPos.Yaw - GameMath.PI / 4,
                entity.ServerPos.Yaw + GameMath.PI / 4
            );
            
        }

        public override bool ContinueExecute(float dt)
        {
            return entity.World.ElapsedMilliseconds < idleUntilMs;
        }
    }
}
