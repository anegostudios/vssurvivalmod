using Newtonsoft.Json;
using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;

namespace Vintagestory.GameContent
{
    [JsonObject(MemberSerialization.OptIn)]
    public class CoordinateCondition : IActionCondition
    {
        [JsonProperty]
        public bool Invert { get; set; }
        [JsonProperty]
        public int Axis;
        [JsonProperty]
        public double Value;

        protected EntityActivitySystem vas;
        public CoordinateCondition() { }
        public CoordinateCondition(EntityActivitySystem vas, int axis, double value, bool invert = false)
        {
            this.vas = vas;
            this.Axis = axis;
            this.Value = value;
            this.Invert = invert;
        }

        public string Type => "coordinate";


        public virtual bool ConditionSatisfied(Entity e)
        {
            var pos = e.ServerPos;
            switch (Axis)
            {
                case 0: return pos.X < Value;
                case 1: return pos.Y < Value;
                case 2: return pos.Z < Value;
            }

            return false;
        }

        public void LoadState(ITreeAttribute tree) { }
        public void StoreState(ITreeAttribute tree) { }


        public void AddGuiEditFields(ICoreClientAPI capi, GuiComposer singleComposer)
        {
            var bc = ElementBounds.Fixed(0, 0, 200, 20);

            singleComposer
                .AddStaticText("When", CairoFont.WhiteDetailText(), bc)
                .AddDropDown(new string[] { "x", "y", "z" }, new string[] { "X", "Y", "Z" }, Axis, null, bc = bc.BelowCopy(0), "axis")

                .AddStaticText("Is smaller than", CairoFont.WhiteDetailText(), bc = bc.BelowCopy(0,5).WithFixedWidth(200))
                .AddNumberInput(bc = bc.BelowCopy(0).WithFixedWidth(200), null, CairoFont.WhiteDetailText(), "value")

                .AddSmallButton("Insert Player Pos", () => onClickPlayerPos(capi, singleComposer), bc = bc.BelowCopy(), EnumButtonStyle.Small)
            ;

            singleComposer.GetNumberInput("value").SetValue(Value);
        }


        private bool onClickPlayerPos(ICoreClientAPI capi, GuiComposer singleComposer)
        {
            int index = singleComposer.GetDropDown("axis").SelectedIndices[0];
            double val = 0;
            switch (index)
            {
                case 0: val = capi.World.Player.Entity.Pos.X; break;
                case 1: val = capi.World.Player.Entity.Pos.Y; break;
                case 2: val = capi.World.Player.Entity.Pos.Z; break;
                default: break;
            }
            
            singleComposer.GetTextInput("value").SetValue("" + Math.Round(val, 1));
            return true;
        }

        public void StoreGuiEditFields(ICoreClientAPI capi, GuiComposer s)
        {
            Value = s.GetNumberInput("value").GetValue();
            Axis = s.GetDropDown("axis").SelectedIndices[0];
        }
        public IActionCondition Clone()
        {
            return new CoordinateCondition(vas, Axis, Value, Invert);
        }

        public override string ToString()
        {
            string axis = new string[] { "X", "Y", "Z" }[Axis];
            return string.Format("When {0} {1} {2}", axis, Invert ? "&gt;=" : "&lt;", Value);
        }

        public void OnLoaded(EntityActivitySystem vas)
        {
            this.vas = vas;
        }
    }
}
