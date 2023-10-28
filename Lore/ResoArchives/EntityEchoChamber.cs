using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class EntityLibraryResonator : EntityAgent
    {
        public override void OnGameTick(float dt)
        {
            base.OnGameTick(dt);

            if (Api.Side == EnumAppSide.Client && Api.World.Rand.NextDouble() < 0.05)
            {
                Api.World.SpawnParticles(new SimpleParticleProperties()
                {
                    MinPos = Pos.XYZ.AddCopy(-0.5, 0.1f, -0.5),
                    AddPos = new Vec3d(1, 0.1f, 1),
                    MinQuantity = 3,
                    OpacityEvolve = EvolvingNatFloat.create(EnumTransformFunction.LINEAR, -75),
                    ParticleModel = EnumParticleModel.Quad,
                    GravityEffect = 0,
                    LifeLength = 6,
                    MinSize = 0.125f,
                    MaxSize = 0.125f,
                    MinVelocity = new Vec3f(-0.125f/2f, 0.5f/16f, -0.125f/2f),
                    AddVelocity = new Vec3f(0.25f/2f, 1/16f, 0.25f/2f),
                    Color = ColorUtil.ColorFromRgba(200, 250, 250, 75)
                });
            }
        }
    }

    public class EntityEchoChamber : EntityAgent
    {
        ILoadedSound echoChamberSound1;
        ILoadedSound echoChamberSound2;
        ILoadedSound echoChamberSound3;
        ICoreClientAPI capi;

        public override void Initialize(EntityProperties properties, ICoreAPI api, long InChunkIndex3d)
        {
            base.Initialize(properties, api, InChunkIndex3d);
            if (api is ICoreClientAPI capi)
            {
                this.capi = capi;
                echoChamberSound1 = capi.World.LoadSound(new SoundParams()
                {
                    DisposeOnFinish = false,
                    Location = new AssetLocation("sounds/effect/echochamber.ogg"),
                    Position = this.Pos.XYZ.ToVec3f().Add(0, 00, 0),
                    RelativePosition = false,
                    ShouldLoop = true,
                    SoundType = EnumSoundType.Ambient,
                    Volume = 1,
                    Range = 60
                });
                echoChamberSound1.Start();

                echoChamberSound2 = capi.World.LoadSound(new SoundParams()
                {
                    DisposeOnFinish = false,
                    Location = new AssetLocation("sounds/effect/echochamber.ogg"),
                    Position = this.Pos.XYZ.ToVec3f().Add(0, 30, 0),
                    RelativePosition = false,
                    ShouldLoop = true,
                    SoundType = EnumSoundType.Ambient,
                    Volume = 0,
                    ReferenceDistance = 28,
                    Range = 200
                });
                echoChamberSound2.Start();

                echoChamberSound3 = capi.World.LoadSound(new SoundParams()
                {
                    DisposeOnFinish = false,
                    Location = new AssetLocation("sounds/effect/echochamber2.ogg"),
                    Position = this.Pos.XYZ.ToVec3f().Add(0, 60, 0),
                    RelativePosition = false,
                    ShouldLoop = true,
                    SoundType = EnumSoundType.Ambient,
                    Volume = 0,
                    ReferenceDistance = 28,
                    Range = 200
                });
                echoChamberSound3.Start();

            }
        }

        double accum = 0;

        public override void OnGameTick(float dt)
        {
            accum += dt;
            if (capi != null && accum > 2f)
            {
                accum = 0;
                double dist2player = capi.World.Player.Entity.Pos.HorDistanceTo(this.Pos) - 35;
                var volume = GameMath.Clamp((20 - dist2player) / 20, 0, 1);
                echoChamberSound2.FadeTo(volume, 2, null);
                echoChamberSound3.FadeTo(volume, 2, null);
            }

            base.OnGameTick(dt);
        }

        public override void OnEntityDespawn(EntityDespawnData despawn)
        {
            echoChamberSound1?.Dispose();
            echoChamberSound2?.Dispose();
            echoChamberSound3?.Dispose();
            base.OnEntityDespawn(despawn);
        }
    }
}
