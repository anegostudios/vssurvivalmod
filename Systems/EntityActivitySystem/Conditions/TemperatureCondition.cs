using Newtonsoft.Json;
using Vintagestory.API.Client;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;

#nullable disable

namespace Vintagestory.GameContent
{
    [JsonObject(MemberSerialization.OptIn)]
    public class TemperatureCondition : IActionCondition
    {
        [JsonProperty]
        public bool Invert { get; set; }
        [JsonProperty]
        float belowTemperature;

        protected EntityActivitySystem vas;
        public TemperatureCondition() { }
        public TemperatureCondition(EntityActivitySystem vas, float belowTemperature, bool invert)
        {
            this.vas = vas;
            this.belowTemperature = belowTemperature;
            this.Invert = invert;
        }

        public string Type => "temperature";

        public virtual bool ConditionSatisfied(Entity e)
        {
            var api = vas.Entity.Api;
            var climate = api.World.BlockAccessor.GetClimateAt(e.Pos.AsBlockPos, API.Common.EnumGetClimateMode.ForSuppliedDate_TemperatureOnly, api.World.Calendar.TotalDays);
            return climate.Temperature < belowTemperature;
        }


        public void LoadState(ITreeAttribute tree) { }
        public void StoreState(ITreeAttribute tree) { }


        public void AddGuiEditFields(ICoreClientAPI capi, GuiComposer singleComposer)
        {
            var b = ElementBounds.Fixed(0, 0, 200, 25);
            singleComposer
                .AddStaticText("When below temperature", CairoFont.WhiteDetailText(), b)
                .AddNumberInput(b = b.BelowCopy(0, -5), null, CairoFont.WhiteDetailText(), "temp")
            ;

            singleComposer.GetNumberInput("temp").SetValue(belowTemperature + "");
        }

        public void StoreGuiEditFields(ICoreClientAPI capi, GuiComposer singleComposer)
        {
            belowTemperature = singleComposer.GetNumberInput("temp").GetValue();
        }
        public IActionCondition Clone()
        {
            return new TemperatureCondition(vas, belowTemperature, Invert);
        }

        public override string ToString()
        {
            return Invert ? "When above temperature " + belowTemperature : "When below temperature" + belowTemperature;
        }

        public void OnLoaded(EntityActivitySystem vas)
        {
            this.vas = vas;
        }
    }
}
