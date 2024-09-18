using Newtonsoft.Json;
using Vintagestory.API.Client;
using Vintagestory.API.Datastructures;

namespace Vintagestory.GameContent
{

    [JsonObject(MemberSerialization.OptIn)]
    public class JumpAction : IEntityAction
    {
        protected EntityActivitySystem vas;
        public string Type => "jump";
        public bool ExecutionHasFailed { get; set; }

        [JsonProperty]
        int Index;
        [JsonProperty]
        float hourOfDay;


        public JumpAction() { }

        public JumpAction(EntityActivitySystem vas, int index, float hourOfDay)
        {
            this.vas = vas;
            this.Index = index;
            this.hourOfDay = hourOfDay;
        }

        public bool IsFinished()
        {
            return true;
        }

        public void Start(EntityActivity act)
        {
            if (vas.Entity.World.Calendar.HourOfDay > hourOfDay)
            {
                return;
            }
            act.currentActionIndex = Index;
            act.CurrentAction?.Start(act);
        }


        public void OnTick(float dt) { }
        public void Cancel() { }
        public void Finish() { }

        public void LoadState(ITreeAttribute tree) { }
        public void StoreState(ITreeAttribute tree) { }


        public void AddGuiEditFields(ICoreClientAPI capi, GuiComposer singleComposer)
        {
            var b = ElementBounds.Fixed(0, 0, 300, 25);
            singleComposer
                .AddStaticText("Jump to Action Index", CairoFont.WhiteDetailText(), b)
                .AddNumberInput(b = b.BelowCopy(0, -5), null, CairoFont.WhiteDetailText(), "index")

                .AddStaticText("Until hour of day (0..24)", CairoFont.WhiteDetailText(), b = b.BelowCopy(0, 15))
                .AddNumberInput(b = b.BelowCopy(0,  -5), null, CairoFont.WhiteDetailText(), "hourOfDay")
            ;

            singleComposer.GetTextInput("index").SetValue(Index);
            singleComposer.GetNumberInput("hourOfDay").SetValue(hourOfDay);
        }

        public bool StoreGuiEditFields(ICoreClientAPI capi, GuiComposer singleComposer)
        {
            Index = (int)singleComposer.GetNumberInput("index").GetValue();
            hourOfDay = singleComposer.GetNumberInput("hourOfDay").GetValue();
            return true;
        }

        public IEntityAction Clone()
        {
            return new JumpAction(vas, Index, hourOfDay);
        }

        public override string ToString()
        {
            return "Jump to action at index " + Index;
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
