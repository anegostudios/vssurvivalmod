using Vintagestory.API.Common;

namespace Vintagestory.GameContent
{
    public class AiTaskFlyLeave : AiTaskFlyWander
    {
        public bool AllowExecute;

        public AiTaskFlyLeave(EntityAgent entity) : base(entity)
        {
        }

        public override bool ShouldExecute()
        {
            return AllowExecute && base.ShouldExecute();
        }
    }
}
