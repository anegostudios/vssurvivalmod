using Newtonsoft.Json;
using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    [JsonObject(MemberSerialization.OptIn)]
    public class AnimationAction : IEntityAction
    {
        protected EntityActivitySystem vas;
        [JsonProperty]
        protected AnimationMetaData meta;
        [JsonProperty]
        protected float DurationHours;
        [JsonProperty]
        protected int OnAnimEnd;

        double untilTotalHours;

        public AnimationAction() { }

        public AnimationAction(EntityActivitySystem vas)
        {
            this.vas = vas;
        }

        public AnimationAction(EntityActivitySystem vas, AnimationMetaData meta, float durationHours, int onAnimEnd)
        {
            this.meta = meta;
            this.DurationHours = durationHours;
            this.OnAnimEnd = onAnimEnd;
        }

        public bool ExecutionHasFailed { get; set; }

        public string Type => "animation";

        public bool IsFinished()
        {
            if (OnAnimEnd == 0) return vas.Entity.World.Calendar.TotalHours > untilTotalHours;

            return vas.Entity.AnimManager.IsAnimationActive(meta.Animation);
        }

        public void Start(EntityActivity act)
        {
            untilTotalHours = vas.Entity.World.Calendar.TotalHours + DurationHours;
            vas.Entity.AnimManager.StartAnimation(meta.Init());
        }

        public void Cancel()
        {
            Finish();
        }
        public void Finish() 
        {
            vas.Entity.AnimManager.StopAnimation(meta.Animation);
        }
        public void LoadState(ITreeAttribute tree) {
            untilTotalHours = tree.GetDouble("untilTotalHours");
        }
        public void StoreState(ITreeAttribute tree) {
            tree.SetDouble("untilTotalHours", untilTotalHours);
        }


        public void AddGuiEditFields(ICoreClientAPI capi, GuiComposer singleComposer)
        {
            var b = ElementBounds.Fixed(0, 0, 200, 25);
            singleComposer
                .AddStaticText("Animation Code", CairoFont.WhiteDetailText(), b)
                .AddTextInput(b = b.BelowCopy(0, -5), null, CairoFont.WhiteDetailText(), "animation")

                .AddStaticText("Animation Speed", CairoFont.WhiteDetailText(), b = b.BelowCopy(0, 10))
                .AddNumberInput(b = b.BelowCopy(0, -5), null, CairoFont.WhiteDetailText(), "speed")

                .AddStaticText("Duration (ingame hours)", CairoFont.WhiteDetailText(), b = b.BelowCopy(0, 10))
                .AddNumberInput(b = b.BelowCopy(0, -5), null, CairoFont.WhiteDetailText(), "durationHours")

                .AddStaticText("On Animation End", CairoFont.WhiteDetailText(), b = b.BelowCopy(0, 10))
                .AddDropDown(new string[] { "repeat", "stop" }, new string[] { "Repeat Animation", "Stop Action" }, OnAnimEnd, null, b = b.BelowCopy(0, -5), CairoFont.WhiteDetailText(), "onAnimEnd")
            ;

            singleComposer.GetTextInput("animation").SetValue(meta?.Animation ?? "");
            singleComposer.GetNumberInput("speed").SetValue(meta?.AnimationSpeed ?? 1);
            singleComposer.GetNumberInput("durationHours").SetValue(DurationHours);
        }


        public IEntityAction Clone()
        {
            return new AnimationAction(vas, meta, DurationHours, OnAnimEnd);
        }

        public bool StoreGuiEditFields(ICoreClientAPI capi, GuiComposer singleComposer)
        {
            meta = new AnimationMetaData()
            {
                Animation = singleComposer.GetTextInput("animation").GetText(),
                AnimationSpeed = singleComposer.GetNumberInput("speed").GetText().ToFloat(1)
            };

            DurationHours = (float)singleComposer.GetNumberInput("durationHours").GetValue();
            OnAnimEnd = singleComposer.GetDropDown("onAnimEnd").SelectedIndices[0];
            return true;
        }

        public override string ToString()
        {
            return "Play animation " + meta?.Animation;
        }

        public void OnTick(float dt)
        {
            if (OnAnimEnd == 1 && !vas.Entity.AnimManager.IsAnimationActive(meta.Animation))
            {
                vas.Entity.AnimManager.StartAnimation(meta.Init());
            }
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
