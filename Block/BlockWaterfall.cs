using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockWaterfall : Block
    {
        float particleQuantity = 0.2f;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            if (api.Side == EnumAppSide.Client)
            {
                ICoreClientAPI capi = api as ICoreClientAPI;
                capi.Settings.Int.AddWatcher("particleLevel", OnParticelLevelChanged);
                OnParticelLevelChanged(0);
            }
        }

        private void OnParticelLevelChanged(int newValue)
        {
            particleQuantity = 0.2f * (api as ICoreClientAPI).Settings.Int["particleLevel"] / 100f;
        }

        public override bool ShouldPlayAmbientSound(IWorldAccessor world, BlockPos pos)
        {
            for (int i = 0; i < BlockFacing.HORIZONTALS.Length; i++)
            {
                BlockFacing facing = BlockFacing.HORIZONTALS[i];
                Block block = world.BlockAccessor.GetBlock(pos.X + facing.Normali.X, pos.Y, pos.Z + facing.Normali.Z);
                if (block.Replaceable >= 6000 && !block.IsLiquid())
                {
                    return true;
                }
            }

            return false;
        }

        public static int ReplacableThreshold = 5000;
        public override bool ShouldReceiveClientGameTicks(IWorldAccessor world, IPlayer player, BlockPos pos)
        {
            return
                    pos.Y >= 2 &&
                    world.BlockAccessor.GetBlock(pos.X, pos.Y - 1, pos.Z).Replaceable >= ReplacableThreshold/* &&
                    world.BlockAccessor.GetBlock(pos.X, pos.Y - 2, pos.Z).Replaceable >= ReplacableThreshold*/
                ;
        }

        public override void OnClientGameTick(IWorldAccessor world, BlockPos pos, float secondsTicking)
        {
            if (ParticleProperties != null && ParticleProperties.Length > 0)
            {
                for (int i = 0; i < 4; i++)
                {
                    if (world.Rand.NextDouble() > particleQuantity) continue;

                    BlockFacing facing = BlockFacing.HORIZONTALS[i];
                    Block block = world.BlockAccessor.GetBlock(pos.X + facing.Normali.X, pos.Y, pos.Z + facing.Normali.Z);
                    if (block.IsLiquid() || block.SideSolid[facing.GetOpposite().Index]) continue;

                    AdvancedParticleProperties bps = ParticleProperties[i];
                    bps.basePos.X = pos.X + TopMiddlePos.X;
                    bps.basePos.Y = pos.Y;
                    bps.basePos.Z = pos.Z + TopMiddlePos.Z;

                    bps.HsvaColor[3].avg = 180 * Math.Min(1, secondsTicking / 7f);
                    bps.Quantity.avg = 1;
                    bps.Velocity[1].avg = -0.4f;

                    world.SpawnParticles(bps);
                }
            }
        }

    }
}
