using Newtonsoft.Json;
using Vintagestory.API.Client;
using Vintagestory.API.Datastructures;

namespace Vintagestory.GameContent
{

    [JsonObject(MemberSerialization.OptIn)]
    public class JumpAction : EntityActionBase
    {
        public override string Type => "jump";

        [JsonProperty]
        int Index;
        [JsonProperty]
        float hourOfDay=-1;
        [JsonProperty]
        int MinRepetitions=-1;
        [JsonProperty]
        int MaxRepetitions = -1;

        int repetitionsLeft = -1;


        public JumpAction() { }

        public JumpAction(EntityActivitySystem vas, int index, float hourOfDay, int minrepetitions, int maxrepetitions)
        {
            this.vas = vas;
            this.Index = index;
            this.hourOfDay = hourOfDay;
            this.MinRepetitions = minrepetitions;
            this.MaxRepetitions = maxrepetitions;
        }

        public override void Start(EntityActivity act)
        {
            if (hourOfDay >= 0 && vas.Entity.World.Calendar.HourOfDay > hourOfDay)
            {
                return;
            }

            if (MinRepetitions >= 0 && MaxRepetitions > 0)
            {
                if (repetitionsLeft < 0)
                {
                    repetitionsLeft = vas.Entity.World.Rand.Next(MinRepetitions, MaxRepetitions);
                } else
                {
                    repetitionsLeft--;
                    if (repetitionsLeft <= 0) return;
                }
            }

            act.currentActionIndex = Index;
            act.CurrentAction?.Start(act);
        }


        public override void LoadState(ITreeAttribute tree) {
            repetitionsLeft = tree.GetInt("repetitionsLeft");
        }
        public override void StoreState(ITreeAttribute tree) {
            tree.SetInt("repetitionsLeft", repetitionsLeft);
        }


        public override void AddGuiEditFields(ICoreClientAPI capi, GuiComposer singleComposer)
        {
            var b = ElementBounds.Fixed(0, 0, 300, 25);
            singleComposer
                .AddStaticText("Jump to Action Index", CairoFont.WhiteDetailText(), b)
                .AddNumberInput(b = b.BelowCopy(0, -5), null, CairoFont.WhiteDetailText(), "index")

                .AddStaticText("Until hour of day (0..24, -1 to ignore)", CairoFont.WhiteDetailText(), b = b.BelowCopy(0, 15))
                .AddNumberInput(b = b.BelowCopy(0,  -5), null, CairoFont.WhiteDetailText(), "hourOfDay")

                .AddStaticText("OR amount of min repetitions (-1 to ignore)", CairoFont.WhiteDetailText(), b = b.BelowCopy(0, 15))
                .AddNumberInput(b = b.BelowCopy(0, -5), null, CairoFont.WhiteDetailText(), "minrepetitions")
                .AddStaticText("+ max repetitions (-1 to ignore)", CairoFont.WhiteDetailText(), b = b.BelowCopy(0, 5))
                .AddNumberInput(b = b.BelowCopy(0, -5), null, CairoFont.WhiteDetailText(), "maxrepetitions")
            ;

            singleComposer.GetTextInput("index").SetValue(Index);
            singleComposer.GetNumberInput("hourOfDay").SetValue(hourOfDay);
            singleComposer.GetNumberInput("minrepetitions").SetValue(MinRepetitions);
            singleComposer.GetNumberInput("maxrepetitions").SetValue(MaxRepetitions);
        }

        public override bool StoreGuiEditFields(ICoreClientAPI capi, GuiComposer singleComposer)
        {
            Index = (int)singleComposer.GetNumberInput("index").GetValue();
            hourOfDay = singleComposer.GetNumberInput("hourOfDay").GetValue();
            MinRepetitions = (int)singleComposer.GetNumberInput("minrepetitions").GetValue();
            MaxRepetitions = (int)singleComposer.GetNumberInput("maxrepetitions").GetValue();
            return true;
        }

        public override IEntityAction Clone()
        {
            return new JumpAction(vas, Index, hourOfDay, MinRepetitions, MaxRepetitions);
        }

        public override string ToString()
        {
            if (hourOfDay >= 0)
            {
                return "Jump to action at index " + Index + " until hour of day is " + hourOfDay;
            }
            if (MinRepetitions >= 0 && MaxRepetitions > 0)
            {
                return "Jump to action at index " + Index + ", " + MinRepetitions + " to " + MaxRepetitions + " times.";
            }
            return "Jump to action at index " + Index;
        }

    }
}
