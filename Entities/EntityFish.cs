using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

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
