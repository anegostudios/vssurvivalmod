using Newtonsoft.Json;
using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    [JsonObject(MemberSerialization.OptIn)]
    public class PositionVicinityCondition : IActionCondition
    {
        [JsonProperty]
        public bool Invert { get; set; }
        [JsonProperty]
        public double targetX { get { return Target.X; } set { Target.X = value; } }
        [JsonProperty]
        public double targetY { get { return Target.Y; } set { Target.Y = value; } }
        [JsonProperty]
        public double targetZ { get { return Target.Z; } set { Target.Z = value; } }
        [JsonProperty]
        float range;

        public Vec3d Target = new Vec3d();

        protected EntityActivitySystem vas;
        public PositionVicinityCondition() { }
        public PositionVicinityCondition(EntityActivitySystem vas, Vec3d pos, float range, bool invert = false)
        {
            this.vas = vas;
            this.Target = pos;
            this.range = range;
            this.Invert = invert;
        }

        public string Type => "positionvicinity";


        public virtual bool ConditionSatisfied(Entity e)
        {
            return e.ServerPos.DistanceTo(Target) < range;
        }

        public void LoadState(ITreeAttribute tree) { }
        public void StoreState(ITreeAttribute tree) { }


        public void AddGuiEditFields(ICoreClientAPI capi, GuiComposer singleComposer)
        {
            var bc = ElementBounds.Fixed(0, 0, 65, 20);
            var b = ElementBounds.Fixed(0, 0, 200, 20);

            singleComposer
                .AddStaticText("x/y/z Pos", CairoFont.WhiteDetailText(), bc)
                .AddTextInput(bc = bc.BelowCopy(0), null, CairoFont.WhiteDetailText(), "x")
                .AddTextInput(bc = bc.CopyOffsetedSibling(70), null, CairoFont.WhiteDetailText(), "y")
                .AddTextInput(bc = bc.CopyOffsetedSibling(70), null, CairoFont.WhiteDetailText(), "z")
                .AddSmallButton("Tp to", () => { capi.SendChatMessage(string.Format("/tp ={0} ={1} ={2}", targetX, targetY, targetZ)); return false; }, bc = bc.CopyOffsetedSibling(70), EnumButtonStyle.Small)

                .AddSmallButton("Insert Player Pos", () => onClickPlayerPos(capi, singleComposer), b = b.FlatCopy().FixedUnder(bc), EnumButtonStyle.Small)

                .AddStaticText("Range", CairoFont.WhiteDetailText(), b = b.BelowCopy(0, 10))
                .AddNumberInput(b = b.BelowCopy(0, -5), null, CairoFont.WhiteDetailText(), "range")
            ;

            singleComposer.GetNumberInput("range").SetValue(range);
            var s = singleComposer;
            s.GetTextInput("x").SetValue(Target?.X + "");
            s.GetTextInput("y").SetValue(Target?.Y + "");
            s.GetTextInput("z").SetValue(Target?.Z + "");
        }


        private bool onClickPlayerPos(ICoreClientAPI capi, GuiComposer singleComposer)
        {
            var plrPos = capi.World.Player.Entity.Pos.XYZ;
            singleComposer.GetTextInput("x").SetValue("" + Math.Round(plrPos.X, 1));
            singleComposer.GetTextInput("y").SetValue("" + Math.Round(plrPos.Y, 1));
            singleComposer.GetTextInput("z").SetValue("" + Math.Round(plrPos.Z, 1));
            return true;
        }

        public void StoreGuiEditFields(ICoreClientAPI capi, GuiComposer s)
        {
            this.Target = new Vec3d(s.GetTextInput("x").GetText().ToDouble(), s.GetTextInput("y").GetText().ToDouble(), s.GetTextInput("z").GetText().ToDouble());
            range = s.GetNumberInput("range").GetValue();
        }
        public IActionCondition Clone()
        {
            return new PositionVicinityCondition(vas, Target, range, Invert);
        }

        public override string ToString()
        {
            return (Invert ? "When not near pos " : "When near pos ") + Target;
        }

        public void OnLoaded(EntityActivitySystem vas)
        {
            this.vas = vas;
        }
    }
}
