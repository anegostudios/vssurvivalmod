using Newtonsoft.Json;
using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{

    [JsonObject(MemberSerialization.OptIn)]
    public class VariableCondition : IActionCondition
    {
        [JsonProperty]
        public bool Invert { get; set; }
        [JsonProperty]
        EnumActivityVariableScope scope;
        [JsonProperty]
        public string Name;
        [JsonProperty]
        public string Comparison;
        [JsonProperty]
        public string Value;

        protected EntityActivitySystem vas;
        public VariableCondition() { }
        public VariableCondition(EntityActivitySystem vas, EnumActivityVariableScope scope, string name, string value, string comparison, bool invert=false)
        {
            this.scope = scope;
            this.vas = vas;
            this.Name = name;
            this.Value = value;
            this.Comparison = comparison;
            this.Invert = invert;
        }

        public string Type => "variable";

        public virtual bool ConditionSatisfied(Entity e)
        {
            var avs = vas.Entity.Api.ModLoader.GetModSystem<VariablesModSystem>();
            var nowvalue = avs.GetVariable(scope, Name, e.EntityId) ?? "";
            var testvalue = this.Value ?? "";

            switch (Comparison)
            {
                case ">":
                    return nowvalue.ToDouble() > testvalue.ToDouble();
                case "<":
                    return nowvalue.ToDouble() < testvalue.ToDouble();
                case "==":
                    return nowvalue.Equals(testvalue);
            }

            return false;
        }


        public void LoadState(ITreeAttribute tree) { }
        public void StoreState(ITreeAttribute tree) { }


        public void AddGuiEditFields(ICoreClientAPI capi, GuiComposer singleComposer)
        {
            string[] comps = new string[] { ">", "<", "==" };
            string[] names = new string[] { "&gt;", "&lt;", "==" };
            var scope = new string[] { "entity", "group", "global" };

            var b = ElementBounds.Fixed(0, 0, 200, 25);
            singleComposer
                .AddStaticText("Variable name", CairoFont.WhiteDetailText(), b)
                .AddDropDown(scope, scope, (int)this.scope, null, b = b.BelowCopy(0, -5).WithFixedWidth(80), CairoFont.WhiteDetailText(), "scope")
                .AddTextInput(b.RightCopy(5).WithFixedWidth(170), null, CairoFont.WhiteDetailText(), "name")

                .AddStaticText("Comparison", CairoFont.WhiteDetailText(), b = b.BelowCopy(0, 10))
                .AddDropDown(comps, names, Math.Max(0, comps.IndexOf(Comparison)), null, b = b.BelowCopy(0, -5), CairoFont.WhiteDetailText(), "comparison")

                .AddStaticText("Value", CairoFont.WhiteDetailText(), b = b.BelowCopy(0, 10))
                .AddTextInput(b = b.BelowCopy(0, -5), null, CairoFont.WhiteDetailText(), "value")
            ;

            singleComposer.GetTextInput("name").SetValue(Name);
            singleComposer.GetTextInput("value").SetValue(Value);
        }

        public void StoreGuiEditFields(ICoreClientAPI capi, GuiComposer singleComposer)
        {
            scope = (EnumActivityVariableScope)singleComposer.GetDropDown("scope").SelectedIndices[0];
            Comparison = singleComposer.GetDropDown("comparison").SelectedValue;
            Name = singleComposer.GetTextInput("name").GetText();
            Value = singleComposer.GetTextInput("value").GetText();
        }
        public IActionCondition Clone()
        {
            return new VariableCondition(vas, scope, Name, Value, Comparison, Invert);
        }

        public override string ToString()
        {
            switch (Comparison)
            {
                case ">":
                    return string.Format("When variable {0} {1} {2}", scope + "." + Name, Invert ? "&lt;=" : "&gt;", Value);
                case "<":
                    return string.Format("When variable {0} {1} {2}", scope + "." + Name, Invert ? "&gt;=" : "&lt;", Value);
                case "==":
                    return string.Format("When variable {0} {1} {2}", scope + "." + Name, Invert ? "!=" : "==", Value);
            }

            return "unknown";
        }

        public void OnLoaded(EntityActivitySystem vas)
        {
            this.vas = vas;
        }
    }
}

