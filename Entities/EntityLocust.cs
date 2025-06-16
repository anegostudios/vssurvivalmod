using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent
{
    public class EntityLocust : EntityGlowingAgent
    {
        double mul1, mul2;

        bool lightEmitting;

        public override void Initialize(API.Common.Entities.EntityProperties properties, ICoreAPI api, long InChunkIndex3d)
        {
            base.Initialize(properties, api, InChunkIndex3d);

            lightEmitting = !this.Code.Path.Contains("sawblade");
        }

        public override byte[] LightHsv
        {
            get {
                return lightEmitting ? base.LightHsv : null; 
            }
        }

        /// <summary>
        /// Gets the walk speed multiplier.
        /// </summary>
        /// <param name="groundDragFactor">The amount of drag provided by the current ground. (Default: 0.3)</param>
        public override double GetWalkSpeedMultiplier(double groundDragFactor = 0.3)
        {
            double multiplier = (servercontrols.Sneak ? GlobalConstants.SneakSpeedMultiplier : 1.0) * (servercontrols.Sprint ? GlobalConstants.SprintSpeedMultiplier : 1.0);

            if (FeetInLiquid) multiplier /= 2.5;

            multiplier *= mul1 * mul2;

            // Apply walk speed modifiers.
            multiplier *= GameMath.Clamp(Stats.GetBlended("walkspeed"), 0, 999);

            return multiplier;
        }


        int cnt;

        public override void OnGameTick(float dt)
        {
            base.OnGameTick(dt);

            // Needed for GetWalkSpeedMultiplier(), less read those a little less often for performance
            if (cnt++ > 2)
            {
                cnt = 0;
                var pos = SidedPos;
                Block belowBlock = World.BlockAccessor.GetBlockRaw((int)pos.X, (int)(pos.InternalY - 0.05f), (int)pos.Z);
                Block insideblock = World.BlockAccessor.GetBlockRaw((int)pos.X, (int)(pos.InternalY + 0.01f), (int)pos.Z);

                mul1 = belowBlock.Code == null || belowBlock.Code.Path.Contains("metalspike") ? 1 : belowBlock.WalkSpeedMultiplier;
                mul2 = insideblock.Code == null || insideblock.Code.Path.Contains("metalspike") ? 1 : insideblock.WalkSpeedMultiplier;
            }
        }

        public override bool ReceiveDamage(DamageSource damageSource, float damage)
        {
            if (damageSource.GetCauseEntity() is EntityEidolon) return false;

            return base.ReceiveDamage(damageSource, damage);
        }
    }
}
