using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace Vintagestory.GameContent
{
    public class BEBehaviorFirepitAmbient : BlockEntityBehavior
    {
        static bool MusicActive;
        static double MusicLastPlayedTotalHr = -99;

        int counter;
        MusicTrack track;

        string trackstring = "music/safety-of-a-warm-fire.ogg";

        BlockEntityFirepit befirepit;
        bool fadingOut;

        public BEBehaviorFirepitAmbient(BlockEntity blockentity) : base(blockentity)
        {
            befirepit = blockentity as BlockEntityFirepit;
        }

        public override void Initialize(ICoreAPI api, JsonObject properties)
        {
            base.Initialize(api, properties);

            if (Blockentity.Api.Side == EnumAppSide.Client)
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
                track = (Api as ICoreClientAPI)?.StartTrack(new AssetLocation(trackstring), 120f, EnumSoundType.AmbientGlitchunaffected, onTrackLoaded);
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
        }

        public override void OnBlockUnloaded()
        {
            StopMusic();
        }
    }
}