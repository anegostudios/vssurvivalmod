using Newtonsoft.Json;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{

    [JsonObject(MemberSerialization.OptIn)]
    public class MountBlockAction : EntityActionBase
    {
        public override string Type => "mountblock";

        [JsonProperty]
        AssetLocation targetBlockCode;
        [JsonProperty]
        float searchRange;

        public MountBlockAction() { }

        public MountBlockAction(EntityActivitySystem vas, AssetLocation targetBlockCode, float searchRange)
        {
            this.vas = vas;
            this.targetBlockCode = targetBlockCode;
            this.searchRange = searchRange;
        }

        public override bool IsFinished()
        {
            return vas.Entity.MountedOn != null;
        }

        public override void Start(EntityActivity act)
        {
            if (vas.Entity.MountedOn != null) return;

            bool mountablefound = false;

            searchMountable(vas.Entity.ServerPos.XYZ, (seat, pos) =>
            {
                mountablefound = true;
                if (vas.Entity.TryMount(seat))
                {
                    vas.Entity.GetBehavior<EntityBehaviorTaskAI>()?.TaskManager.StopTasks();
                    vas.Entity.ServerControls.StopAllMovement();
                    return true;
                }
                return false;
            });

            if (vas.Debug && !mountablefound) vas.Entity.World.Logger.Debug("ActivitySystem entity {0} MountBlockAction, no nearby block of code {1} found.", vas.Entity.EntityId, targetBlockCode);

            ExecutionHasFailed = vas.Entity.MountedOn == null;
        }

        private void searchMountable(Vec3d fromPos, ActionBoolReturn<IMountableSeat, BlockPos> onblock)
        {
            var minPos = fromPos.Clone().Sub(searchRange, 1, searchRange).AsBlockPos;
            var maxPos = fromPos.Clone().Add(searchRange, 1, searchRange).AsBlockPos;

            vas.Entity.World.BlockAccessor.SearchBlocks(minPos, maxPos, (block, pos) =>
            {
                if (block.WildCardMatch(targetBlockCode))
                {
                    var seat = block.GetInterface<IMountableSeat>(vas.Entity.World, pos);
                    if (seat != null)
                        if (onblock(seat, pos)) 
                            return false;
                }
                return true;
            });
        }

        public override void Cancel()
        {
            vas.Entity.TryUnmount();
        }

        public override void AddGuiEditFields(ICoreClientAPI capi, GuiComposer singleComposer)
        {
            var b = ElementBounds.Fixed(0, 0, 200, 25);
            singleComposer
                .AddStaticText("Search Range", CairoFont.WhiteDetailText(), b)
                .AddTextInput(b = b.BelowCopy(0, -5), null, CairoFont.WhiteDetailText(), "searchRange")

                .AddStaticText("Block Code", CairoFont.WhiteDetailText(), b = b.BelowCopy(0, 10))
                .AddTextInput(b = b.BelowCopy(0, -5), null, CairoFont.WhiteDetailText(), "targetBlockCode")
            ;

            singleComposer.GetTextInput("searchRange").SetValue(searchRange);
            singleComposer.GetTextInput("targetBlockCode").SetValue(targetBlockCode?.ToShortString() ?? "");
        }

        public override bool StoreGuiEditFields(ICoreClientAPI capi, GuiComposer singleComposer)
        {
            searchRange = singleComposer.GetTextInput("searchRange").GetText().ToFloat();
            targetBlockCode = new AssetLocation(singleComposer.GetTextInput("targetBlockCode").GetText());
            return true;
        }

        public override IEntityAction Clone()
        {
            return new MountBlockAction(vas, targetBlockCode, searchRange);
        }

        public override string ToString()
        {
            return "Mount block " + targetBlockCode + " within " + searchRange + " blocks";
        }

        public override void OnVisualize(ActivityVisualizer visualizer)
        {
            searchMountable(visualizer.CurrentPos, (seat, pos) =>
            {
                visualizer.LineTo(visualizer.CurrentPos, pos.ToVec3d().Add(0.5, 0.5, 0.5), ColorUtil.ColorFromRgba(0, 255, 255, 255));
                return false;
            });
        }
    }
}
