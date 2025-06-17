using Newtonsoft.Json;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent
{
    [JsonObject(MemberSerialization.OptIn)]
    public class PlaySoundAction : EntityActionBase
    {
        public override string Type => "playsound";

        [JsonProperty]
        public float delaySeconds = 0;
        [JsonProperty]
        public string soundLocation;
        [JsonProperty]
        public bool randomizePitch = true;
        [JsonProperty]
        public float range = 32;
        [JsonProperty]
        public float volume = 1;

        public PlaySoundAction(EntityActivitySystem vas, float delaySeconds, string soundLocation, bool randomizePitch, float range, float volume)
        {
            this.vas = vas;
            this.delaySeconds = delaySeconds;
            this.soundLocation = soundLocation;
            this.randomizePitch = randomizePitch;
            this.range = range;
            this.volume = volume;
        }

        public PlaySoundAction() { }

        public override void Start(EntityActivity act)
        {
            if (delaySeconds > 0)
            {
                vas.Entity.Api.Event.RegisterCallback(playsound, (int)(delaySeconds * 1000));
            } else
            {
                playsound(0);
            }
            
        }

        private void playsound(float dt)
        {
            var loc = new AssetLocation(soundLocation).WithPathPrefixOnce("sounds/").WithPathAppendixOnce(".ogg");
            vas.Entity.World.PlaySoundAt(loc, vas.Entity, null, randomizePitch, range, volume);
        }

        public override void AddGuiEditFields(ICoreClientAPI capi, GuiComposer singleComposer)
        {
            var b = ElementBounds.Fixed(0, 0, 200, 25);
            singleComposer
                .AddStaticText("Sound Location", CairoFont.WhiteDetailText(), b)
                .AddTextInput(b = b.BelowCopy(0, -5), null, CairoFont.WhiteDetailText(), "soundlocation")

                .AddStaticText("Delay in Seconds", CairoFont.WhiteDetailText(), b = b.BelowCopy(0, 10))
                .AddNumberInput(b = b.BelowCopy(0, -5), null, CairoFont.WhiteDetailText(), "delay")

                .AddStaticText("Range", CairoFont.WhiteDetailText(), b = b.BelowCopy(0, 10))
                .AddNumberInput(b = b.BelowCopy(0, -5), null, CairoFont.WhiteDetailText(), "range")

                .AddStaticText("Volume", CairoFont.WhiteDetailText(), b = b.BelowCopy(0, 10))
                .AddNumberInput(b = b.BelowCopy(0, -5), null, CairoFont.WhiteDetailText(), "volume")

                .AddSwitch(null, b.BelowCopy(0, 10), "randomizepitch", 20, 2)
                .AddStaticText("Randomize pitch", CairoFont.WhiteDetailText(), b = b.BelowCopy(25, 12))
            ;

            singleComposer.GetTextInput("soundlocation").SetValue(soundLocation);
            singleComposer.GetNumberInput("delay").SetValue(delaySeconds);
            singleComposer.GetNumberInput("range").SetValue(range);
            singleComposer.GetNumberInput("volume").SetValue(volume);
            singleComposer.GetSwitch("randomizepitch").On = randomizePitch;
        }


        public override bool StoreGuiEditFields(ICoreClientAPI capi, GuiComposer singleComposer)
        {
            soundLocation = singleComposer.GetTextInput("soundlocation").GetText();
            delaySeconds = singleComposer.GetNumberInput("delay").GetValue();
            range = singleComposer.GetNumberInput("range").GetValue();
            volume = singleComposer.GetNumberInput("volume").GetValue();
            randomizePitch = singleComposer.GetSwitch("randomizepitch").On;
            return true;
        }


        public override IEntityAction Clone()
        {
            return new PlaySoundAction(vas, delaySeconds, soundLocation, randomizePitch, range, volume);
        }

        public override string ToString()
        {
            return string.Format("Play sound {0} after {1} seconds", soundLocation, delaySeconds);
        }
    }
}
