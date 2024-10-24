using Newtonsoft.Json;
using Vintagestory.API.Client;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{

    [JsonObject(MemberSerialization.OptIn)]
    public class MonthCondition : IActionCondition
    {
        [JsonProperty]
        public bool Invert { get; set; }
        [JsonProperty]
        int month;

        protected EntityActivitySystem vas;
        public MonthCondition() { }
        public MonthCondition(EntityActivitySystem vas, int month, bool invert=false)
        {
            this.vas = vas;
            this.month = month;
            this.Invert = invert;
        }

        public string Type => "month";

        public virtual bool ConditionSatisfied(Entity e)
        {
            var api = vas.Entity.Api;
            return api.World.Calendar.Month == month;
        }


        public void LoadState(ITreeAttribute tree) { }
        public void StoreState(ITreeAttribute tree) { }


        public void AddGuiEditFields(ICoreClientAPI capi, GuiComposer singleComposer)
        {
            var b = ElementBounds.Fixed(0, 0, 200, 25);
            singleComposer
                .AddStaticText("Month (1..12)", CairoFont.WhiteDetailText(), b)
                .AddNumberInput(b = b.BelowCopy(0, -5), null, CairoFont.WhiteDetailText(), "month")
            ;

            singleComposer.GetNumberInput("month").SetValue(month + "");
        }

        public void StoreGuiEditFields(ICoreClientAPI capi, GuiComposer singleComposer)
        {
            month = (int)singleComposer.GetNumberInput("month").GetValue();
        }
        public IActionCondition Clone()
        {
            return new MonthCondition(vas, month, Invert);
        }

        public override string ToString()
        {
            return Invert ? "Outside the month " + month : "On the month " + month;
        }

        public void OnLoaded(EntityActivitySystem vas)
        {
            this.vas = vas;
        }
    }
}
