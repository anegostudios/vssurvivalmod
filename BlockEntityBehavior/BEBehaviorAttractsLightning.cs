using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent
{
    /// <summary>
    /// Redirects nearby lightning strikes to this block if it is exposed to the sky and is higher than nearby blocks.
    /// Defined with the "AttractsLightning" code.
    /// </summary>
    /// <example><code lang="json">
    /// "entityBehaviors": [
    ///     {
    ///         "name": "AttractsLightning",
    ///         "properties": {
    ///             "artificialElevation": 5,
    ///             "elevationAttractivenessMultiplier": 2
    ///         }
    ///     }
    /// ],
    /// </code></example>
    [DocumentAsJson]
    public class BEBehaviorAttractsLightning : BlockEntityBehavior
    {
        /// <summary>
        /// Properties of block that redirects lightning strikes. Used by <see cref="BEBehaviorAttractsLightning"/>.
        /// </summary>
        [DocumentAsJson]
        private class ConfigurationProperties
        {
            /// <summary>
            /// Modifies the elevation level used when calculating if a lightning strike should be redirected.
            /// </summary>
            [DocumentAsJson("Optional", "1")]
            public float ArtificialElevation { get; set; } = 1;

            /// <summary>
            /// A multiplier calculated elevation difference.
            /// Will help overcome differences if lower elevation and further increase range if higher elevation.
            /// </summary>
            [DocumentAsJson("Optional", "1")]
            public float ElevationAttractivenessMultiplier { get; set; } = 1;
        }

        /// <summary>
        /// <!--<jsonalias>Properties</jsonalias>-->
        /// The properties for this behavior.
        /// </summary>
        [DocumentAsJson("Required")]
        private ConfigurationProperties configProps;

        private WeatherSystemServer weatherSystem => Api.ModLoader.GetModSystem<WeatherSystemServer>();

        public BEBehaviorAttractsLightning(BlockEntity blockentity) : base(blockentity) { }

        bool registered = false;
        public override void Initialize(ICoreAPI api, JsonObject properties)
        {
            base.Initialize(api, properties);

            configProps = properties.AsObject<ConfigurationProperties>();

            if (Api.Side == EnumAppSide.Server && !registered)
            {
                weatherSystem.OnLightningImpactBegin += OnLightningStart;
                registered = true;
            }
        }

        public override void OnBlockPlaced(ItemStack byItemstack = null)
        {
            base.OnBlockPlaced();

            if (Api.Side == EnumAppSide.Server && !registered)
            {
                weatherSystem.OnLightningImpactBegin += OnLightningStart;
            }
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

            // If yDiff is less than 0, this doesn't change the result
            // since any multiplication or division would still be less than 0
            yDiff *= configProps.ElevationAttractivenessMultiplier;

            yDiff = GameMath.Min(40, yDiff);

            var posA = new Vec2d(Blockentity.Pos.X, Blockentity.Pos.Z);
            if (posA.DistanceTo(impactPos.X, impactPos.Z) > yDiff) return;

            impactPos = Blockentity.Pos.ToVec3d();
        }
    }
}
