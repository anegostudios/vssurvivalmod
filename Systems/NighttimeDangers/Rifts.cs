using ProtoBuf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Vintagestory.GameContent
{
    public class ModSystemRifts : ModSystem
    {
        ICoreAPI api;
        ICoreServerAPI sapi;
        ICoreClientAPI capi;
        RiftRenderer renderer;

        public List<Rift> rifts = new List<Rift>();
        public ILoadedSound[] riftSounds = new ILoadedSound[4];
        public Rift[] nearestRifts;

        public override bool ShouldLoad(EnumAppSide forSide)
        {
            return true;
        }

        public override void Start(ICoreAPI api)
        {
            base.Start(api);
            this.api = api;
        }


        public override void StartClientSide(ICoreClientAPI api)
        {
            base.StartClientSide(api);

            capi = api;

            renderer = new RiftRenderer(api, rifts);

            api.RegisterCommand("rifttest", "", "", onCmdRiftTest);
            api.Event.BlockTexturesLoaded += Event_BlockTexturesLoaded;
            api.Event.LeaveWorld += Event_LeaveWorld;
            api.Event.RegisterGameTickListener(onClientTick, 100);
        }

        private void onClientTick(float dt)
        {
            Vec3d plrPos = capi.World.Player.Entity.Pos.XYZ;

            nearestRifts = rifts.OrderBy(rift => rift.Position.SquareDistanceTo(plrPos)).ToArray();

            for (int i = 0; i < Math.Min(4, nearestRifts.Length); i++)
            {
                Rift rift = nearestRifts[i];
                ILoadedSound sound = riftSounds[i];
                if (!sound.IsPlaying)
                {
                    sound.Start();
                    sound.PlaybackPosition = sound.SoundLengthSeconds * (float)capi.World.Rand.NextDouble();
                }

                sound.SetVolume(GameMath.Clamp(rift.Size / 3f, 0.2f, 1f));
                sound.SetPosition((float)rift.Position.X + rift.JitterOffset.X, (float)rift.Position.Y + rift.JitterOffset.Y, (float)rift.Position.Z + rift.JitterOffset.Z);
            }

            for (int i = nearestRifts.Length; i < 4; i++)
            {
                if (riftSounds[i].IsPlaying)
                {
                    riftSounds[i].Stop();
                }
            }
        }

        private void Event_LeaveWorld()
        {
            for (int i = 0; i < 4; i++)
            {
                riftSounds[i]?.Stop();
                riftSounds[i]?.Dispose();
            }
        }

        private void Event_BlockTexturesLoaded()
        {
            for (int i = 0; i < 4; i++)
            {
                riftSounds[i] = capi.World.LoadSound(new SoundParams()
                {
                    Location = new AssetLocation("sounds/effect/rift.ogg"),
                    ShouldLoop = true,
                    Position = null,
                    DisposeOnFinish = false,
                    Volume = 1,
                    SoundType = EnumSoundType.AmbientGlitchunaffected
                });
            }
        }

        private void onCmdRiftTest(int groupId, CmdArgs args)
        {
            Vec3d pos = capi.World.Player.Entity.Pos.XYZ;

            string cmd = args.PopWord();

            if (cmd == "clear")
            {
                rifts.Clear();
            }

            if (cmd == "spawn")
            {
                for (int i = 0; i < 20; i++)
                {
                    float dx = (float)api.World.Rand.NextDouble() * 300 - 150;
                    float dz = (float)api.World.Rand.NextDouble() * 300 - 150;
                    Vec3d riftPos = pos.AddCopy(dx, 0, dz);

                    int terrainY = api.World.BlockAccessor.GetTerrainMapheightAt(new BlockPos((int)riftPos.X, 0, (int)riftPos.Z));

                    riftPos.Y = terrainY + (api.World.Rand.NextDouble() < 0.5 ? (float)api.World.Rand.NextDouble() * 3 : (float)api.World.Rand.NextDouble() * (float)api.World.Rand.NextDouble() * 50);


                    rifts.Add(new Rift() { Position = riftPos, Size = 0.5f + (float)api.World.Rand.NextDouble() * 4 });
                }
            }
        }
    }
}
