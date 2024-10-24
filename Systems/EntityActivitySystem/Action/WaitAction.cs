using Newtonsoft.Json;
using Vintagestory.API.Client;
using Vintagestory.API.Datastructures;

namespace Vintagestory.GameContent
{
    [JsonObject(MemberSerialization.OptIn)]
    public class WaitAction : EntityActionBase
    {
        public override string Type => "wait";

        [JsonProperty]
        float durationSeconds;

        float waitLeftSeconds;

        public WaitAction() { }

        public WaitAction(EntityActivitySystem vas, float durationSeconds)
        {
            this.vas = vas;
            this.durationSeconds = durationSeconds;
        }

        public override bool IsFinished()
        {
            return waitLeftSeconds < 0;
        }

        public override void Start(EntityActivity act)
        {
            waitLeftSeconds = durationSeconds;
        }


        public override void OnTick(float dt) {
            waitLeftSeconds -= dt;
        }

        public override void AddGuiEditFields(ICoreClientAPI capi, GuiComposer singleComposer)
        {
            var b = ElementBounds.Fixed(0, 0, 300, 25);
            singleComposer
                .AddStaticText("Wait IRL seconds", CairoFont.WhiteDetailText(), b)
                .AddNumberInput(b = b.BelowCopy(0, -5), null, CairoFont.WhiteDetailText(), "wait")
            ;

            singleComposer.GetTextInput("wait").SetValue(durationSeconds);
        }

        public override bool StoreGuiEditFields(ICoreClientAPI capi, GuiComposer singleComposer)
        {
            durationSeconds = singleComposer.GetNumberInput("wait").GetValue();
            return true;
        }

        public override IEntityAction Clone()
        {
            return new WaitAction(vas, durationSeconds);
        }

        public override string ToString()
        {
            return "Wait for " + durationSeconds + " IRL seconds";
        }

    }
}
