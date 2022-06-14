using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BEBehaviorAttractsLightning : BlockEntityBehavior
    {
        private class ConfigurationProperties
        {
            /// <summary>
            /// Modifies the elevation level used when calculating if a lightning strike should be redirected.
            /// </summary>
            public float ArtificialElevation { get; set; } = 1;

            /// <summary>
            /// A multiplier calculated elevation difference.
            /// Will help overcome differences if lower elevation and further increase range if higher elevation.
            /// </summary>
            public float ElevationAttractivenessMultiplier { get; set; } = 1;
        }

        private ConfigurationProperties configProps;

        private WeatherSystemServer weatherSystem => Api.ModLoader.GetModSystem<WeatherSystemServer>();

        public BEBehaviorAttractsLightning(BlockEntity blockentity) : base(blockentity) { }

        public override void Initialize(ICoreAPI api, JsonObject properties)
        {
            base.Initialize(api, properties);

            configProps = properties.AsObject<ConfigurationProperties>();
        }

        public override void OnBlockPlaced()
        {
            base.OnBlockPlaced();

            if (Api.Side == EnumAppSide.Client) return;

            weatherSystem.OnLightningImpactBegin += OnLightningStart;
        }

        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();

            if (Api.Side == EnumAppSide.Client) return;

            weatherSystem.OnLightningImpactBegin -= OnLightningStart;
        }

        private void OnLightningStart(ref Vec3d impactPos, ref EnumHandling handling)
        {
            var world = Blockentity.Api.World;
            var ourPos = Blockentity.Pos;
            
            var ourRainHeight = world.BlockAccessor.GetRainMapHeightAt(ourPos.X, ourPos.Z);

            // Something may be above us blocking line of sight to the sky
            if (ourRainHeight != ourPos.Y) return;

            var impactRainHeight = world.BlockAccessor.GetRainMapHeightAt((int)impactPos.X, (int)impactPos.Z);

            float yDiff = configProps.ArtificialElevation + ourRainHeight - impactRainHeight;

            // We want the modifier to always be beneficial (if greater than 1)
            if(yDiff < 0)
            {
                yDiff /= configProps.ElevationAttractivenessMultiplier;
            } else
            {
                yDiff *= configProps.ElevationAttractivenessMultiplier;
            }

            var posA = new Vec2d(Blockentity.Pos.X, Blockentity.Pos.Z);
            if (posA.DistanceTo(impactPos.X, impactPos.Z) > yDiff) return;

            impactPos = Blockentity.Pos.ToVec3d();
        }
    }
}
