using Newtonsoft.Json;
using System;
using Vintagestory.API.Client;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    [JsonObject(MemberSerialization.OptIn)]
    public class TurnAction : IEntityAction
    {
        public string Type => "turn";
        public bool ExecutionHasFailed { get; set; }

        EntityActivitySystem vas;
        [JsonProperty]
        float yaw;

        public TurnAction() { }

        public TurnAction(EntityActivitySystem vas, float yaw)
        {
            this.vas = vas;
            this.yaw = yaw;
        }


        public bool IsFinished()
        {
            return true;
        }

        public void Start(EntityActivity act)
        {
            vas.Entity.ServerPos.Yaw = yaw * GameMath.DEG2RAD;
        }

        public void OnTick(float dt) { }

        public void Cancel() { }
        public void Finish() { }
        public void LoadState(ITreeAttribute tree) { }
        public void StoreState(ITreeAttribute tree) { }

        public override string ToString()
        {
            return "Turn to look direction " + yaw + " degrees";
        }

        public void AddGuiEditFields(ICoreClientAPI capi, GuiComposer singleComposer)
        {
            var b = ElementBounds.Fixed(0, 0, 300, 25);
            singleComposer
                .AddStaticText("Yaw (in degrees)", CairoFont.WhiteDetailText(), b)
                .AddNumberInput(b = b.BelowCopy(0, -5), null, CairoFont.WhiteDetailText(), "yaw")

                .AddSmallButton("Insert Player Yaw", () => onClickPlayerYaw(capi, singleComposer), b = b.FlatCopy().WithFixedPosition(0, 0).FixedUnder(b, 2), EnumButtonStyle.Small)
            ;

            singleComposer.GetNumberInput("yaw").SetValue(yaw);
        }

        private bool onClickPlayerYaw(ICoreClientAPI capi, GuiComposer singleComposer)
        {
            var plrPos = capi.World.Player.Entity.Pos;
            singleComposer.GetTextInput("yaw").SetValue("" + Math.Round(GameMath.Mod(plrPos.Yaw*GameMath.RAD2DEG, 360), 1));
            return true;
        }

        public IEntityAction Clone()
        {
            return new TurnAction(vas, yaw);
        }

        public bool StoreGuiEditFields(ICoreClientAPI capi, GuiComposer singleComposer)
        {
            yaw = singleComposer.GetTextInput("yaw").GetText().ToFloat();
            return true;
        }

        public void OnVisualize(ActivityVisualizer visualizer) { }
        public void OnLoaded(EntityActivitySystem vas)
        {
            this.vas = vas;
        }
    }
}
