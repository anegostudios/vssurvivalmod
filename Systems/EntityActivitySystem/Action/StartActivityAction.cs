using Newtonsoft.Json;
using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    [JsonObject(MemberSerialization.OptIn)]
    public class StartActivityAction : EntityActionBase
    {
        public override string Type => "startactivity";

        [JsonProperty]
        string activityCode;
        [JsonProperty]
        string target;
        [JsonProperty]
        float priority = -1f;
        [JsonProperty]
        int slot=-1;
        [JsonProperty]
        string entitySelector;

        public StartActivityAction() { }

        public StartActivityAction(EntityActivitySystem vas, string activityCode, int slot, string target, float priority, string entitySelector)
        {
            this.vas = vas;
            this.activityCode = activityCode;
            this.slot = slot;
            this.target = target;
            this.priority = priority;
            this.entitySelector = entitySelector;
        }



        public override void Start(EntityActivity act)
        {
            string code = activityCode;

            if (activityCode.Contains(","))
            {
                var codes = activityCode.Split(',');
                code = codes[vas.Entity.World.Rand.Next(codes.Length)].Trim();
            }

            if (target == "other")
            {
                EntitiesArgParser parser = new EntitiesArgParser("target", vas.Entity.Api, true);
                TextCommandCallingArgs args = new TextCommandCallingArgs()
                {
                    Caller = new Caller() { Entity = vas.Entity, Pos = vas.Entity.ServerPos.XYZ, Type = EnumCallerType.Entity },
                    RawArgs = new CmdArgs(entitySelector)
                };
                var result = parser.TryProcess(args);
                if (result == EnumParseResult.Good)
                {
                    var entities = parser.GetValue() as Entity[];
                    foreach (var entity in entities)
                    {
                        if (entity.EntityId == vas.Entity.EntityId) continue;
                        entity.GetBehavior<EntityBehaviorActivityDriven>()?.ActivitySystem.StartActivity(code, priority, slot);
                    }
                } else
                {
                    vas.Entity.World.Logger.Debug("Unable to parse entity selector '{0}' for the Entity Activity Action 'StartActivity' - {1}", entitySelector, parser.LastErrorMessage);
                }
            }
            else
            {
                if(vas.Debug)
                    vas.Entity.World.Logger.Debug("StartActivity, starting {0}", code);
                vas.StartActivity(code, priority, slot);
            }
        }



        public override void AddGuiEditFields(ICoreClientAPI capi, GuiComposer singleComposer)
        {
            var values = new string[] { "self", "other" };
            var b = ElementBounds.Fixed(0, 0, 350, 25);
            singleComposer
                .AddStaticText("Activity code, or 'multiple,randomly,selected,codes'", CairoFont.WhiteDetailText(), b)
                .AddTextInput(b = b.BelowCopy(0, -5), null, CairoFont.WhiteDetailText(), "codes")

                .AddStaticText("Activity slot (-1 for default)", CairoFont.WhiteDetailText(), b = b.BelowCopy(0, 5).WithFixedWidth(200))
                .AddNumberInput(b = b.BelowCopy(0, -5).WithFixedWidth(80), null, CairoFont.WhiteDetailText(), "slot")

                .AddStaticText("Priority (-1 to ignore priority)", CairoFont.WhiteDetailText(), b = b.BelowCopy(0, 5).WithFixedWidth(200))
                .AddNumberInput(b = b.BelowCopy(0, -5).WithFixedWidth(80), null, CairoFont.WhiteDetailText(), "priority")

                .AddStaticText("Target entity", CairoFont.WhiteDetailText(), b = b.BelowCopy(0, 5).WithFixedWidth(200))
                .AddDropDown(values, new string[] { "On self", "On another entity" }, values.IndexOf(target), (code, selected)=>onSeleChanged(code, singleComposer), b=b.BelowCopy(0,-5).WithFixedWidth(100), "target")

                .AddStaticText("Target entity selector", CairoFont.WhiteDetailText(), b = b.BelowCopy(0, 5).WithFixedWidth(200))
                .AddTextInput(b = b.BelowCopy(0, -5).WithFixedWidth(200), null, CairoFont.WhiteDetailText(), "selector")
            ;

            singleComposer.GetTextInput("codes").SetValue(activityCode);
            singleComposer.GetNumberInput("slot").SetValue(slot);
            singleComposer.GetNumberInput("priority").SetValue(priority);
            singleComposer.GetTextInput("selector").SetValue(entitySelector);
        }

        private void onSeleChanged(string code, GuiComposer singleComposer)
        {
            singleComposer.GetTextInput("selector").Enabled = code == "other";
        }

        public override bool StoreGuiEditFields(ICoreClientAPI capi, GuiComposer singleComposer)
        {
            activityCode = singleComposer.GetTextInput("codes").GetText();
            slot = (int)singleComposer.GetNumberInput("slot").GetValue();
            target = singleComposer.GetDropDown("target").SelectedValue;
            priority = singleComposer.GetNumberInput("priority").GetValue();
            entitySelector = singleComposer.GetTextInput("selector").GetText();
            return true;
        }

        public override IEntityAction Clone()
        {
            return new StartActivityAction(vas, activityCode, slot, target, priority, entitySelector);
        }

        public override string ToString()
        {
            if (target == "other")
            {
                return activityCode.Contains(",") ? ("Start random activity on "+entitySelector+" (" + activityCode + ")") : ("Start activity " + activityCode + " on " + entitySelector);
            }
            return activityCode.Contains(",") ? ("Start random activity (" + activityCode +")") : ("Start activity " + activityCode);
        }

    }
}
