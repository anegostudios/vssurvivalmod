using System.Diagnostics;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

#nullable disable

namespace Vintagestory.GameContent
{
    public class AiTaskFlyLeave : AiTaskFlyWander
    {
        public bool AllowExecute;

        public AiTaskFlyLeave(EntityAgent entity, JsonObject taskConfig, JsonObject aiConfig) : base(entity, taskConfig, aiConfig)
        {
        }

        public override bool ShouldExecute()
        {
            return AllowExecute && base.ShouldExecute();
        }
    }
}
