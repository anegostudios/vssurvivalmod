using Newtonsoft.Json;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;

#nullable disable

namespace Vintagestory.GameContent
{
    [JsonObject(MemberSerialization.OptIn)]
    public class AnimationCondition : IActionCondition
    {
        [JsonProperty]
        public bool Invert { get; set; }
        [JsonProperty]
        string animCode = "";

        protected EntityActivitySystem vas;
        public AnimationCondition() { }
        public AnimationCondition(EntityActivitySystem vas, string animCode, bool invert = false)
        {
            this.vas = vas;
            this.animCode = animCode;
            this.Invert = invert;
        }

        public string Type => "animation";

        public virtual bool ConditionSatisfied(Entity e)
        {
            if (animCode.Contains(","))
            {
                return vas.Entity.AnimManager.IsAnimationActive(animCode.Split(","));
            }

            return vas.Entity.AnimManager.IsAnimationActive(animCode);
        }


        public void LoadState(ITreeAttribute tree) { }
        public void StoreState(ITreeAttribute tree) { }


        public void AddGuiEditFields(ICoreClientAPI capi, GuiComposer singleComposer)
        {
            var b = ElementBounds.Fixed(0, 0, 200, 25);
            singleComposer
                .AddStaticText("Animation Code", CairoFont.WhiteDetailText(), b)
                .AddTextInput(b = b.BelowCopy(0, -5), null, CairoFont.WhiteDetailText(), "animCode")
            ;

            singleComposer.GetTextInput("animCode").SetValue(animCode);
        }

        public void StoreGuiEditFields(ICoreClientAPI capi, GuiComposer singleComposer)
        {
            animCode = singleComposer.GetTextInput("animCode").GetText();
        }
        public IActionCondition Clone()
        {
            return new AnimationCondition(vas, animCode, Invert);
        }

        public override string ToString()
        {
            return (Invert ? "When animation " + animCode + " does not play" : "When animation "+ animCode + " plays");
        }

        public void OnLoaded(EntityActivitySystem vas)
        {
            this.vas = vas;
        }
    }
}
