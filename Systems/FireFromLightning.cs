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
        int fireBlockId;

        public override void StartServerSide(ICoreServerAPI api)
        {
            this.api = api;
            fireBlockId = api.World.GetBlock("fire")!.BlockId;
            api.ModLoader.GetModSystem<WeatherSystemServer>().OnLightningImpactEnd += ModSystemFireFromLightning_OnLightningImpactEnd;
        }

        private void ModSystemFireFromLightning_OnLightningImpactEnd(ref Vec3d impactPos, ref EnumHandling handling)
        {
            if (handling != EnumHandling.PassThrough) return;

            if (api.World.Config.GetBool("lightningFires", false))
            {
                var rnd = api.World.Rand;
                var blockAccess = api.World.BlockAccessor;
                var npos = impactPos.AsBlockPos.Add(rnd.Next(3) - 1, rnd.Next(3) - 1, rnd.Next(3) - 1);
                if (blockAccess.GetBlock(npos).GetCombustibleProperties(api.World, null, npos) == null) return;

                foreach (var facing in BlockFacing.ALLFACES)
                {
                    facing.IterateThruFacingOffsets(npos); // This must be the first command in the loop, to ensure all facings will be properly looped through regardless of any 'continue;' statements
                    if (blockAccess.GetBlock(npos).BlockId != 0) continue;

                    // If there was medium rain for half a day over the last 10 days, this would result in a wetness of 0.025
                    if (api.ModLoader.GetModSystem<WeatherSystemBase>().GetEnvironmentWetness(npos, 10) >= 0.01) continue;

                    blockAccess.SetBlock(fireBlockId, npos);
                    blockAccess.GetBlockEntity(npos)?.GetBehavior<BEBehaviorBurning>()?.OnFirePlaced(facing, null);
                }
            }
        }
    }

}
