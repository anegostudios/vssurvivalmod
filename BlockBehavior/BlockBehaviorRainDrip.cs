using System;
using Vintagestory.API;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent
{
    /// <summary>
    /// Makes a block emit steam particles, and also marks some water blocks as 'boiling'.
    /// Uses the code "Steaming". This behavior has no properties.
    /// </summary>
    /// <example>
    /// <code lang="json">
    ///"behaviors": [
	///	{ "name": "Steaming" },
	///]
    /// </code>
    /// </example>
    [DocumentAsJson]
    public class BlockBehaviorSteaming : BlockBehavior
    {
        SimpleParticleProperties steamParticles;
        static WaterSplashParticles SplashParticleProps = new WaterSplashParticles();
        ICoreAPI api;

        public BlockBehaviorSteaming(Block block) : base(block)
        {
        }

        public override void OnLoaded(ICoreAPI api)
        {
            this.api = api;

            steamParticles = new SimpleParticleProperties(
               0.05f / 2f, 0.1f / 2f,
               ColorUtil.ColorFromRgba(240, 200, 200, 50),
               new Vec3d(), new Vec3d(1, 1, 1),
               new Vec3f(-0.7f, 0.2f, -0.7f), new Vec3f(0.7f, 0.5f, 0.7f),
               3, 0, 1, 2,
               EnumParticleModel.Quad
            );
            steamParticles.WindAffected = true;
            steamParticles.WindAffectednes = 1f;
            steamParticles.OpacityEvolve = EvolvingNatFloat.create(EnumTransformFunction.QUADRATIC, -20f);

            SplashParticleProps.QuantityMul = 0.05f / 30f;

        }

        public override bool ShouldReceiveClientParticleTicks(IWorldAccessor world, IPlayer byPlayer, BlockPos pos, ref EnumHandling handling)
        {
            handling = EnumHandling.PreventDefault;
            return true;
        }

        public override void OnAsyncClientParticleTick(IAsyncParticleManager manager, BlockPos pos, float windAffectednessAtPos, float secondsTicking)
        {
            var rand = api.World.Rand;
            steamParticles.Color= ColorUtil.HsvToRgba(110, 40 + rand.Next(50), 200 + rand.Next(30), 50 + rand.Next(40));
            steamParticles.MinPos.Set(pos.X+0.5f, pos.Y+4/8f, pos.Z+0.5f);
            manager.Spawn(steamParticles);

            SplashParticleProps.BasePos.Set(pos.X + 0.25f, pos.Y, pos.Z + 0.25f);
            manager.Spawn(SplashParticleProps);
        }

        
    }


    /// <summary>
    /// Makes a block emit rain particles during or after rainy weather.
    /// Uses the "RainDrip" code. This behavior has no properties.
    /// </summary>
    /// <example><code lang="json">
    ///"behaviors": [
	///	{ "name": "RainDrip" },
	///]
    /// </code></example>
    [DocumentAsJson]
    public class BlockBehaviorRainDrip : BlockBehavior
    {
        private Random random;

        protected static SimpleParticleProperties accumParticle = null;
        protected static SimpleParticleProperties dripParticle = null;

        protected readonly static Vec3d center = new Vec3d(0.5, 0.125, 0.5);

        WeatherSystemClient wsys;

        static BlockBehaviorRainDrip()
        {
            accumParticle = new SimpleParticleProperties(1, 1, ColorUtil.ColorFromRgba(255, 255, 255, 128), new Vec3d(), new Vec3d(), new Vec3f(), new Vec3f(), 0.6f, 0f, 0.2f, 0.2f, EnumParticleModel.Cube);
            accumParticle.MinPos = new Vec3d(0.0, -0.05, 0.0);
            accumParticle.AddPos = new Vec3d(1.0, 0.04, 1.0);
            accumParticle.SizeEvolve = new EvolvingNatFloat(EnumTransformFunction.LINEAR, 0.5f);
            accumParticle.ClimateColorMap = "climateWaterTint";
            accumParticle.AddQuantity = 1;
            accumParticle.WindAffected = true;

            dripParticle = new SimpleParticleProperties(
                1, 1, ColorUtil.ColorFromRgba(255, 255, 255, 180), new Vec3d(), new Vec3d(),
                new Vec3f(0f, 0.08f, 0f), new Vec3f(0f, -0.1f, 0f), 0.6f, 1f, 0.6f, 0.8f, EnumParticleModel.Cube
            );


            dripParticle.SizeEvolve = new EvolvingNatFloat(EnumTransformFunction.LINEAR, -0.2f);
            dripParticle.ClimateColorMap = "climateWaterTint";
            
            accumParticle.DeathParticles = new IParticlePropertiesProvider[] { dripParticle };
        }

        public BlockBehaviorRainDrip(Block block) : base(block)
        {
        }

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            this.random = new Random();

            wsys = api.ModLoader.GetModSystem<WeatherSystemClient>();
        }

        public override bool ShouldReceiveClientParticleTicks(IWorldAccessor world, IPlayer byPlayer, BlockPos pos, ref EnumHandling handling)
        {
            if (WeatherSystemClient.CurrentEnvironmentWetness4h < 0.05 || wsys.clientClimateCond.Temperature < 2) return false;

            int rainHeight = world.BlockAccessor.GetRainMapHeightAt(pos);
            if (rainHeight <= pos.Y || (rainHeight <= pos.Y + 1 && world.BlockAccessor.GetBlockAbove(pos, 1, BlockLayersAccess.Solid).HasBehavior<BlockBehaviorRainDrip>()) || (rainHeight <= pos.Y + 2 && world.BlockAccessor.GetBlockAbove(pos, 2, BlockLayersAccess.Solid).HasBehavior<BlockBehaviorRainDrip>()))
            {
                handling = EnumHandling.Handled;
                return true;
            }

            return false;
        }
        

        public override void OnAsyncClientParticleTick(IAsyncParticleManager manager, BlockPos pos, float windAffectednessAtPos, float secondsTicking)
        {
            double rand = random.NextDouble() * 75;

            if (rand < WeatherSystemClient.CurrentEnvironmentWetness4h)
            {
                accumParticle.WindAffectednes = windAffectednessAtPos / 2f;
                accumParticle.MinPos.Set(pos.X, pos.InternalY, pos.Z);
                manager.Spawn(accumParticle);
            }
        }

    }
}
