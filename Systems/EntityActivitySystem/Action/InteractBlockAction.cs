using Newtonsoft.Json;
using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    [JsonObject(MemberSerialization.OptIn)]
    public class ActivateBlockAction : EntityActionBase
    {
        public override string Type => "activateblock";

        [JsonProperty]
        AssetLocation targetBlockCode;
        [JsonProperty]
        float searchRange;
        [JsonProperty]
        string activateArgs;
        [JsonProperty]
        public double targetX { get { return ExactTarget.X; } set { ExactTarget.X = value; } }
        [JsonProperty]
        public double targetY { get { return ExactTarget.Y; } set { ExactTarget.Y = value; } }
        [JsonProperty]
        public double targetZ { get { return ExactTarget.Z; } set { ExactTarget.Z = value; } }

        public Vec3d ExactTarget = new Vec3d();

        public ActivateBlockAction() { }

        public ActivateBlockAction(EntityActivitySystem vas, AssetLocation targetBlockCode, float searchRange, string activateArgs, Vec3d exacttarget)
        {
            this.vas = vas;
            this.targetBlockCode = targetBlockCode;
            this.searchRange = searchRange;
            this.activateArgs = activateArgs;
            this.ExactTarget = exacttarget;
        }


        public override void Start(EntityActivity act)
        {
            BlockPos targetPos = getTarget();

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

                var block = vas.Entity.Api.World.BlockAccessor.GetBlock(targetPos);

                var blockSel = new BlockSelection()
                {
                    Block = block,
                    Position = targetPos,
                    HitPosition = new Vec3d(0.5, 0.5, 0.5),
                    Face = BlockFacing.NORTH
                };

                var args = activateArgs == null ? null : TreeAttribute.FromJson(activateArgs) as ITreeAttribute;
                block.Activate(vas.Entity.World, new Caller() { Entity = vas.Entity, Type = EnumCallerType.Entity, Pos = vas.Entity.Pos.XYZ }, blockSel, args);
            }
        }

        private BlockPos getTarget()
        {
            if (ExactTarget.Length() > 0)
            {
                if (ExactTarget.DistanceTo(vas.Entity.ServerPos.XYZ) < searchRange) return ExactTarget.AsBlockPos;
                return null;
            }

            var range = GameMath.Clamp(searchRange, -10, 10);
            var api = vas.Entity.Api;
            var minPos = vas.Entity.ServerPos.XYZ.Add(-range, -1, -range).AsBlockPos;
            var maxPos = vas.Entity.ServerPos.XYZ.Add(range, 1, range).AsBlockPos;

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
            if (ExactTarget.Length() > 0) return "Activate block at " + ExactTarget;
            return "Activate nearest block " + targetBlockCode + " within " + searchRange + " blocks";
        }

        public override void AddGuiEditFields(ICoreClientAPI capi, GuiComposer singleComposer)
        {
            var b = ElementBounds.Fixed(0, 0, 300, 25);
            var bc = ElementBounds.Fixed(0, 0, 65, 20);

            singleComposer
                .AddStaticText("Block Code", CairoFont.WhiteDetailText(), b)
                .AddTextInput(b = b.BelowCopy(0, -5), null, CairoFont.WhiteDetailText(), "targetBlockCode")

                .AddStaticText("OR exact x/y/z Pos", CairoFont.WhiteDetailText(), b = b.BelowCopy(0, 5))
                .AddTextInput(bc = bc.FlatCopy().FixedUnder(b, -3), null, CairoFont.WhiteDetailText(), "x")
                .AddTextInput(bc = bc.CopyOffsetedSibling(70), null, CairoFont.WhiteDetailText(), "y")
                .AddTextInput(bc = bc.CopyOffsetedSibling(70), null, CairoFont.WhiteDetailText(), "z")

                .AddSmallButton("Tp to", () => { capi.SendChatMessage(string.Format("/tp ={0} ={1} ={2}", targetX, targetY, targetZ)); return false; }, bc = bc.CopyOffsetedSibling(70), EnumButtonStyle.Small)

                .AddSmallButton("Insert Player Pos", () => onClickPlayerPos(capi, singleComposer), b = b.FlatCopy().WithFixedPosition(0,0).FixedUnder(bc,2), EnumButtonStyle.Small)


                .AddStaticText("Within Range (capped to 10 blocks)", CairoFont.WhiteDetailText(), b = b.BelowCopy(0, 30))
                .AddNumberInput(b = b.BelowCopy(0, -5), null, CairoFont.WhiteDetailText(), "searchRange")

                .AddStaticText("Activation Arguments", CairoFont.WhiteDetailText(), b = b.BelowCopy(0, 10))
                .AddTextInput(b = b.BelowCopy(0, -5), null, CairoFont.WhiteDetailText(), "activateArgs")
            ;

            var s = singleComposer;
            s.GetNumberInput("searchRange").SetValue(searchRange);
            s.GetTextInput("targetBlockCode").SetValue(targetBlockCode?.ToShortString());
            s.GetTextInput("activateArgs").SetValue(activateArgs);

            s.GetTextInput("x").SetValue(ExactTarget?.X + "");
            s.GetTextInput("y").SetValue(ExactTarget?.Y + "");
            s.GetTextInput("z").SetValue(ExactTarget?.Z + "");

        }

        private bool onClickPlayerPos(ICoreClientAPI capi, GuiComposer singleComposer)
        {
            var plrPos = capi.World.Player.Entity.Pos.XYZ;
            singleComposer.GetTextInput("x").SetValue("" + Math.Round(plrPos.X, 1));
            singleComposer.GetTextInput("y").SetValue("" + Math.Round(plrPos.Y, 1));
            singleComposer.GetTextInput("z").SetValue("" + Math.Round(plrPos.Z, 1));
            return true;
        }

        public override IEntityAction Clone()
        {
            return new ActivateBlockAction(vas, targetBlockCode, searchRange, activateArgs, ExactTarget);
        }

        public override bool StoreGuiEditFields(ICoreClientAPI capi, GuiComposer singleComposer)
        {
            var s = singleComposer;

            this.ExactTarget = new Vec3d(s.GetTextInput("x").GetText().ToDouble(), s.GetTextInput("y").GetText().ToDouble(), s.GetTextInput("z").GetText().ToDouble());
            searchRange = s.GetTextInput("searchRange").GetText().ToFloat();
            targetBlockCode = new AssetLocation(s.GetTextInput("targetBlockCode").GetText());
            activateArgs = s.GetTextInput("activateArgs").GetText();
            return true;
        }

        public override void OnVisualize(ActivityVisualizer visualizer)
        {
            var target = getTarget();
            if (target != null)
            {
                visualizer.LineTo(target.ToVec3d().Add(0.5, 0.5, 0.5));
            }
        }
    }
}
