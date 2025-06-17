using Newtonsoft.Json;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent
{
    public enum EnumActivityVariableScope
    {
        /// <summary>
        /// This entity specifically
        /// </summary>
        Entity,
        /// <summary>
        /// E.g. for the entire village
        /// </summary>
        Group,
        /// <summary>
        /// For this player
        /// </summary>
        Player,
        /// <summary>
        /// Everywhere
        /// </summary>
        Global,
        /// <summary>
        /// Per entity, per player
        /// </summary>
        EntityPlayer
    }


    [JsonObject(MemberSerialization.OptIn)]
    public class SetVarAction : EntityActionBase
    {
        public override string Type => "setvariable";

        [JsonProperty]
        EnumActivityVariableScope scope;
        [JsonProperty]
        string op;
        [JsonProperty]
        string name;
        [JsonProperty]
        string value;

        public SetVarAction() { }

        public SetVarAction(EntityActivitySystem vas, EnumActivityVariableScope scope, string op, string name, string value)
        {
            this.vas = vas;
            this.op = op;
            this.scope = scope;
            this.name = name;
            this.value = value;
        }

        public override void Start(EntityActivity act)
        {
            var avs = vas.Entity.Api.ModLoader.GetModSystem<VariablesModSystem>();

            switch (op)
            {
                case "set":
                    avs.SetVariable(vas.Entity, scope, name, value);
                    break;
                case "incrementby":
                case "decrementby":
                    var curvalue = avs.GetVariable(scope, name, vas.Entity);
                    int sign = op == "decrementby" ? -1 : 1;
                    avs.SetVariable(vas.Entity, scope, name, "" + (curvalue.ToDouble() + sign*value.ToDouble()));
                    break;
            }
        }


        public override void AddGuiEditFields(ICoreClientAPI capi, GuiComposer singleComposer)
        {
            var scope = new string[] { "entity", "group", "global" };
            var ops = new string[] { "set", "incrementby", "decrementby" };

            var b = ElementBounds.Fixed(0, 0, 300, 25);
            singleComposer
                .AddStaticText("Variable Scope", CairoFont.WhiteDetailText(), b)
                .AddDropDown(scope, scope, (int)this.scope, null, b = b.BelowCopy(0, -5), CairoFont.WhiteDetailText(), "scope")

                .AddStaticText("Operation", CairoFont.WhiteDetailText(), b = b.BelowCopy(0, 15))
                .AddDropDown(ops, ops, (int)System.Math.Max(0, ops.IndexOf(op)), null, b = b.BelowCopy(0, -5), CairoFont.WhiteDetailText(), "op")

                .AddStaticText("Name", CairoFont.WhiteDetailText(), b = b.BelowCopy(0, 15).WithFixedWidth(150))
                .AddTextInput(b = b.BelowCopy(0, -5), null, CairoFont.WhiteDetailText(), "name")

                .AddStaticText("Value", CairoFont.WhiteDetailText(), b = b.BelowCopy(0, 15).WithFixedWidth(150))
                .AddTextInput(b = b.BelowCopy(0, -5), null, CairoFont.WhiteDetailText(), "value")
            ;

            singleComposer.GetTextInput("name").SetValue(name);
            singleComposer.GetTextInput("value").SetValue(value);
        }

        public override bool StoreGuiEditFields(ICoreClientAPI capi, GuiComposer singleComposer)
        {
            scope = (EnumActivityVariableScope)singleComposer.GetDropDown("scope").SelectedIndices[0];
            op = singleComposer.GetDropDown("op").SelectedValue;
            name = singleComposer.GetTextInput("name").GetText();
            value = singleComposer.GetTextInput("value").GetText();
            return true;
        }

        public override IEntityAction Clone()
        {
            return new SetVarAction(vas, scope, op, name, value);
        }

        public override string ToString()
        {
            var avs = vas?.Entity.Api.ModLoader.GetModSystem<VariablesModSystem>();
            string curvalue=null;
            if (avs != null)
            {
                curvalue = avs.GetVariable(scope, name, vas.Entity);
            }

            if (op == "incrementby" || op == "decrementby") {
                return string.Format("{3} {0} variable {1} by {2}", scope, name, value, op == "incrementby" ? "Increment" : "Decrement");
            }

            return string.Format("Set {0} variable {1} to {2}", scope, name, value);
        }

    }
}
