using Newtonsoft.Json;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public enum EnumActivityVariableScope
    {
        Entity,
        /// <summary>
        /// E.g. for the entire village
        /// </summary>
        Group,
        Global
    }


    public class ActivityVariableSystem : ModSystem
    {
        protected Dictionary<string, string> variables = new Dictionary<string, string>();
        public override bool ShouldLoad(EnumAppSide forSide) => true;
        ICoreServerAPI sapi;
        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;
            api.Event.SaveGameLoaded += Event_SaveGameLoaded;
            api.Event.GameWorldSave += Event_GameWorldSave;
        }

        private void Event_GameWorldSave()
        {
            sapi.WorldManager.SaveGame.StoreData("activityVariables", variables);
        }

        private void Event_SaveGameLoaded()
        {
            variables = sapi.WorldManager.SaveGame.GetData<Dictionary<string, string>>("activityVariables") ?? new();
        }

        public void SetVariable(long callingEntityId, EnumActivityVariableScope scope, string name, string value)
        {
            string key = "global-" + name;
            if (scope == EnumActivityVariableScope.Group)
            {
                var groupCode = sapi.World.GetEntityById(callingEntityId).WatchedAttributes.GetString("groupCode");
                key = "group-"+ groupCode + "-" + name;
            }
            if (scope == EnumActivityVariableScope.Entity)
            {
                key = "entity-" + callingEntityId + "-" + name;
            }

            variables[key] = value;
        }

        public string GetVariable(EnumActivityVariableScope scope, string name, long callingEntityId)
        {
            string key = "global-" + name;
            if (scope == EnumActivityVariableScope.Group)
            {
                var groupCode = sapi.World.GetEntityById(callingEntityId).WatchedAttributes.GetString("groupCode");
                key = "group-" + groupCode + "-" + name;
            }
            if (scope == EnumActivityVariableScope.Entity)
            {
                key = "entity-" + callingEntityId + "-" + name;
            }

            variables.TryGetValue(key, out var variable);
            return variable;
        }
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class SetVarAction : IEntityAction
    {
        protected EntityActivitySystem vas;
        public string Type => "setvariable";
        public bool ExecutionHasFailed { get; set; }

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

        public bool IsFinished()
        {
            return true;
        }

        public void Start(EntityActivity act)
        {
            var avs = vas.Entity.Api.ModLoader.GetModSystem<ActivityVariableSystem>();

            switch (op)
            {
                case "set":
                    avs.SetVariable(vas.Entity.EntityId, scope, name, value);
                    break;
                case "incrementby":
                case "decrementby":
                    var curvalue = avs.GetVariable(scope, name, vas.Entity.EntityId);
                    int sign = op == "decrementby" ? -1 : 1;
                    avs.SetVariable(vas.Entity.EntityId, scope, name, "" + (curvalue.ToDouble() + sign*value.ToDouble()));
                    break;
            }
        }


        public void OnTick(float dt) { }
        public void Cancel() { }
        public void Finish() { }

        public void LoadState(ITreeAttribute tree) { }
        public void StoreState(ITreeAttribute tree) { }


        public void AddGuiEditFields(ICoreClientAPI capi, GuiComposer singleComposer)
        {
            var scope = new string[] { "entity", "group", "global" };
            var ops = new string[] { "set", "incrementby", "decrementby" };

            var b = ElementBounds.Fixed(0, 0, 300, 25);
            singleComposer
                .AddStaticText("Variable Scope", CairoFont.WhiteDetailText(), b)
                .AddDropDown(scope, scope, (int)this.scope, null, b = b.BelowCopy(0, -5), CairoFont.WhiteDetailText(), "scope")

                .AddStaticText("Operation", CairoFont.WhiteDetailText(), b = b.BelowCopy(0, 15))
                .AddDropDown(ops, ops, (int)System.Math.Max(0, ops.IndexOf(op)), null, b = b.BelowCopy(0, -5), CairoFont.WhiteDetailText(), "op")

                .AddStaticText("Name", CairoFont.WhiteDetailText(), b = b.BelowCopy(0, 15).WithFixedWidth(50))
                .AddTextInput(b = b.BelowCopy(0, 5), null, CairoFont.WhiteDetailText(), "name")

                .AddStaticText("Value", CairoFont.WhiteDetailText(), b = b.BelowCopy(0, 15).WithFixedWidth(50))
                .AddTextInput(b = b.BelowCopy(0, 5), null, CairoFont.WhiteDetailText(), "value")
            ;

            singleComposer.GetTextInput("name").SetValue(name);
            singleComposer.GetTextInput("value").SetValue(value);
        }

        public bool StoreGuiEditFields(ICoreClientAPI capi, GuiComposer singleComposer)
        {
            scope = (EnumActivityVariableScope)singleComposer.GetDropDown("scope").SelectedIndices[0];
            op = singleComposer.GetDropDown("op").SelectedValue;
            name = singleComposer.GetTextInput("name").GetText();
            value = singleComposer.GetTextInput("value").GetText();
            return true;
        }

        public IEntityAction Clone()
        {
            return new SetVarAction(vas, scope, op, name, value);
        }

        public override string ToString()
        {
            var avs = vas?.Entity.Api.ModLoader.GetModSystem<ActivityVariableSystem>();
            string curvalue=null;
            if (avs != null)
            {
                curvalue = avs.GetVariable(scope, name, vas.Entity.EntityId);
            }

            if (op == "incrementby" || op == "decrementby") {
                return string.Format("{3} {0} variable {1} by {2}", scope, name, value, op == "incrementby" ? "Increment" : "Decrement");
            }

            return string.Format("Set {0} variable {1} to {2}", scope, name, value);
        }

        public void OnVisualize(ActivityVisualizer visualizer)
        {

        }
        public void OnLoaded(EntityActivitySystem vas)
        {
            this.vas = vas;
        }
    }
}
