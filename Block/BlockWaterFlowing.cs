using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockWaterflowing : BlockForFluidsLayer
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

        public override float GetAmbientSoundStrength(IWorldAccessor world, BlockPos pos)
        {
            Block block = world.BlockAccessor.GetBlock(pos.X, pos.Y + 1, pos.Z);
            if (block.Replaceable >= 6000)   // This is a kind of rough "transparent to sound" test
            {
                block = world.BlockAccessor.GetBlock(pos.X, pos.Y + 1, pos.Z, BlockLayersAccess.Fluid);
                if (!block.IsLiquid()) return 1;
            }

            return 0;
        }


        public override void OnAsyncClientParticleTick(IAsyncParticleManager manager, BlockPos pos, float windAffectednessAtPos, float secondsTicking)
        {
            foreach (BlockBehavior behavior in BlockBehaviors)
            {
                behavior.OnAsyncClientParticleTick(manager, pos, windAffectednessAtPos, secondsTicking);
            }

            if (api.World.Rand.NextDouble() > particleQuantity)
            {
                return;
            }

            AdvancedParticleProperties bps = ParticleProperties[0];

            bps.basePos.X = pos.X;
            bps.basePos.Y = pos.Y;
            bps.basePos.Z = pos.Z;

            bps.Velocity[0].avg = (float)PushVector.X * 500;
            bps.Velocity[1].avg = (float)PushVector.Y * 1000;
            bps.Velocity[2].avg = (float)PushVector.Z * 500;

            bps.GravityEffect.avg = 0.5f;

            bps.HsvaColor[3].avg = 180 * Math.Min(1, secondsTicking / 7f);
            bps.Quantity.avg = 1;

            bps.PosOffset[1].avg = 2/16f;
            bps.PosOffset[1].var = LiquidLevel / 8f * 0.75f;
            bps.SwimOnLiquid = true;

            bps.Size.avg = 0.05f;
            bps.Size.var = 0f;
            bps.SizeEvolve = EvolvingNatFloat.create(EnumTransformFunction.LINEAR, 0.8f);

            manager.Spawn(bps);
        }

    }
}
