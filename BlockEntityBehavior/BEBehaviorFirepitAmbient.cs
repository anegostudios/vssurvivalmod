using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BEBehaviorFirepitAmbient : BlockEntityBehavior
    {
        protected ILoadedSound ambientSound;


        static bool MusicActive;
        static double MusicLastPlayedTotalHr = -99;

        int counter;
        MusicTrack track;

        string trackstring = "music/safety-of-a-warm-fire.ogg";

        IFirePit befirepit;
        BlockEntity be;
        bool fadingOut;

        BlockPos Pos => be.Pos;

        public BEBehaviorFirepitAmbient(BlockEntity blockentity) : base(blockentity)
        {
            be = blockentity;
            befirepit = blockentity as IFirePit;
        }



        #region Sound
        public virtual float SoundLevel
        {
            get { return 0.66f; }
        }

        public void ToggleAmbientSounds(bool on)
        {
            if (Api.Side != EnumAppSide.Client) return;

            if (on)
            {
                if (ambientSound == null || !ambientSound.IsPlaying)
                {
                    ambientSound = ((IClientWorldAccessor)Api.World).LoadSound(new SoundParams()
                    {
                        Location = new AssetLocation("sounds/environment/fireplace.ogg"),
                        ShouldLoop = true,
                        Position = be.Pos.ToVec3f().Add(0.5f, 0.25f, 0.5f),
                        DisposeOnFinish = false,
                        Volume = SoundLevel
                    });

                    if (ambientSound != null)
                    {
                        ambientSound.Start();
                        ambientSound.PlaybackPosition = ambientSound.SoundLengthSeconds * (float)Api.World.Rand.NextDouble();
                    }
                }
            }
            else
            {
                ambientSound?.Stop();
                ambientSound?.Dispose();
                ambientSound = null;
            }

        }

        #endregion

        #region Music

        public override void Initialize(ICoreAPI api, JsonObject properties)
        {
            base.Initialize(api, properties);

            if (Blockentity.Api.Side == EnumAppSide.Client && befirepit != null)
            {
                Blockentity.RegisterGameTickListener(OnTick3s, 3000);
            }
        }

        private void OnTick3s(float dt)
        {
            if (MusicActive)
            {
                if (!fadingOut && track != null && track.IsActive && (!befirepit.IsBurning || !HasNearbySittingPlayer || !IsNight))
                {
                    fadingOut = true;
                    track.FadeOut(4, () => StopMusic());
                }
                return;
            }

            double nowHours = Api.World.Calendar.TotalHours;
            if (nowHours - MusicLastPlayedTotalHr < 6) return;
            if (!IsNight) return;
            if (!befirepit.IsBurning) return;

            if (HasNearbySittingPlayer) counter++;
            else counter = 0;

            if (counter > 3)
            {
                MusicActive = true;
                MusicLastPlayedTotalHr = nowHours;
                startLoadingMs = Api.World.ElapsedMilliseconds;
                track = (Api as ICoreClientAPI)?.StartTrack(new AssetLocation(trackstring), 120f, EnumSoundType.Music, onTrackLoaded);
            }
        }

        bool IsNight
        {
            get
            {
                float str = Api.World.Calendar.GetDayLightStrength(Blockentity.Pos.X, Blockentity.Pos.Z);
                return str < 0.4;
            }
        }

        bool HasNearbySittingPlayer
        {
            get
            {
                var eplr = (Api as ICoreClientAPI).World.Player;
                return eplr.Entity.Controls.FloorSitting && eplr.Entity.Pos.DistanceTo(Blockentity.Pos.ToVec3d().Add(0.5, 0.5, 0.5)) < 4;
            }
        }

        long startLoadingMs;
        long handlerId;
        bool wasStopped;

        private void onTrackLoaded(ILoadedSound sound)
        {
            if (track == null)
            {
                sound?.Dispose();
                return;
            }
            if (sound == null) return;

            track.Sound = sound;

            // Needed so that the music engine does not dispose the sound
            Api.Event.EnqueueMainThreadTask(() => track.loading = true, "settrackloading");

            long longMsPassed = Api.World.ElapsedMilliseconds - startLoadingMs;
            handlerId = Blockentity.RegisterDelayedCallback((dt) =>
            {
                if (sound.IsDisposed)
                {
                    Api.World.Logger.Notification("firepit track is diposed? o.O");
                }

                if (!wasStopped)
                {
                    sound.Start();
                }

                track.loading = false;

            }, (int)Math.Max(0, 500 - longMsPassed));
        }



        void StopMusic()
        {
            if (Api?.Side != EnumAppSide.Client) return;

            if (track != null && track.IsActive)
            {
                MusicActive = false;
            }

            track?.Stop();
            track = null;
            Api.Event.UnregisterCallback(handlerId);
            wasStopped = true;
            fadingOut = false;
        }

        public override void OnBlockRemoved()
        {
            StopMusic();
            if (ambientSound != null)
            {
                ambientSound.Stop();
                ambientSound.Dispose();
            }
        }

        public override void OnBlockUnloaded()
        {
            StopMusic();
            ToggleAmbientSounds(false);
        }


        ~BEBehaviorFirepitAmbient()
        {
            if (ambientSound != null)
            {
                ambientSound.Dispose();
            }
        }

        #endregion
    }
}