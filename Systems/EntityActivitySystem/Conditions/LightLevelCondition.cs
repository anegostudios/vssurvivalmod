using Newtonsoft.Json;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent
{

    [JsonObject(MemberSerialization.OptIn)]
    public class LightLevelCondition : IActionCondition
    {
        [JsonProperty]
        public bool Invert { get; set; }
        [JsonProperty]
        float minLight;
        [JsonProperty]
        float maxLight;
        [JsonProperty]
        EnumLightLevelType lightLevelType;

        protected EntityActivitySystem vas;
        public LightLevelCondition() { }
        public LightLevelCondition(EntityActivitySystem vas, float minLight, float maxLight, EnumLightLevelType lightLevelType, bool invert=false)
        {
            this.vas = vas;
            this.minLight = minLight;
            this.maxLight = maxLight;
            this.lightLevelType = lightLevelType;
            this.Invert = invert;
        }

        public string Type => "lightlevel";

        public virtual bool ConditionSatisfied(Entity e)
        {
            int lightLevel = e.Api.World.BlockAccessor.GetLightLevel(e.Pos.AsBlockPos, lightLevelType);
            return lightLevel >= minLight && lightLevel <= maxLight;
        }


        public void LoadState(ITreeAttribute tree) { }
        public void StoreState(ITreeAttribute tree) { }


        public void AddGuiEditFields(ICoreClientAPI capi, GuiComposer singleComposer)
        {
            string[] values = new string[] { "0","1","2","3","4","5" };
            string[] names = System.Enum.GetNames(typeof(EnumLightLevelType));

            var b = ElementBounds.Fixed(0, 0, 200, 25);
            singleComposer
                .AddStaticText("Light level type", CairoFont.WhiteDetailText(), b)
                .AddDropDown(values, names, (int)lightLevelType, null, b = b.BelowCopy(0, -5), CairoFont.WhiteDetailText(), "lightleveltype")

                .AddStaticText("Min light level (0..32)", CairoFont.WhiteDetailText(), b = b.BelowCopy(0, 10))
                .AddNumberInput(b = b.BelowCopy(0, -5), null, CairoFont.WhiteDetailText(), "minLight")

                .AddStaticText("Max light level (0..32)", CairoFont.WhiteDetailText(), b = b.BelowCopy(0, 10))
                .AddNumberInput(b = b.BelowCopy(0, -5), null, CairoFont.WhiteDetailText(), "maxLight")
            ;

            singleComposer.GetNumberInput("minLight").SetValue(minLight + "");
            singleComposer.GetNumberInput("maxLight").SetValue(maxLight + "");
        }

        public void StoreGuiEditFields(ICoreClientAPI capi, GuiComposer singleComposer)
        {
            minLight = singleComposer.GetNumberInput("minLight").GetValue();
            maxLight = singleComposer.GetNumberInput("maxLight").GetValue();
            lightLevelType = (EnumLightLevelType)singleComposer.GetDropDown("lightleveltype").SelectedIndices[0];
        }
        public IActionCondition Clone()
        {
            return new LightLevelCondition(vas, minLight, maxLight, lightLevelType, Invert);
        }

        public override string ToString()
        {
            return Invert ? string.Format("{0} outside of {1} to {2}", lightLevelType, minLight, maxLight) : string.Format("{0} within {1} to {2}", lightLevelType, minLight, maxLight);
        }

        public void OnLoaded(EntityActivitySystem vas)
        {
            this.vas = vas;
        }
    }
}
