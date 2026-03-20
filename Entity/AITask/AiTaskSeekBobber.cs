#nullable disable

using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class AiTaskSeekBobber : AiTaskSeekEntity
    {
        string[] baitTags;

        public AiTaskSeekBobber(EntityAgent entity, JsonObject taskConfig, JsonObject aiConfig) : base(entity, taskConfig, aiConfig)
        {
            baitTags = entity.Properties.Attributes["baitTags"].AsArray<string>();
        }

        public override bool CanSense(Entity e, double range)
        {
            if (!base.CanSense(e, range)) return false;

            var baitTag = (e as EntityBobber)?.BaitStack?.Collectible.Attributes?["baitTag"].AsString() ?? "nobait";
            return baitTags.Contains(baitTag);
        }
    }
}
