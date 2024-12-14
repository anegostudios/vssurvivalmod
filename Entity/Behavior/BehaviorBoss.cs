using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;

namespace Vintagestory.GameContent
{
    public class EntityBehaviorBoss : EntityBehavior
    {
        AssetLocation musicTrackLocation;

        public bool ShowHealthBar => entity.WatchedAttributes.GetBool("showHealthbar", true);
        ICoreClientAPI capi => entity.World.Api as ICoreClientAPI;


        bool playtrack;
        public bool ShouldPlayTrack
        {
            get
            {
                return playtrack;
            }
            set
            {
                if (playtrack != value)
                {
                    if (value) StartMusic();
                    else StopMusic();
                }

                playtrack = value;
            }
        }

        public float BossHpbarRange { get; set; }

        public EntityBehaviorBoss(Entity entity) : base(entity)
        {
        }

        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            base.Initialize(properties, attributes);

            if (attributes["musicTrack"].Exists)
            {
                musicTrackLocation = AssetLocation.Create(attributes["musicTrack"].AsString(), entity.Code.Domain);
            }

            BossHpbarRange = attributes["bossHpBarRange"].AsFloat(30);
        }
 

        public override string PropertyName()
        {
            return "boss";
        }

        public override void OnGameTick(float deltaTime)
        {
            base.OnGameTick(deltaTime);
        }


        #region Music start/stop

        MusicTrack track;
        long startLoadingMs;
        long handlerId;
        bool wasStopped;

        void StartMusic()
        {
            if (capi == null || musicTrackLocation == null) return;
            if (track?.IsActive == true) return;

            startLoadingMs = capi.World.ElapsedMilliseconds;
            track = capi.StartTrack(musicTrackLocation, 99f, EnumSoundType.AmbientGlitchunaffected, onTrackLoaded);
            track.Priority = 5;
            wasStopped = false;
        }

        void StopMusic()
        {
            if (capi == null) return;

            track?.FadeOut(3);
            track = null;
            capi.Event.UnregisterCallback(handlerId);
            wasStopped = true;
        }

        private void onTrackLoaded(ILoadedSound sound)
        {
            if (track == null)
            {
                sound?.Dispose();
                return;
            }
            if (sound == null) return;

            sound.SetLooping(true);
            track.Sound = sound;

            // Needed so that the music engine does not dispose the sound
            track.ManualDispose = true;

            long longMsPassed = capi.World.ElapsedMilliseconds - startLoadingMs;
            handlerId = capi.Event.RegisterCallback((dt) => {
                if (sound.IsDisposed) { return; }

                if (!wasStopped)
                {
                    sound.Start();
                    sound.FadeIn(2, null);
                }

                track.loading = false;

            }, (int)Math.Max(0, 500 - longMsPassed));
        }


        public override void OnEntityDespawn(EntityDespawnData despawn)
        {
            base.OnEntityDespawn(despawn);

            track?.Stop();
            track?.Sound?.Dispose();
        }

        #endregion

    }
}
