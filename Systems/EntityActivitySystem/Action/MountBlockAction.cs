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

            searchBlocks((block, pos) =>
            {
                mountablefound = true;
                var seat = block.GetInterface<IMountableSeat>(vas.Entity.World, pos);
                if (seat != null)
                {
                    if (vas.Entity.TryMount(seat))
                    {
                        vas.Entity.GetBehavior<EntityBehaviorTaskAI>()?.TaskManager.StopTasks();
                        vas.Entity.ServerControls.StopAllMovement();
                        return true;
                    }
                }
                return false;
            });

            if (vas.Debug && !mountablefound) vas.Entity.World.Logger.Debug("ActivitySystem entity {0} MountBlockAction, no nearby block of code {1} found.", vas.Entity.EntityId, targetBlockCode);

            ExecutionHasFailed = vas.Entity.MountedOn == null;
        }

        private void searchBlocks(ActionBoolReturn<Block, BlockPos> onblock)
        {
            var minPos = vas.Entity.ServerPos.XYZ.Sub(searchRange, 1, searchRange).AsBlockPos;
            var maxPos = vas.Entity.ServerPos.XYZ.Add(searchRange, 1, searchRange).AsBlockPos;

            vas.Entity.World.BlockAccessor.SearchBlocks(minPos, maxPos, (block, pos) =>
            {
                if (block.WildCardMatch(targetBlockCode))
                {
                    if (onblock(block, pos)) return false;
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
            BlockPos targetPos=null;
            searchBlocks((block, pos) =>
            {
                if (block.GetInterface<IMountableSeat>(vas.Entity.World, pos) != null)
                {
                    targetPos = pos;
                    return true;
                }
                return false;
            });

            
            if (targetPos != null)
            {
                visualizer.LineTo(targetPos.ToVec3d().Add(0.5, 0.5, 0.5));
            }
        }
    }
}
