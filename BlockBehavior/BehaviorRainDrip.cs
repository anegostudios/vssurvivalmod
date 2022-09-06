using System;
using Vintagestory.API;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BehaviorRainDrip : BlockBehavior
    {
        private Random random;

        protected static SimpleParticleProperties accumParticle = null;
        protected static SimpleParticleProperties dripParticle = null;

        protected readonly static Vec3d center = new Vec3d(0.5, 0.125, 0.5);

        static BehaviorRainDrip()
        {
            accumParticle = new SimpleParticleProperties(1, 1, ColorUtil.ColorFromRgba(255, 255, 255, 128), new Vec3d(), new Vec3d(), new Vec3f(), new Vec3f(), 0.6f, 0f, 0.2f, 0.2f, EnumParticleModel.Cube);
            accumParticle.MinPos = new Vec3d(0.0, -0.05, 0.0);
            accumParticle.AddPos = new Vec3d(1.0, 0.04, 1.0);
            accumParticle.SizeEvolve = new EvolvingNatFloat(EnumTransformFunction.LINEAR, 0.5f);
            accumParticle.ClimateColorMap = "climateWaterTint";
            accumParticle.AddQuantity = 1;

            dripParticle = new SimpleParticleProperties(
                1, 1, ColorUtil.ColorFromRgba(255, 255, 255, 180), new Vec3d(), new Vec3d(),
                new Vec3f(0f, 0.08f, 0f), new Vec3f(0f, -0.1f, 0f), 0.6f, 1f, 0.6f, 0.8f, EnumParticleModel.Cube
            );


            dripParticle.SizeEvolve = new EvolvingNatFloat(EnumTransformFunction.LINEAR, -0.2f);
            dripParticle.ClimateColorMap = "climateWaterTint";
            

            //var splashParticle = WeatherSimulationParticles.splashParticles;
            //dripParticle.DeathParticles = new IParticlePropertiesProvider[] { splashParticle };

            accumParticle.DeathParticles = new IParticlePropertiesProvider[] { dripParticle };
        }

        public BehaviorRainDrip(Block block) : base(block)
        {
        }

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            this.random = new Random();
        }

        public override bool ShouldReceiveClientParticleTicks(IWorldAccessor world, IPlayer byPlayer, BlockPos pos, ref EnumHandling handling)
        {
            if (WeatherSystemClient.CurrentEnvironmentWetness < 0.05) return false;

            int rainHeight = world.BlockAccessor.GetRainMapHeightAt(pos);
            if (rainHeight <= pos.Y || (rainHeight <= pos.Y + 1 && world.BlockAccessor.GetBlock(pos.X, pos.Y + 1, pos.Z).HasBehavior<BehaviorRainDrip>()) || (rainHeight <= pos.Y + 2 && world.BlockAccessor.GetBlock(pos.X, pos.Y + 2, pos.Z).HasBehavior<BehaviorRainDrip>()))
            {
                handling = EnumHandling.Handled;
                return true;
            }

            return false;
        }
        

        public override void OnAsyncClientParticleTick(IAsyncParticleManager manager, BlockPos pos, float windAffectednessAtPos, float secondsTicking)
        {
            double rand = random.NextDouble() * 75;

            if (rand < WeatherSystemClient.CurrentEnvironmentWetness)
            {
                accumParticle.MinPos.Set(pos.X, pos.Y, pos.Z);
                manager.Spawn(accumParticle);
            }
        }

    }
}
