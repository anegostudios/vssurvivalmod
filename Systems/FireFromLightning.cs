using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

#nullable disable

namespace Vintagestory.GameContent
{
    public class ModSystemFireFromLightning : ModSystem
    {
        public override double ExecuteOrder() => 1;
        public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Server;

        ICoreServerAPI api;

        public override void StartServerSide(ICoreServerAPI api)
        {
            this.api = api;
            api.ModLoader.GetModSystem<WeatherSystemServer>().OnLightningImpactEnd += ModSystemFireFromLightning_OnLightningImpactEnd;
        }

        private void ModSystemFireFromLightning_OnLightningImpactEnd(ref Vec3d impactPos, ref EnumHandling handling)
        {
            if (handling != EnumHandling.PassThrough) return;

            if (api.World.Config.GetBool("lightningFires", false))
            {
                var rnd = api.World.Rand;
                var npos = impactPos.AsBlockPos.Add(rnd.Next(2) - 1, rnd.Next(2) - 1, rnd.Next(2) - 1);
                var block = api.World.BlockAccessor.GetBlock(npos);
                var combustibleProps = block.GetCombustibleProperties(api.World, null, npos);
                if (combustibleProps != null)
                {
                    foreach (var facing in BlockFacing.ALLFACES)
                    {
                        BlockPos bpos = npos.AddCopy(facing);
                        block = api.World.BlockAccessor.GetBlock(bpos);

                        if (block.BlockId == 0)
                        {
                            double wetness = api.ModLoader.GetModSystem<WeatherSystemBase>().GetEnvironmentWetness(bpos, 10);
                            
                            if (wetness < 0.01) // If there was medium rain for half a day over the last 10 days, this would result in a wetness of 0.025
                            {
                                api.World.BlockAccessor.SetBlock(api.World.GetBlock(new AssetLocation("fire")).BlockId, bpos);
                                BlockEntity befire = api.World.BlockAccessor.GetBlockEntity(bpos);
                                befire?.GetBehavior<BEBehaviorBurning>()?.OnFirePlaced(facing, null);
                            }
                        }
                    }
                }
            }
        }
    }

}
