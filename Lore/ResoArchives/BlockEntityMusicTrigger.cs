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
    public class BlockEntityMusicTrigger : BlockEntity
    {
        ICoreClientAPI capi;

        public AssetLocation musicTrackLocation;
        public float priority = 4.5f;
        public float fadeInDuration = 0.1f;
        public Cuboidi[] areas;

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            if (api.Side == EnumAppSide.Client)
            {
                RegisterGameTickListener(onTick1s, 1001);
                capi = api as ICoreClientAPI;
            }
        }

        private void onTick1s(float dt)
        {
            if (musicTrackLocation == null || areas == null) return;

            int dx = (int)capi.World.Player.Entity.Pos.X - Pos.X;
            int dy = (int)capi.World.Player.Entity.Pos.Y - Pos.Y;
            int dz = (int)capi.World.Player.Entity.Pos.Z - Pos.Z;
            bool isInRange = false;

            foreach (var area in areas)
            {
                isInRange |= area.ContainsOrTouches(dx, dy, dz);
            }

            if (isInRange) StartMusic();
            else StopMusic();
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);

            var atree = tree["areas"] as TreeAttribute;
            if (atree != null)
            {
                List<Cuboidi> cubs = new List<Cuboidi>();
                foreach (var val in atree.Values)
                {
                    cubs.Add(new Cuboidi((val as IntArrayAttribute).value));
                }
                this.areas = cubs.ToArray();
            }

            if (tree.HasAttribute("musicTrackLocation"))
            {
                musicTrackLocation = new AssetLocation(tree.GetString("musicTrackLocation"));
            }
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            if (areas != null)
            {
                var atree = new TreeAttribute();
                tree["areas"] = atree;

                for (int i = 0; i < areas.Length; i++)
                {
                    atree["" + i] = new IntArrayAttribute(areas[i].Coordinates);
                }
            }

            if (musicTrackLocation != null)
            {
                tree.SetString("musicTrackLocation", musicTrackLocation.ToShortString());
            }
        }



        #region Music start/stop

        MusicTrack track;
        long startLoadingMs;
        long handlerId;
        bool wasStopped;

        void StartMusic()
        {
            if (track != null || capi == null || musicTrackLocation == null)
            {
                if (track != null && !track.IsActive)
                {
                    track.Sound?.Dispose();
                    track = null;
                }
                return;
            }

            startLoadingMs = capi.World.ElapsedMilliseconds;
            track = capi.StartTrack(musicTrackLocation, 99f, EnumSoundType.Music, onTrackLoaded);
            track.Priority = priority;
            wasStopped = false;
        }

        void StopMusic()
        {
            if (capi == null) return;

            track?.FadeOut(4);
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

            track.Sound = sound;

            // Needed so that the music engine does not dispose the sound
            track.ManualDispose = true;

            long longMsPassed = capi.World.ElapsedMilliseconds - startLoadingMs;
            handlerId = capi.Event.RegisterCallback((dt) => {
                if (sound.IsDisposed) { return; }

                if (!wasStopped)
                {
                    sound.Start();
                    sound.FadeIn(fadeInDuration, null);
                }

                track.loading = false;

            }, (int)Math.Max(0, 500 - longMsPassed));
        }


        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();

            track?.Stop();
            track?.Sound?.Dispose();
        }

        #endregion
    }
}
