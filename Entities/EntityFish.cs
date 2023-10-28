using Vintagestory.API.Common;

namespace Vintagestory.GameContent
{
    public class EntityFish : EntityAgent
    {
        static EntityFish()
        {
            AiTaskRegistry.Register<AiTaskFishMoveFast>("fishmovefast");
            AiTaskRegistry.Register<AiTaskFishOutOfWater>("fishoutofwater");
        }

    }
}
