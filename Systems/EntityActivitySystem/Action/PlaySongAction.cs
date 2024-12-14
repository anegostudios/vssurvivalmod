using Newtonsoft.Json;
using ProtoBuf;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using static Vintagestory.API.Common.EntityAgent;

namespace Vintagestory.GameContent
{
    [ProtoContract]
    public class SongPacket
    {
        [ProtoMember(1)]
        public string SoundLocation;
        [ProtoMember(2)]
        public float SecondsPassed;
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class PlaySongAction : EntityActionBase
    {
        public override string Type => "playsong";

        [JsonProperty]
        public float durationSeconds = 0;
        [JsonProperty]
        public string soundLocation;
        [JsonProperty]
        public float pitch = 1f;
        [JsonProperty]
        public float range = 32;
        [JsonProperty]
        public float volume = 1;
        [JsonProperty]
        public AnimationMetaData animMeta;

        float secondsPassed=0;
        bool stop = false;

        public PlaySongAction(EntityActivitySystem vas, float durationSeconds, string soundLocation, float pitch, float range, float volume, AnimationMetaData animMeta)
        {
            this.vas = vas;
            this.durationSeconds = durationSeconds;
            this.soundLocation = soundLocation;
            this.pitch = pitch;
            this.range = range;
            this.volume = volume;
            this.animMeta = animMeta;
        }

        public PlaySongAction ()
        {
        }

        float accum;
        public override void OnTick(float dt)
        {
            accum += dt;
            if (accum > 3)
            {
                sendSongPacket();
            }

            secondsPassed += dt;
        }

        public override bool IsFinished()
        {
            return stop || secondsPassed > durationSeconds;
        }

        public override void Start(EntityActivity act)
        {
            secondsPassed = 0;
            stop = false;
            vas.Entity.AnimManager.StartAnimation(animMeta.Init());
            sendSongPacket();
        }

        private void sendSongPacket()
        {
            var sapi = vas.Entity.Api as ICoreServerAPI;
            sapi.Network.BroadcastEntityPacket(vas.Entity.EntityId, (int)EntityServerPacketId.PlayMusic, SerializerUtil.Serialize(new SongPacket()
            {
                SecondsPassed = secondsPassed,
                SoundLocation = soundLocation
            }));
        }

        public override void OnHurt(DamageSource dmgSource, float damage)
        {
            stop = true;
            Cancel();
        }
        
        public override void Cancel()
        {
            var sapi = vas.Entity.Api as ICoreServerAPI;
            sapi.Network.BroadcastEntityPacket(vas.Entity.EntityId, (int)EntityServerPacketId.StopMusic);
            Finish();
        }
        public override void Finish() {
            vas.Entity.AnimManager.StopAnimation(animMeta.Code);
            var sapi = vas.Entity.Api as ICoreServerAPI;
            sapi.Network.BroadcastEntityPacket(vas.Entity.EntityId, (int)EntityServerPacketId.StopMusic);
        }

        public override void AddGuiEditFields(ICoreClientAPI capi, GuiComposer singleComposer)
        {
            var b = ElementBounds.Fixed(0, 0, 200, 25);
            singleComposer
                .AddStaticText("Sound Location", CairoFont.WhiteDetailText(), b)
                .AddTextInput(b = b.BelowCopy(0, -5), null, CairoFont.WhiteDetailText(), "soundlocation")

                .AddStaticText("Duration in Seconds", CairoFont.WhiteDetailText(), b = b.BelowCopy(0, 10))
                .AddNumberInput(b = b.BelowCopy(0, -5), null, CairoFont.WhiteDetailText(), "durationSec")

                .AddStaticText("Range", CairoFont.WhiteDetailText(), b = b.BelowCopy(0, 10))
                .AddNumberInput(b = b.BelowCopy(0, -5), null, CairoFont.WhiteDetailText(), "range")

                .AddStaticText("Animation Code", CairoFont.WhiteDetailText(), b = b.BelowCopy(0, 10))
                .AddTextInput(b = b.BelowCopy(0, -5), null, CairoFont.WhiteDetailText(), "animation")

                .AddStaticText("Animation Speed", CairoFont.WhiteDetailText(), b = b.BelowCopy(0, 10))
                .AddNumberInput(b = b.BelowCopy(0, -5), null, CairoFont.WhiteDetailText(), "speed")

            ;

            singleComposer.GetTextInput("soundlocation").SetValue(soundLocation);
            singleComposer.GetNumberInput("durationSec").SetValue(durationSeconds);
            singleComposer.GetNumberInput("range").SetValue(range);

            singleComposer.GetTextInput("animation").SetValue(animMeta?.Animation ?? "");
            singleComposer.GetNumberInput("speed").SetValue(animMeta?.AnimationSpeed ?? 1);
        }


        public override bool StoreGuiEditFields(ICoreClientAPI capi, GuiComposer singleComposer)
        {
            animMeta = new AnimationMetaData()
            {
                Animation = singleComposer.GetTextInput("animation").GetText(),
                AnimationSpeed = singleComposer.GetNumberInput("speed").GetText().ToFloat(1)
            };

            soundLocation = singleComposer.GetTextInput("soundlocation").GetText();
            durationSeconds = singleComposer.GetNumberInput("durationSec").GetValue();
            range = singleComposer.GetNumberInput("range").GetValue();
            return true;
        }



        public override IEntityAction Clone()
        {
            return new PlaySongAction(vas, durationSeconds, soundLocation, pitch, range, volume, animMeta);
        }

        public override string ToString()
        {
            return string.Format("Play song {0} for {1} seconds, with anim {2}", soundLocation, durationSeconds, animMeta?.Code);
        }

    }
}
