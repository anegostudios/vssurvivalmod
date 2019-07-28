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
    public class BlockWaterflowing : Block
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

            ParticleProperties[0].SwimOnLiquid = true;
        }

        private void OnParticelLevelChanged(int newValue)
        {
            particleQuantity = 0.4f * (api as ICoreClientAPI).Settings.Int["particleLevel"] / 100f;
        }

        public override bool ShouldPlayAmbientSound(IWorldAccessor world, BlockPos pos)
        {
            Block block = world.BlockAccessor.GetBlock(pos.X, pos.Y + 1, pos.Z);
            return block.Replaceable >= 6000 && !block.IsLiquid();
        }

        public override bool ShouldReceiveClientGameTicks(IWorldAccessor world, IPlayer player, BlockPos pos)
        {
            return base.ShouldReceiveClientGameTicks(world, player, pos);
        }

        public override void OnClientGameTick(IWorldAccessor world, BlockPos pos, float secondsTicking)
        {
            if (world.Rand.NextDouble() > particleQuantity) return;

            AdvancedParticleProperties bps = ParticleProperties[0];

            bps.basePos.X = pos.X;
            bps.basePos.Y = pos.Y;
            bps.basePos.Z = pos.Z;

            bps.Velocity[0].avg = (float)PushVector.X * 250;
            bps.Velocity[1].avg = (float)PushVector.Y * 250;
            bps.Velocity[2].avg = (float)PushVector.Z * 250;

            bps.GravityEffect.avg = 0.5f;

            bps.HsvaColor[3].avg = 180 * Math.Min(1, secondsTicking / 7f);
            bps.Quantity.avg = 1;

            bps.PosOffset[1].avg = 2/16f;
            bps.PosOffset[1].var = LiquidLevel / 8f * 0.75f;
            bps.SwimOnLiquid = true;

            world.SpawnParticles(bps);
        }

    }
}
