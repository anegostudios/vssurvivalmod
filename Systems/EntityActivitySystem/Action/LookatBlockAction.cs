using Newtonsoft.Json;
using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent
{
    [JsonObject(MemberSerialization.OptIn)]
    public class LookatBlockAction : EntityActionBase
    {
        public override string Type => "lookatblock";

        [JsonProperty]
        AssetLocation targetBlockCode;
        [JsonProperty]
        float searchRange;

        public LookatBlockAction() { }

        public LookatBlockAction(EntityActivitySystem vas, AssetLocation targetBlockCode, float searchRange)
        {
            this.vas = vas;
            this.targetBlockCode = targetBlockCode;
            this.searchRange = searchRange;
        }

        public override void Start(EntityActivity act)
        {
            BlockPos targetPos = getTarget(vas.Entity.Api, vas.Entity.ServerPos.XYZ);

            ExecutionHasFailed = targetPos == null;

            if (targetPos != null)
            {
                Vec3f targetVec = new Vec3f();

                targetVec.Set(
                    (float)(targetPos.X + 0.5 - vas.Entity.ServerPos.X),
                    (float)(targetPos.Y + 0.5 - vas.Entity.ServerPos.Y),
                    (float)(targetPos.Z + 0.5 - vas.Entity.ServerPos.Z)
                );

                vas.Entity.ServerPos.Yaw = (float)Math.Atan2(targetVec.X, targetVec.Z);
            }
        }

        private BlockPos getTarget(ICoreAPI api, Vec3d fromPos)
        {
            var range = GameMath.Clamp(searchRange, -10, 10);
            var minPos = fromPos.Clone().Add(-range, -1, -range).AsBlockPos;
            var maxPos = fromPos.Clone().Add(range, 1, range).AsBlockPos;

            BlockPos targetPos = null;
            api.World.BlockAccessor.WalkBlocks(minPos, maxPos, (block, x, y, z) =>
            {
                if (targetBlockCode == null) return;

                if (block.WildCardMatch(targetBlockCode))
                {
                    targetPos = new BlockPos(x, y, z);
                }
            }, true);
            return targetPos;
        }

        public override string ToString()
        {
            return "Look at nearest block " + targetBlockCode + " within " + searchRange + " blocks";
        }

        public override void AddGuiEditFields(ICoreClientAPI capi, GuiComposer singleComposer)
        {
            var b = ElementBounds.Fixed(0, 0, 300, 25);
            singleComposer
                .AddStaticText("Search Range (capped to 10 blocks)", CairoFont.WhiteDetailText(), b)
                .AddNumberInput(b = b.BelowCopy(0, -5), null, CairoFont.WhiteDetailText(), "searchRange")

                .AddStaticText("Block Code", CairoFont.WhiteDetailText(), b = b.BelowCopy(0, 10))
                .AddTextInput(b = b.BelowCopy(0, -5), null, CairoFont.WhiteDetailText(), "targetBlockCode")
            ;

            singleComposer.GetNumberInput("searchRange").SetValue(searchRange);
            singleComposer.GetTextInput("targetBlockCode").SetValue(targetBlockCode?.ToShortString());
        }

        public override IEntityAction Clone()
        {
            return new LookatBlockAction(vas, targetBlockCode, searchRange);
        }

        public override bool StoreGuiEditFields(ICoreClientAPI capi, GuiComposer singleComposer)
        {
            searchRange = singleComposer.GetTextInput("searchRange").GetText().ToFloat();
            targetBlockCode = new AssetLocation(singleComposer.GetTextInput("targetBlockCode").GetText());
            return true;
        }

        public override void OnVisualize(ActivityVisualizer visualizer)
        {
            var target = getTarget(visualizer.Api, visualizer.CurrentPos);
            if (target != null)
            {
                visualizer.LineTo(visualizer.CurrentPos, target.ToVec3d().Add(0.5, 0.5, 0.5), ColorUtil.ColorFromRgba(0, 255, 0, 255));
            }
        }
    }
}
