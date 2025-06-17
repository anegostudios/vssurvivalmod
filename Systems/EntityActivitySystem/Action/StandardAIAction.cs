using Newtonsoft.Json;
using System;
using Vintagestory.API.Client;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent
{


    [JsonObject(MemberSerialization.OptIn)]
    public class StandardAIAction : EntityActionBase
    {
        public override string Type => "standardai";
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

        public override void OnTick(float dt)
        {
            secondsLeft -= dt;
        }

        public override bool IsFinished()
        {
            return secondsLeft <= 0;
        }

        public override void Start(EntityActivity act)
        {
            secondsLeft = durationSeconds;
        }

        public override void Cancel()
        {
            secondsLeft = 0;
        }
        public override void Finish() { secondsLeft = 0; }

        public override void AddGuiEditFields(ICoreClientAPI capi, GuiComposer singleComposer)
        {
            var b = ElementBounds.Fixed(0, 0, 200, 25);
            singleComposer
                .AddStaticText("Duration in IRL Seconds", CairoFont.WhiteDetailText(), b)
                .AddTextInput(b = b.BelowCopy(0, -5), null, CairoFont.WhiteDetailText(), "duration")
            ;

            singleComposer.GetTextInput("duration").SetValue(durationSeconds);
        }

        public override bool StoreGuiEditFields(ICoreClientAPI capi, GuiComposer singleComposer)
        {
            durationSeconds = singleComposer.GetTextInput("duration").GetText().ToFloat();
            return true;
        }

        public override IEntityAction Clone()
        {
            return new StandardAIAction(vas, durationSeconds);
        }

        public override string ToString()
        {
            return "Run standard AI for " + durationSeconds +"s";
        }

    }
}
