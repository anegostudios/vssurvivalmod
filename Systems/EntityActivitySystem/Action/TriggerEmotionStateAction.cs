using Newtonsoft.Json;
using Vintagestory.API.Client;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    [JsonObject(MemberSerialization.OptIn)]
    public class TriggerEmotionStateAction : IEntityAction
    {
        public bool ExecutionHasFailed => false;
        public string Type => "triggeremotionstate";

        EntityActivitySystem vas;
        [JsonProperty]
        public string emotionState;


        public TriggerEmotionStateAction(EntityActivitySystem vas, string emotionState)
        {
            this.vas = vas;
            this.emotionState = emotionState;
        }

        public TriggerEmotionStateAction() { }

        public void OnTick(float dt)
        {
            
        }

        public bool IsFinished()
        {
            return true;
        }

        public void Start(EntityActivity act)
        {
            vas.Entity.GetBehavior<EntityBehaviorEmotionStates>()?.TryTriggerState(emotionState, vas.Entity.EntityId);
        }

        public void Cancel()
        {
            
        }
        public void Finish() { }

        public void AddGuiEditFields(ICoreClientAPI capi, GuiComposer singleComposer)
        {
            var b = ElementBounds.Fixed(0, 0, 200, 25);
            singleComposer
                .AddStaticText("Emotion State Code", CairoFont.WhiteDetailText(), b)
                .AddTextInput(b = b.BelowCopy(0, -5), null, CairoFont.WhiteDetailText(), "emotionState")
            ;

            singleComposer.GetTextInput("emotionState").SetValue(emotionState);
        }

        public bool StoreGuiEditFields(ICoreClientAPI capi, GuiComposer singleComposer)
        {
            emotionState = singleComposer.GetTextInput("emotionState").GetText();
            return true;
        }


        public void LoadState(ITreeAttribute tree) { }
        public void StoreState(ITreeAttribute tree) { }


        public IEntityAction Clone()
        {
            return new TriggerEmotionStateAction(vas, emotionState);
        }

        public override string ToString()
        {
            return "Trigger emotion state " + emotionState;
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
