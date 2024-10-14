using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockWaterfall : BlockForFluidsLayer
    {
        float particleQuantity = 0.2f;
        bool isBoiling;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            if (api.Side == EnumAppSide.Client)
            {
                ICoreClientAPI capi = api as ICoreClientAPI;
                capi.Settings.Int.AddWatcher("particleLevel", OnParticleLevelChanged);
                OnParticleLevelChanged(0);
            }

            isBoiling = HasBehavior<BlockBehaviorSteaming>();
        }

        private void OnParticleLevelChanged(int newValue)
        {
            particleQuantity = 0.2f * (api as ICoreClientAPI).Settings.Int["particleLevel"] / 100f;
        }

        public override float GetAmbientSoundStrength(IWorldAccessor world, BlockPos pos)
        {
            for (int i = 0; i < BlockFacing.HORIZONTALS.Length; i++)
            {
                BlockFacing facing = BlockFacing.HORIZONTALS[i];
                Block block = world.BlockAccessor.GetBlock(pos.X + facing.Normali.X, pos.InternalY, pos.Z + facing.Normali.Z);
                if (block.Replaceable >= 6000)   // This is a kind of rough "transparent to sound" test
                {
                    block = world.BlockAccessor.GetBlock(pos.X + facing.Normali.X, pos.InternalY, pos.Z + facing.Normali.Z, BlockLayersAccess.Fluid);
                    if (!block.IsLiquid()) return 1;
                }
            }

            return 0;
        }

        public static int ReplacableThreshold = 5000;
        public override bool ShouldReceiveClientParticleTicks(IWorldAccessor world, IPlayer player, BlockPos pos, out bool isWindAffected)
        {
            isWindAffected = true;

            return
                    pos.Y >= 2 &&
                    world.BlockAccessor.GetBlockBelow(pos).Replaceable >= ReplacableThreshold
                ;
        }

        public override void OnAsyncClientParticleTick(IAsyncParticleManager manager, BlockPos pos, float windAffectednessAtPos, float secondsTicking)
        {
            if (ParticleProperties != null && ParticleProperties.Length > 0)
            {
                for (int i = 0; i < 4; i++)
                {
                    if (api.World.Rand.NextDouble() > particleQuantity) continue;

                    BlockFacing facing = BlockFacing.HORIZONTALS[i];
                    Block block = manager.BlockAccess.GetBlock(pos.X + facing.Normali.X, pos.InternalY, pos.Z + facing.Normali.Z);
                    if (block.SideSolid[facing.Opposite.Index]) continue;
                    block = manager.BlockAccess.GetBlock(pos.X + facing.Normali.X, pos.InternalY, pos.Z + facing.Normali.Z, BlockLayersAccess.Fluid);
                    if (block.BlockId != 0) continue;   // No particles if neighbouring liquid or ice

                    AdvancedParticleProperties bps = ParticleProperties[i];
                    bps.basePos.X = pos.X + TopMiddlePos.X;
                    bps.basePos.Y = pos.Y;
                    bps.basePos.Z = pos.Z + TopMiddlePos.Z;
                    bps.WindAffectednes = windAffectednessAtPos * 0.25f;

                    bps.HsvaColor[3].avg = 180 * Math.Min(1, secondsTicking / 7f);
                    bps.Quantity.avg = 1;
                    bps.Velocity[1].avg = -0.4f;
                    bps.Velocity[0].avg = API.Config.GlobalConstants.CurrentWindSpeedClient.X * windAffectednessAtPos;
                    bps.Velocity[2].avg = API.Config.GlobalConstants.CurrentWindSpeedClient.Z * windAffectednessAtPos;

                    bps.Size.avg = 0.05f;
                    bps.Size.var = 0f;
                    bps.SizeEvolve = EvolvingNatFloat.create(EnumTransformFunction.LINEAR, 0.8f);

                    manager.Spawn(bps);
                }
            }
        }

        public override float GetTraversalCost(BlockPos pos, EnumAICreatureType creatureType)
        {
            if (creatureType == EnumAICreatureType.SeaCreature && !isBoiling) return 0;
            return isBoiling && creatureType != EnumAICreatureType.HeatProofCreature ? 99999f : 5f;
        }

    }
}
