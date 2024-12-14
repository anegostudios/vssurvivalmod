using Newtonsoft.Json;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    [JsonObject(MemberSerialization.OptIn)]
    public class PlayAnimationAction : EntityActionBase
    {
        [JsonProperty]
        protected AnimationMetaData meta;
        [JsonProperty]
        protected float DurationHours=-1;
        [JsonProperty]
        protected float DurationIrlSeconds = -1;
        [JsonProperty]
        protected int OnAnimEnd;

        double untilTotalHours;
        double secondsLeft;

        public PlayAnimationAction() { }

        public PlayAnimationAction(EntityActivitySystem vas)
        {
            this.vas = vas;
        }

        public PlayAnimationAction(EntityActivitySystem vas, AnimationMetaData meta, float durationHours, float durationIrlSeconds, int onAnimEnd)
        {
            this.meta = meta;
            this.DurationHours = durationHours;
            this.DurationIrlSeconds = durationIrlSeconds;
            this.OnAnimEnd = onAnimEnd;
        }

        public override void Pause()
        {
            vas.Entity.AnimManager.StopAnimation(meta.Animation);
        }

        public override void Resume()
        {
            vas.Entity.AnimManager.StartAnimation(meta.Init());
        }

        public override string Type => "playanimation";

        public override bool IsFinished()
        {
            if (OnAnimEnd == 0 && DurationHours >= 0) return vas.Entity.World.Calendar.TotalHours > untilTotalHours;
            if (OnAnimEnd == 0 && DurationIrlSeconds >= 0) return secondsLeft <= 0;

            return !vas.Entity.AnimManager.IsAnimationActive(meta.Animation);
        }

        public override void Start(EntityActivity act)
        {
            untilTotalHours = vas.Entity.World.Calendar.TotalHours + DurationHours;
            secondsLeft = DurationIrlSeconds;

            if (meta.Animation.Contains(","))
            {
                var cm = meta.Clone();
                var anims = meta.Animation.Split(",");
                cm.Code = cm.Animation = anims[vas.Entity.World.Rand.Next(anims.Length)];
                vas.Entity.AnimManager.StartAnimation(cm.Init());
            } else
            {
                vas.Entity.AnimManager.StartAnimation(meta.Init());
            }
        }

        public override void Cancel()
        {
            Finish();
        }
        public override void Finish() 
        {
            vas.Entity.AnimManager.StopAnimation(meta.Animation);
        }
        public override void LoadState(ITreeAttribute tree) {
            untilTotalHours = tree.GetDouble("untilTotalHours");
            secondsLeft = tree.GetDouble("secondsLeft");
        }
        public override void StoreState(ITreeAttribute tree) {
            tree.SetDouble("untilTotalHours", untilTotalHours);
            tree.SetDouble("secondsLeft", secondsLeft);
        }


        public override void AddGuiEditFields(ICoreClientAPI capi, GuiComposer singleComposer)
        {
            var b = ElementBounds.Fixed(0, 0, 250, 25);
            singleComposer
                .AddStaticText("Animation Code", CairoFont.WhiteDetailText(), b)
                .AddTextInput(b = b.BelowCopy(0, -5), null, CairoFont.WhiteDetailText(), "animation")

                .AddStaticText("Animation Speed", CairoFont.WhiteDetailText(), b = b.BelowCopy(0, 10))
                .AddNumberInput(b = b.BelowCopy(0, -5), null, CairoFont.WhiteDetailText(), "speed")

                .AddStaticText("Duration (ingame hours. -1 to ignore)", CairoFont.WhiteDetailText(), b = b.BelowCopy(0, 10))
                .AddNumberInput(b = b.BelowCopy(0, -5), null, CairoFont.WhiteDetailText(), "durationHours")

                .AddStaticText("OR Duration (irl seconds. -1 to ignore)", CairoFont.WhiteDetailText(), b = b.BelowCopy(0, 10))
                .AddNumberInput(b = b.BelowCopy(0, -5), null, CairoFont.WhiteDetailText(), "durationIrlSeconds")


                .AddStaticText("On Animation End", CairoFont.WhiteDetailText(), b = b.BelowCopy(0, 10))
                .AddDropDown(new string[] { "repeat", "stop" }, new string[] { "Repeat Animation", "Stop Action" }, OnAnimEnd, null, b = b.BelowCopy(0, -5), CairoFont.WhiteDetailText(), "onAnimEnd")


            ;

            singleComposer.GetTextInput("animation").SetValue(meta?.Animation ?? "");
            singleComposer.GetNumberInput("speed").SetValue(meta?.AnimationSpeed ?? 1);
            singleComposer.GetNumberInput("durationHours").SetValue(DurationHours);
            singleComposer.GetNumberInput("durationIrlSeconds").SetValue(DurationIrlSeconds);
        }


        public override IEntityAction Clone()
        {
            return new PlayAnimationAction(vas, meta, DurationHours, DurationIrlSeconds, OnAnimEnd);
        }

        public override bool StoreGuiEditFields(ICoreClientAPI capi, GuiComposer singleComposer)
        {
            meta = new AnimationMetaData()
            {
                Animation = singleComposer.GetTextInput("animation").GetText(),
                AnimationSpeed = singleComposer.GetNumberInput("speed").GetText().ToFloat(1)
            };

            DurationHours = (float)singleComposer.GetNumberInput("durationHours").GetValue();
            DurationIrlSeconds = (float)singleComposer.GetNumberInput("durationIrlSeconds").GetValue();
            OnAnimEnd = singleComposer.GetDropDown("onAnimEnd").SelectedIndices[0];
            return true;
        }

        public override string ToString()
        {
            if (DurationHours >= 0 && OnAnimEnd==0)
            {
                return "Play animation " + meta?.Animation + ". Repeat for " + DurationHours + " ingame hours";
            }
            if (DurationIrlSeconds >= 0 && OnAnimEnd==0)
            {
                return "Play animation " + meta?.Animation + ". Repeat for " + DurationIrlSeconds + " irl seconds";
            }

            return "Play animation " + meta?.Animation + " until finished.";
        }

        public override void OnTick(float dt)
        {
            secondsLeft -= dt;

            if (OnAnimEnd == 0 && (secondsLeft >= 0 || DurationHours>=0) && !vas.Entity.AnimManager.IsAnimationActive(meta.Animation))
            {
                vas.Entity.AnimManager.StartAnimation(meta.Init());
            }
        }
    }
}
