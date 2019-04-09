using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public enum EnumTalkType
    {
        Meet,
        Idle,
        Hurt,
        Death,
        Purchase,
        Complain,
        Goodbye
    }

    class SlidingPitchSound
    {
        public ILoadedSound sound;
        public float startPitch;
        public float endPitch;
        public long startMs;
    }

    public class TalkUtil
    {
        int lettersLeftToTalk = 0;
        int totalLettersToTalk = 0;

        int currentLetterInWord = 0;
        int totalLettersTalked = 0;

        float chordDelay = 0f;



        Dictionary<EnumTalkType, float> TalkSpeed;



        EnumTalkType talkType;

        ICoreClientAPI capi;
        Entity entity;

        AssetLocation soundName = new AssetLocation("sounds/instrument/saxophone");

        List<SlidingPitchSound> slidingPitchSounds = new List<SlidingPitchSound>();
        List<SlidingPitchSound> stoppedSlidingSounds = new List<SlidingPitchSound>();

        float talkSpeedModifier = 1;
        float pitchModifier = 1;
        float volumneModifier = 1;


        public TalkUtil(ICoreClientAPI capi, Entity atEntity)
        {
            this.capi = capi;
            this.entity = atEntity;
            TalkSpeed = defaultTalkSpeeds();
            
        }

        public void SetModifiers(float talkSpeedModifier = 1, float pitchModifier = 1, float volumneModifier = 1)
        {
            this.talkSpeedModifier = talkSpeedModifier;
            this.pitchModifier = pitchModifier;
            this.volumneModifier = volumneModifier;
            TalkSpeed = defaultTalkSpeeds();
            foreach (var key in TalkSpeed.Keys.ToArray())
            {
                TalkSpeed[key] = Math.Max(0.06f, TalkSpeed[key] * talkSpeedModifier);
            }
        }

        public Random Rand { get { return capi.World.Rand; } }

        public void OnGameTick(float dt)
        {
            for (int i = 0; i < slidingPitchSounds.Count; i++)
            {
                SlidingPitchSound sps = slidingPitchSounds[i];
                if (sps.sound.HasStopped)
                {
                    stoppedSlidingSounds.Add(sps);
                    continue;
                }

                float secondspassed = (capi.World.ElapsedMilliseconds - sps.startMs) / 1000f;
                float progress = GameMath.Min(1, 1 - secondspassed / sps.sound.SoundLength);
                float pitch = sps.endPitch + (sps.startPitch - sps.endPitch) * progress;
                sps.sound.SetPitch(pitch);
            }

            foreach (var val in stoppedSlidingSounds) slidingPitchSounds.Remove(val);


            if (lettersLeftToTalk > 0)
            {
                chordDelay -= dt;

                if (chordDelay < 0)
                {
                    chordDelay = TalkSpeed[talkType];

                    switch (talkType)
                    {
                        case EnumTalkType.Purchase:
                            {
                                float startpitch = 1.5f;
                                float endpitch = totalLettersTalked > 0 ? 0.9f : 1.5f;
                                PlaySound(startpitch, endpitch, 0.5f);
                                chordDelay = 0.3f * talkSpeedModifier;
                            }
                            break;

                        case EnumTalkType.Goodbye:
                            {
                                float pitch = 1.25f - 0.6f * (float)totalLettersTalked / totalLettersToTalk;
                                PlaySound(pitch, pitch * 0.9f, 0.25f);
                                chordDelay = 0.25f * talkSpeedModifier;
                            }
                            break;

                        case EnumTalkType.Death:
                            {
                                float startpitch = 1.25f - 0.6f * (float)totalLettersTalked / totalLettersToTalk;
                                PlaySound(startpitch, startpitch * 0.4f, 0.25f);
                            }
                            break;

                        case EnumTalkType.Meet:
                            {
                                float pitch = 0.75f + 0.5f * (float)Rand.NextDouble() + (float)totalLettersTalked / totalLettersToTalk / 3;
                                PlaySound(pitch, pitch * 1.5f, 0.25f);

                                if (currentLetterInWord > 1 && capi.World.Rand.NextDouble() < 0.35)
                                {
                                    chordDelay = 0.45f * talkSpeedModifier;
                                    currentLetterInWord = 0;
                                }
                                break;
                            }

                        case EnumTalkType.Complain:
                            {
                                float startPitch = 0.75f + 0.5f * (float)Rand.NextDouble();
                                float endPitch = 0.75f + 0.5f * (float)Rand.NextDouble();
                                PlaySound(startPitch, endPitch, 0.25f);

                                if (currentLetterInWord > 1 && capi.World.Rand.NextDouble() < 0.35)
                                {
                                    chordDelay = 0.45f * talkSpeedModifier;
                                    currentLetterInWord = 0;
                                }

                                break;
                            }

                        case EnumTalkType.Idle:
                            {
                                float startPitch = 0.75f + 0.25f * (float)Rand.NextDouble();
                                float endPitch = 0.75f + 0.25f * (float)Rand.NextDouble();
                                PlaySound(startPitch, endPitch, 0.1f);

                                if (currentLetterInWord > 1 && capi.World.Rand.NextDouble() < 0.35)
                                {
                                    chordDelay = 0.55f * talkSpeedModifier;
                                    currentLetterInWord = 0;
                                }


                                break;
                            }

                        case EnumTalkType.Hurt:
                            {
                                float pitch = 0.75f + 0.5f * (float)Rand.NextDouble() + (1 - (float)totalLettersTalked / totalLettersToTalk);

                                PlaySound(pitch, 0.25f + (1 - (float)totalLettersTalked / totalLettersToTalk) / 2);

                                if (currentLetterInWord > 1 && capi.World.Rand.NextDouble() < 0.35)
                                {
                                    chordDelay = 0.25f * talkSpeedModifier;
                                    currentLetterInWord = 0;
                                }

                                break;
                            }
                    }


                    lettersLeftToTalk--;
                    currentLetterInWord++;
                    totalLettersTalked++;
                }

                return;
            }



            if (lettersLeftToTalk == 0 && capi.World.Rand.NextDouble() < 0.0001)
            {
                Talk(EnumTalkType.Idle);
            }
        }


        public void PlaySound(float startpitch, float volume)
        {
            PlaySound(startpitch, startpitch, volume);
        }

        public void PlaySound(float startPitch, float endPitch, float volume)
        {
            startPitch *= pitchModifier;
            endPitch *= pitchModifier;
            volume *= volumneModifier;

            SoundParams param = new SoundParams()
            {
                Location = soundName,
                DisposeOnFinish = true,
                Pitch = startPitch,
                Volume = volume,
                Position = entity.Pos.XYZ.ToVec3f().Add(0, (float)entity.EyeHeight, 0),
                ShouldLoop = false,
                Range = 8,
            };

            ILoadedSound sound = capi.World.LoadSound(param);

            if (startPitch != endPitch)
            {
                slidingPitchSounds.Add(new SlidingPitchSound()
                {
                    startPitch = startPitch,
                    endPitch = endPitch,
                    sound = sound,
                    startMs = capi.World.ElapsedMilliseconds
                });
            }


            sound.Start();
        }


        public void Talk(EnumTalkType talkType)
        {
            IClientWorldAccessor world = capi.World as IClientWorldAccessor;

            this.talkType = talkType;
            totalLettersTalked = 0;
            currentLetterInWord = 0;

            chordDelay = TalkSpeed[talkType];

            if (talkType == EnumTalkType.Meet)
            {
                lettersLeftToTalk = 2 + world.Rand.Next(10);
            }

            if (talkType == EnumTalkType.Hurt)
            {
                lettersLeftToTalk = 3 + world.Rand.Next(6);
            }

            if (talkType == EnumTalkType.Idle)
            {
                lettersLeftToTalk = 3 + world.Rand.Next(12);
            }

            if (talkType == EnumTalkType.Purchase)
            {
                lettersLeftToTalk = 2 + world.Rand.Next(2);
            }

            if (talkType == EnumTalkType.Complain)
            {
                lettersLeftToTalk = 3 + world.Rand.Next(5);
            }

            if (talkType == EnumTalkType.Goodbye)
            {
                lettersLeftToTalk = 2 + world.Rand.Next(2);
            }

            if (talkType == EnumTalkType.Death)
            {
                lettersLeftToTalk = 2 + world.Rand.Next(2);
            }

            totalLettersToTalk = lettersLeftToTalk;
        }


        Dictionary<EnumTalkType, float> defaultTalkSpeeds()
        {
            return new Dictionary<EnumTalkType, float>()
            {
                { EnumTalkType.Meet, 0.13f },
                { EnumTalkType.Death, 0.3f },
                { EnumTalkType.Idle, 0.2f },
                { EnumTalkType.Hurt, 0.07f },
                { EnumTalkType.Goodbye, 0.07f },
                { EnumTalkType.Complain, 0.09f },
                { EnumTalkType.Purchase, 0.15f },
            };
        }
    }
}
