using Newtonsoft.Json;
using Vintagestory.API.Client;
using Vintagestory.API.Datastructures;

namespace Vintagestory.GameContent
{
    [JsonObject(MemberSerialization.OptIn)]
    public class StopAnimationAction : IEntityAction
    {
        protected EntityActivitySystem vas;
        [JsonProperty]
        protected string Animation;

        public StopAnimationAction() { }

        public StopAnimationAction(EntityActivitySystem vas)
        {
            this.vas = vas;
        }

        public StopAnimationAction(EntityActivitySystem vas, string anim)
        {
            this.Animation = anim;
        }

        public bool ExecutionHasFailed { get; set; }

        public string Type => "stopanimation";

        public bool IsFinished()
        {
            return true;
        }

        public void Start(EntityActivity act)
        {
            vas.Entity.AnimManager.StopAnimation(Animation);
        }

        public void Cancel()
        {
            Finish();
        }
        public void Finish()
        {
        }
        public void LoadState(ITreeAttribute tree)
        {
        }
        public void StoreState(ITreeAttribute tree)
        {
        }


        public void AddGuiEditFields(ICoreClientAPI capi, GuiComposer singleComposer)
        {
            var b = ElementBounds.Fixed(0, 0, 200, 25);
            singleComposer
                .AddStaticText("Animation Code", CairoFont.WhiteDetailText(), b)
                .AddTextInput(b = b.BelowCopy(0, -5), null, CairoFont.WhiteDetailText(), "animation")
            ;

            singleComposer.GetTextInput("animation").SetValue(Animation ?? "");
        }


        public IEntityAction Clone()
        {
            return new StopAnimationAction(vas, Animation);
        }

        public bool StoreGuiEditFields(ICoreClientAPI capi, GuiComposer singleComposer)
        {
            Animation = singleComposer.GetTextInput("animation").GetText();
            return true;
        }

        public override string ToString()
        {
            return "Stop animation " + Animation;
        }

        public void OnTick(float dt)
        {
            
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
