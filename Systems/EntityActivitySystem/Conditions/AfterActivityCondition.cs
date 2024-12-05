using Newtonsoft.Json;
using Vintagestory.API.Client;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{

    [JsonObject(MemberSerialization.OptIn)]
    public class AfterActivityCondition : IActionCondition
    {
        [JsonProperty]
        public bool Invert { get; set; }
        [JsonProperty]
        string activity;

        string[] activities;

        protected EntityActivitySystem vas;
        public AfterActivityCondition() { }
        public AfterActivityCondition(EntityActivitySystem vas, string activity, bool invert=false)
        {
            this.vas = vas;
            this.activity = activity;
            this.Invert = invert;
        }

        public string Type => "afteractivity";

        public virtual bool ConditionSatisfied(Entity e)
        {
            var lastActivity = e.Attributes.GetString("lastActivity");
            if (lastActivity == null) return false;

            if (activities != null)
            {
                return activities.Contains(lastActivity);
            }
            return lastActivity == activity;
        }


        public void LoadState(ITreeAttribute tree) {
            if (activity.Contains(",")) activities = activity.Split(',');
            else activities = null;
        }
        public void StoreState(ITreeAttribute tree) { }


        public void AddGuiEditFields(ICoreClientAPI capi, GuiComposer singleComposer)
        {
            var b = ElementBounds.Fixed(0, 0, 200, 25);
            singleComposer
                .AddStaticText("Activity", CairoFont.WhiteDetailText(), b)
                .AddTextInput(b = b.BelowCopy(0, -5), null, CairoFont.WhiteDetailText(), "activity")
            ;

            singleComposer.GetTextInput("activity").SetValue(activity + "");
        }

        public void StoreGuiEditFields(ICoreClientAPI capi, GuiComposer singleComposer)
        {
            activity = singleComposer.GetTextInput("activity").GetText();

            if (activity.Contains(",")) activities = activity.Split(',');
            else activities = null;
        }
        public IActionCondition Clone()
        {
            return new AfterActivityCondition(vas, activity, Invert);
        }

        public override string ToString()
        {
            return Invert ? "Whenever activity " + activity + " is not running" : "Right after activity " + activity;
        }

        public void OnLoaded(EntityActivitySystem vas)
        {
            this.vas = vas;
            if (activity.Contains(",")) activities = activity.Split(',');
            else activities = null;
        }
    }
}
