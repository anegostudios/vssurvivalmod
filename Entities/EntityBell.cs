using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent
{
    public class EntityBell : EntityAgent
    {
        ILoadedSound alarmSound;
        ICoreClientAPI capi;

        public override void Initialize(EntityProperties properties, ICoreAPI api, long InChunkIndex3d)
        {
            base.Initialize(properties, api, InChunkIndex3d);

            capi = api as ICoreClientAPI;
        }


        public override void OnGameTick(float dt)
        {
            base.OnGameTick(dt);

            if (alarmSound != null && alarmSound.IsPlaying)
            {
                alarmSound.SetPosition((float)Pos.X, (float)Pos.Y, (float)Pos.Z);

                if (!Alive)
                {
                    alarmSound.FadeOutAndStop(0.25f);
                }
            }
        }

        public override void OnReceivedServerPacket(int packetId, SerializedData packetData)
        {
            if (packetId == 1025)
            {
                var loc = packetData.Deserialize<AssetLocation>();

                if (alarmSound == null)
                {
                    alarmSound = capi.World.LoadSound(new SoundParams()
                    {
                        Location = loc,
                        Position = Pos.XYZ.ToVec3f(),
                        Range = 48,
                        ShouldLoop = true,
                        SoundType = EnumSoundType.Entity,
                        Volume = 0,
                        DisposeOnFinish = false
                    });
                }

                if (!alarmSound.IsPlaying)
                {
                    alarmSound.Start();
                    alarmSound.FadeIn(0.25f, null);
                }
            }

            if (packetId == 1026)
            {
                alarmSound?.FadeOutAndStop(0.25f);
            }

            base.OnReceivedServerPacket(packetId, packetData);
        }

        public override void OnEntityDespawn(EntityDespawnData despawn)
        {
            base.OnEntityDespawn(despawn);

            alarmSound?.FadeOutAndStop(0.25f);
        }
    }
}
