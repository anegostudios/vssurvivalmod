using Newtonsoft.Json;
using Vintagestory.API.Client;
using Vintagestory.API.Datastructures;

#nullable disable

namespace Vintagestory.GameContent
{
    [JsonObject(MemberSerialization.OptIn)]
    public class StopAnimationAction : EntityActionBase
    {
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


        public override string Type => "stopanimation";

        public override bool IsFinished()
        {
            return true;
        }

        public override void Start(EntityActivity act)
        {
            vas.Entity.AnimManager.StopAnimation(Animation);
        }

        public override void AddGuiEditFields(ICoreClientAPI capi, GuiComposer singleComposer)
        {
            var b = ElementBounds.Fixed(0, 0, 200, 25);
            singleComposer
                .AddStaticText("Animation Code", CairoFont.WhiteDetailText(), b)
                .AddTextInput(b = b.BelowCopy(0, -5), null, CairoFont.WhiteDetailText(), "animation")
            ;

            singleComposer.GetTextInput("animation").SetValue(Animation ?? "");
        }


        public override IEntityAction Clone()
        {
            return new StopAnimationAction(vas, Animation);
        }

        public override bool StoreGuiEditFields(ICoreClientAPI capi, GuiComposer singleComposer)
        {
            Animation = singleComposer.GetTextInput("animation").GetText();
            return true;
        }

        public override string ToString()
        {
            return "Stop animation " + Animation;
        }
    }
}
