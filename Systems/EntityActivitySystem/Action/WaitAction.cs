using Newtonsoft.Json;
using Vintagestory.API.Client;
using Vintagestory.API.Datastructures;

namespace Vintagestory.GameContent
{
    [JsonObject(MemberSerialization.OptIn)]
    public class WaitAction : IEntityAction
    {
        protected EntityActivitySystem vas;
        public string Type => "wait";
        public bool ExecutionHasFailed { get; set; }

        [JsonProperty]
        float durationSeconds;

        float waitLeftSeconds;

        public WaitAction() { }

        public WaitAction(EntityActivitySystem vas, float durationSeconds)
        {
            this.vas = vas;
            this.durationSeconds = durationSeconds;
        }

        public bool IsFinished()
        {
            return waitLeftSeconds < 0;
        }

        public void Start(EntityActivity act)
        {
            waitLeftSeconds = durationSeconds;
        }


        public void OnTick(float dt) {
            waitLeftSeconds -= dt;
        }
        public void Cancel()
        {
            // re-equip here
        }
        public void Finish() { }

        public void LoadState(ITreeAttribute tree) { }
        public void StoreState(ITreeAttribute tree) { }


        public void AddGuiEditFields(ICoreClientAPI capi, GuiComposer singleComposer)
        {
            var b = ElementBounds.Fixed(0, 0, 300, 25);
            singleComposer
                .AddStaticText("Wait IRL seconds", CairoFont.WhiteDetailText(), b)
                .AddNumberInput(b = b.BelowCopy(0, -5), null, CairoFont.WhiteDetailText(), "wait")
            ;

            singleComposer.GetTextInput("wait").SetValue(durationSeconds);
        }

        public bool StoreGuiEditFields(ICoreClientAPI capi, GuiComposer singleComposer)
        {
            durationSeconds = singleComposer.GetNumberInput("wait").GetValue();
            return true;
        }

        public IEntityAction Clone()
        {
            return new WaitAction(vas, durationSeconds);
        }

        public override string ToString()
        {
            return "Wait for " + durationSeconds + " IRL seconds";
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
