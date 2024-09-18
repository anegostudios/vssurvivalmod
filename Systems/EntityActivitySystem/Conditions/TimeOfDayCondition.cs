using Newtonsoft.Json;
using Vintagestory.API.Client;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{

    [JsonObject(MemberSerialization.OptIn)]
    public class TimeOfDayCondition : IActionCondition
    {
        [JsonProperty]
        public bool Invert { get; set; }
        [JsonProperty]
        float minHour;
        [JsonProperty]
        float maxHour;

        protected EntityActivitySystem vas;
        public TimeOfDayCondition() { }
        public TimeOfDayCondition(EntityActivitySystem vas, float minHour, float maxHour, bool invert=false)
        {
            this.vas = vas;
            this.minHour = minHour;
            this.maxHour = maxHour;
            this.Invert = invert;
        }

        public string Type => "timeofday";

        public virtual bool ConditionSatisfied(Entity e)
        {
            var api = vas.Entity.Api;
            var hourRel = api.World.Calendar.HourOfDay / api.World.Calendar.HoursPerDay;

            var minHourRel = minHour / 24f;
            var maxHourRel = maxHour / 24f;

            return hourRel >= minHourRel && hourRel <= maxHourRel;
        }


        public void LoadState(ITreeAttribute tree) { }
        public void StoreState(ITreeAttribute tree) { }


        public void AddGuiEditFields(ICoreClientAPI capi, GuiComposer singleComposer)
        {
            var b = ElementBounds.Fixed(0, 0, 200, 25);
            singleComposer
                .AddStaticText("Min Hour", CairoFont.WhiteDetailText(), b)
                .AddTextInput(b = b.BelowCopy(0, -5), null, CairoFont.WhiteDetailText(), "minHour")

                .AddStaticText("Max Hour", CairoFont.WhiteDetailText(), b = b.BelowCopy(0, 10))
                .AddTextInput(b = b.BelowCopy(0, -5), null, CairoFont.WhiteDetailText(), "maxHour")
            ;

            singleComposer.GetTextInput("minHour").SetValue(minHour + "");
            singleComposer.GetTextInput("maxHour").SetValue(maxHour + "");
        }

        public void StoreGuiEditFields(ICoreClientAPI capi, GuiComposer singleComposer)
        {
            minHour = singleComposer.GetTextInput("minHour").GetText().ToFloat();
            maxHour = singleComposer.GetTextInput("maxHour").GetText().ToFloat();
        }
        public IActionCondition Clone()
        {
            return new TimeOfDayCondition(vas, minHour, maxHour, Invert);
        }

        public override string ToString()
        {
            return Invert ? "Time of day, outside of hours " + minHour + " to " + maxHour : "Time of day, from hour " + minHour + " until " + maxHour;
        }

        public void OnLoaded(EntityActivitySystem vas)
        {
            this.vas = vas;
        }
    }
}
