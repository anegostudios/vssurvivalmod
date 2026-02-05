using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent
{
    /// <summary>
    /// Similar to BlockWater but with particles spawning
    /// </summary>
    public class BlockWaterflowing : BlockForFluidsLayer, IBlockFlowing
    {
        public string Flow { get; set; }
        public Vec3i FlowNormali { get; set; }
        public bool IsLava => false;
        public virtual bool HasNormalWaves => true;

        float particleQuantity = 0.2f;
        bool isBoiling;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            Flow = Variant["flow"] is string f ? string.Intern(f) : null;
            FlowNormali = Flow != null ? Cardinal.FromInitial(Flow)?.Normali : null;

            if (api.Side == EnumAppSide.Client)
            {
                ICoreClientAPI capi = api as ICoreClientAPI;
                capi.Settings.Int.AddWatcher("particleLevel", OnClientParticleLevelChanged);
                OnClientParticleLevelChanged(0);
            }

            ParticleProperties[0].SwimOnLiquid = true;

            isBoiling = HasBehavior<BlockBehaviorSteaming>();
        }

        public virtual void OnClientParticleLevelChanged(int newValue)
        {
            particleQuantity = 0.4f * (api as ICoreClientAPI).Settings.Int["particleLevel"] / 100f;
        }

        public override float GetAmbientSoundStrength(IWorldAccessor world, BlockPos pos)
        {
            Block blockAbove = world.BlockAccessor.GetBlockAbove(pos);
            if (blockAbove.Replaceable >= 6000)   // This is a kind of rough "transparent to sound" test
            {
                blockAbove = world.BlockAccessor.GetBlockAbove(pos, 1, BlockLayersAccess.Fluid);
                if (!blockAbove.IsLiquid()) return 1;
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
            bps.basePos.Y = pos.InternalY;
            bps.basePos.Z = pos.Z;

            FastVec3f pushVector = GetPushVector(pos);
            bps.Velocity[0].avg = pushVector.X * 500;
            bps.Velocity[1].avg = pushVector.Y * 1000;
            bps.Velocity[2].avg = pushVector.Z * 500;

            bps.GravityEffect.avg = 0.5f;

            bps.HsvaColor[3].avg = 180 * Math.Min(1, secondsTicking / 7f);

            bps.PosOffset[1].avg = 2/16f;
            bps.PosOffset[1].var = LiquidLevel / 8f * 0.75f;
            manager.Spawn(bps);
        }

        public override float GetTraversalCost(BlockPos pos, EnumAICreatureType creatureType)
        {
            if (creatureType == EnumAICreatureType.SeaCreature && !isBoiling) return 0;
            return isBoiling && creatureType != EnumAICreatureType.HeatProofCreature ? 99999f : 5f;
        }
    }
}
