using Newtonsoft.Json;
using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
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
        [JsonProperty]
        public BlockPos targetPosition;

        public MountBlockAction() { }

        public MountBlockAction(EntityActivitySystem vas, AssetLocation targetBlockCode, float searchRange, BlockPos pos)
        {
            this.vas = vas;
            this.targetBlockCode = targetBlockCode;
            this.searchRange = searchRange;
            this.targetPosition = pos;
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
            if (targetPosition != null)
            {
                var pos = targetPosition.Copy();
                if (vas != null)
                {
                    pos.Add(vas.ActivityOffset);
                }
                var seat = vas.Entity.World.BlockAccessor.GetBlock(pos).GetInterface<IMountableSeat>(vas.Entity.World, pos);
                if (seat != null) onblock(seat, pos);
                return;
            }

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
            var bc = ElementBounds.Fixed(0, 0, 65, 20);

            singleComposer
                .AddStaticText("Search Range", CairoFont.WhiteDetailText(), b)
                .AddTextInput(b = b.BelowCopy(0, -5), null, CairoFont.WhiteDetailText(), "searchRange")

                .AddStaticText("OR exact x/y/z Pos", CairoFont.WhiteDetailText(), b = b.BelowCopy(0, 5))
                .AddTextInput(bc = bc.FlatCopy().FixedUnder(b, -3), null, CairoFont.WhiteDetailText(), "x")
                .AddTextInput(bc = bc.CopyOffsetedSibling(70), null, CairoFont.WhiteDetailText(), "y")
                .AddTextInput(bc = bc.CopyOffsetedSibling(70), null, CairoFont.WhiteDetailText(), "z")
                .AddSmallButton("Tp to", () => onClickTpTo(capi), bc = bc.CopyOffsetedSibling(70), EnumButtonStyle.Small)
                .AddSmallButton("Insert Player Pos", () => onClickPlayerPos(capi, singleComposer), b = b.FlatCopy().WithFixedPosition(0, 0).FixedUnder(bc, 2), EnumButtonStyle.Small)


                .AddStaticText("Block Code", CairoFont.WhiteDetailText(), b = b.BelowCopy(0, 10))
                .AddTextInput(b = b.BelowCopy(0, -5), null, CairoFont.WhiteDetailText(), "targetBlockCode")
            ;

            var s = singleComposer;
            s.GetTextInput("searchRange").SetValue(searchRange);
            s.GetTextInput("targetBlockCode").SetValue(targetBlockCode?.ToShortString() ?? "");
            s.GetTextInput("x").SetValue(targetPosition?.X + "");
            s.GetTextInput("y").SetValue(targetPosition?.Y + "");
            s.GetTextInput("z").SetValue(targetPosition?.Z + "");

        }


        private bool onClickTpTo(ICoreClientAPI capi)
        {
            var x = targetPosition.X;
            var y = targetPosition.Y;
            var z = targetPosition.Z;
            if (vas != null)
            {
                x += vas.ActivityOffset.X;
                y += vas.ActivityOffset.Y;
                z += vas.ActivityOffset.Z;
            }
            capi.SendChatMessage(string.Format("/tp ={0} ={1} ={2}", x, y, z));
            return false;
        }

        private bool onClickPlayerPos(ICoreClientAPI capi, GuiComposer singleComposer)
        {
            var plrPos = capi.World.Player.Entity.Pos.XYZ;
            singleComposer.GetTextInput("x").SetValue("" + Math.Round(plrPos.X, 1));
            singleComposer.GetTextInput("y").SetValue("" + Math.Round(plrPos.Y, 1));
            singleComposer.GetTextInput("z").SetValue("" + Math.Round(plrPos.Z, 1));
            return true;
        }

        public override bool StoreGuiEditFields(ICoreClientAPI capi, GuiComposer singleComposer)
        {
            var s = singleComposer;

            if (s.GetTextInput("x").GetText().Length > 0)
            {
                this.targetPosition = new BlockPos(
                    (int)s.GetTextInput("x").GetText().ToDouble(),
                    (int)s.GetTextInput("y").GetText().ToDouble(),
                    (int)s.GetTextInput("z").GetText().ToDouble()
                );
            }
            else targetPosition = null;

            searchRange = singleComposer.GetTextInput("searchRange").GetText().ToFloat();
            targetBlockCode = new AssetLocation(singleComposer.GetTextInput("targetBlockCode").GetText());
            return true;
        }

        public override IEntityAction Clone()
        {
            return new MountBlockAction(vas, targetBlockCode, searchRange, targetPosition);
        }

        public override string ToString()
        {
            if (targetPosition != null)
            {
                var exactTarget = targetPosition.Copy();
                if (vas != null)
                {
                    exactTarget.Add(vas.ActivityOffset);
                }
                return "Mount block at " + exactTarget;
            }

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
