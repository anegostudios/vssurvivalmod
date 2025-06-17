using Newtonsoft.Json;
using Vintagestory.API.Client;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent
{
    [JsonObject(MemberSerialization.OptIn)]
    public class TriggerEmotionStateAction : EntityActionBase
    {
        public override string Type => "triggeremotionstate";

        [JsonProperty]
        public string emotionState;


        public TriggerEmotionStateAction(EntityActivitySystem vas, string emotionState)
        {
            this.vas = vas;
            this.emotionState = emotionState;
        }

        public TriggerEmotionStateAction() { }


        public override void Start(EntityActivity act)
        {
            vas.Entity.GetBehavior<EntityBehaviorEmotionStates>()?.TryTriggerState(emotionState, vas.Entity.EntityId);
        }


        public override void AddGuiEditFields(ICoreClientAPI capi, GuiComposer singleComposer)
        {
            var b = ElementBounds.Fixed(0, 0, 200, 25);
            singleComposer
                .AddStaticText("Emotion State Code", CairoFont.WhiteDetailText(), b)
                .AddTextInput(b = b.BelowCopy(0, -5), null, CairoFont.WhiteDetailText(), "emotionState")
            ;

            singleComposer.GetTextInput("emotionState").SetValue(emotionState);
        }

        public override bool StoreGuiEditFields(ICoreClientAPI capi, GuiComposer singleComposer)
        {
            emotionState = singleComposer.GetTextInput("emotionState").GetText();
            return true;
        }


        public override IEntityAction Clone()
        {
            return new TriggerEmotionStateAction(vas, emotionState);
        }

        public override string ToString()
        {
            return "Trigger emotion state " + emotionState;
        }
    }
}
