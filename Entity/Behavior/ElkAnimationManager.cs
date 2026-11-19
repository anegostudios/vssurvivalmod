using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace Vintagestory.GameContent
{
    public class ElkAnimationManager : AnimationManager
    {
        public string animAppendix = "-antlers";

        public ElkAnimationManager(Entity entity) : base(entity) { }

        public override void ResetAnimation(string animCode)
        {
            base.ResetAnimation(animCode);
            base.ResetAnimation(animCode + animAppendix);
        }

        public override void StopAnimation(string code)
        {
            base.StopAnimation(code);
            base.StopAnimation(code + animAppendix);
            AnimationsDirty = true;
        }
    }
}
