using Newtonsoft.Json;
using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{

    [JsonObject(MemberSerialization.OptIn)]
    public class RandomCondition : IActionCondition
    {
        [JsonProperty]
        public bool Invert { get; set; }
        [JsonProperty]
        float chance;
        EntityActivitySystem vas;

        public RandomCondition() { }

        public RandomCondition(EntityActivitySystem vas, float chance, bool invert = false)
        {
            this.vas = vas;
            this.chance = chance;
            this.Invert = invert;
        }

        public string Type => "random";

        public bool ConditionSatisfied(Entity e)
        {
            return e.World.Rand.NextDouble() < chance;
        }

        public void LoadState(ITreeAttribute tree) { }
        public void StoreState(ITreeAttribute tree) { }


        public void AddGuiEditFields(ICoreClientAPI capi, GuiComposer singleComposer)
        {
            var b = ElementBounds.Fixed(0, 0, 200, 25);
            singleComposer
                .AddStaticText("Chance (0..1)", CairoFont.WhiteDetailText(), b)
                .AddTextInput(b = b.BelowCopy(0, -5), null, CairoFont.WhiteDetailText(), "chance")
            ;

            singleComposer.GetTextInput("chance").SetValue(chance + "");
        }

        public IActionCondition Clone()
        {
            return new RandomCondition(vas, chance, Invert);
        }

        public void StoreGuiEditFields(ICoreClientAPI capi, GuiComposer singleComposer)
        {
            chance = singleComposer.GetTextInput("chance").GetText().ToFloat();
        }

        public override string ToString()
        {
            return (Invert ? "NOT" : "") + (int)(chance * 100) + "% chance";
        }

        public void OnLoaded(EntityActivitySystem vas)
        {
            this.vas = vas;
        }
    }
}
