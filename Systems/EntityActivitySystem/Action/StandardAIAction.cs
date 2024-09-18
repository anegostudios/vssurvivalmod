using Newtonsoft.Json;
using System;
using Vintagestory.API.Client;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{


    [JsonObject(MemberSerialization.OptIn)]
    public class StandardAIAction : IEntityAction
    {
        public bool ExecutionHasFailed => false;
        public string Type => "standardai";

        EntityActivitySystem vas;
        [JsonProperty]
        public float durationSeconds;

        float secondsLeft;

        public StandardAIAction(EntityActivitySystem vas, float durationSeconds)
        {
            this.vas = vas;
            this.durationSeconds = durationSeconds;
        }

        public StandardAIAction()
        {
        }

        public void OnTick(float dt)
        {
            secondsLeft -= dt;
        }

        public bool IsFinished()
        {
            return secondsLeft <= 0;
        }

        public void Start(EntityActivity act)
        {
            secondsLeft = durationSeconds;
        }

        public void Cancel()
        {
            secondsLeft = 0;
        }
        public void Finish() { secondsLeft = 0; }

        public void AddGuiEditFields(ICoreClientAPI capi, GuiComposer singleComposer)
        {
            var b = ElementBounds.Fixed(0, 0, 200, 25);
            singleComposer
                .AddStaticText("Duration in IRL Seconds", CairoFont.WhiteDetailText(), b)
                .AddTextInput(b = b.BelowCopy(0, -5), null, CairoFont.WhiteDetailText(), "duration")
            ;

            singleComposer.GetTextInput("duration").SetValue(durationSeconds);
        }

        public bool StoreGuiEditFields(ICoreClientAPI capi, GuiComposer singleComposer)
        {
            durationSeconds = singleComposer.GetTextInput("duration").GetText().ToFloat();
            return true;
        }


        public void LoadState(ITreeAttribute tree) { }
        public void StoreState(ITreeAttribute tree) { }


        public IEntityAction Clone()
        {
            return new StandardAIAction(vas, durationSeconds);
        }

        public override string ToString()
        {
            return "Run standard AI for " + durationSeconds +"s";
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
