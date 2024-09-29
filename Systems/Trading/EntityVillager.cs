﻿using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using System.IO;
using Vintagestory.API.Util;
using Vintagestory.API.Client;
using Newtonsoft.Json.Linq;
using System;
using Vintagestory.API.MathTools;
using System.Linq;

namespace Vintagestory.GameContent
{
    public class EntityVillager : EntityDressedHumanoid, ITalkUtil
    {
        public OrderedDictionary<string, TraderPersonality> Personalities => Properties.Attributes["personalities"].AsObject<OrderedDictionary<string, TraderPersonality>>();

        public EntityVillager()
        {
            AnimManager = new PersonalizedAnimationManager();
        }

        public string Personality
        {
            get { return WatchedAttributes.GetString("personality", "balanced"); }
            set
            {
                var ps = Personalities;
                WatchedAttributes.SetString("personality", value);
                TalkUtil?.SetModifiers(ps[value].ChordDelayMul, ps[value].PitchModifier, ps[value].VolumneModifier);
            }
        }

        public string VoiceSound
        {
            get {
                if (!WatchedAttributes.HasAttribute("voiceSound"))
                {
                    var sounds = Properties.Attributes["voiceSounds"].AsStringArray();
                    var index = Api.World.Rand.Next(sounds.Length);
                    var sound = sounds[index];
                    WatchedAttributes.SetString("voiceSound", sound);
                    TalkUtil.soundName = AssetLocation.Create(sound, Code.Domain);
                    return sound;
                }

                return WatchedAttributes.GetString("voiceSound");
            }
            set {
                WatchedAttributes.SetString("voiceSound", value);
                TalkUtil.soundName = AssetLocation.Create(value, Code.Domain);
            }
        }

        public EntityTalkUtil TalkUtil { get; set; }

        public override void Initialize(EntityProperties properties, ICoreAPI api, long InChunkIndex3d)
        {
            base.Initialize(properties, api, InChunkIndex3d);

            if (!WatchedAttributes.HasAttribute("personality"))
            {
                var p = Personalities;
                int index = api.World.Rand.Next(p.Count);
                this.Personality = p.GetKeyAtIndex(index);
            }

            (AnimManager as PersonalizedAnimationManager).Personality = this.Personality;
            (AnimManager as PersonalizedAnimationManager).All = true;

            if (api.Side == EnumAppSide.Client)
            {
                bool isMultiSoundVoice = true;
                TalkUtil = new EntityTalkUtil(api as ICoreClientAPI, this, isMultiSoundVoice);
                TalkUtil.soundName = AssetLocation.Create(VoiceSound, Code.Domain);
            }

            this.Personality = this.Personality; // to update the talkutil
        }

        MusicTrack track;
        public override void OnReceivedServerPacket(int packetid, byte[] data)
        {
            base.OnReceivedServerPacket(packetid, data);

            if (packetid == (int)EntityServerPacketId.Hurt)
            {
                if (!Alive) return;
                TalkUtil.Talk(EnumTalkType.Hurt);
            }
            if (packetid == (int)EntityServerPacketId.Death)
            {
                TalkUtil.Talk(EnumTalkType.Death);
            }
            if (packetid == (int)EntityServerPacketId.PlayMusic)
            {
                if (track != null) return;

                string trackstring = SerializerUtil.Deserialize<string>(data);
                var capi = Api as ICoreClientAPI;

                startLoadingMs = Api.World.ElapsedMilliseconds;
                wasStopped = false;
                track = capi.StartTrack(AssetLocation.Create(trackstring), 99f, EnumSoundType.MusicGlitchunaffected, onTrackLoaded);
            }
            if (packetid == (int)EntityServerPacketId.StopMusic)
            {
                track?.Stop();
                track = null;
                Api.Event.UnregisterCallback(handlerId);
                wasStopped = true;
                TalkUtil.ShouldDoIdleTalk = true;
            }
        }

        long handlerId;
        long startLoadingMs;
        bool wasStopped = false;

        private void onTrackLoaded(ILoadedSound sound)
        {
            if (track == null)
            {
                sound?.Dispose();
                return;
            }
            if (sound == null) return;

            track.Sound = sound;
            TalkUtil.ShouldDoIdleTalk = false;

            // Needed so that the music engine does not dispose the sound
            Api.Event.EnqueueMainThreadTask(() => track.loading = true, "settrackloading");

            long longMsPassed = Api.World.ElapsedMilliseconds - startLoadingMs;
            handlerId = Api.Event.RegisterCallback((dt) => {
                if (sound.IsDisposed)
                {
                    Api.World.Logger.Notification("Villager track is disposed? o.O");
                }

                if (!wasStopped)
                {
                    sound.Start();
                }

                track.loading = false;

            }, (int)Math.Max(0, 500 - longMsPassed));
        }




        public override void OnGameTick(float dt)
        {
            base.OnGameTick(dt);

            if (World.Side == EnumAppSide.Client)
            {
                TalkUtil.OnGameTick(dt);

                if (track?.Sound != null && track.Sound.IsPlaying)
                {
                    ICoreClientAPI capi = Api as ICoreClientAPI;
                    Vec3d plrpos = capi.World.Player.Entity?.Pos?.XYZ;
                    if (plrpos == null) return;

                    float dist = GameMath.Sqrt(plrpos.SquareDistanceTo(Pos.X + 0.5, Pos.Y + 0.5, Pos.Z + 0.5));

                    // 1/log(x * 0.7)-0.8
                    // https://www.desmos.com/calculator/e9rejsvrcj-

                    float volume = GameMath.Clamp(1 / (float)Math.Log10(Math.Max(1, dist * 0.7)) - 0.8f, 0, 1);

                    track.Sound.SetVolume(volume);
                    track.Sound.SetPitch(GameMath.Clamp(1 - capi.Render.ShaderUniforms.GlitchStrength, 0.1f, 1));
                } else
                {
                    TalkUtil.ShouldDoIdleTalk = true;
                }
            }
        }

        public override void PlayEntitySound(string type, IPlayer dualCallByPlayer = null, bool randomizePitch = true, float range = 24)
        {
            if (type == "idle" && track != null && track.IsActive) return;

            base.PlayEntitySound(type, dualCallByPlayer, randomizePitch, range);
        }

        public override void OnHurt(DamageSource dmgSource, float damage)
        {
            if (World.Side != EnumAppSide.Server) return;

            var acts = GetBehavior<EntityBehaviorActivityDriven>().ActivitySystem.ActiveActivitiesBySlot;
            foreach (var act in acts)
            {
                act.Value.CurrentAction?.OnHurt(dmgSource, damage);
            }

            base.OnHurt(dmgSource, damage);
        }


        public override void OnEntitySpawn()
        {
            base.OnEntitySpawn();

            if (World.Api.Side == EnumAppSide.Server)
            {
                Personality = Personalities.GetKeyAtIndex(World.Rand.Next(Personalities.Count));
                (AnimManager as PersonalizedAnimationManager).Personality = this.Personality;
            }
        }

        public override void FromBytes(BinaryReader reader, bool forClient)
        {
            base.FromBytes(reader, forClient);

            (AnimManager as PersonalizedAnimationManager).Personality = this.Personality;
        }

        public override string GetInfoText()
        {
            var text = base.GetInfoText();

            var capi = Api as ICoreClientAPI;
            if (capi != null && capi.World.Player.WorldData.CurrentGameMode == EnumGameMode.Creative)
            {
                text += "\nPersonality: " + Personality;
                text += "\nVoice: " + VoiceSound;
            }

            return text;
        }
    }

}