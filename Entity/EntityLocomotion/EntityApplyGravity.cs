using System;
using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;

namespace Vintagestory.GameContent
{
    public class EntityApplyGravity : EntityLocomotion
    {
        double gravityPerSecond = GlobalConstants.GravityPerSecond;

        internal override void Initialize(EntityType config)
        {
            JsonObject physics = config?.Attributes?["physics"];
            if (physics != null)
            {
                gravityPerSecond = GlobalConstants.GravityPerSecond * (float)physics["gravityFactor"].AsDouble(1f);
            }
        }

        public override bool Applicable(Entity entity, EntityPos pos, EntityControls controls)
        {
            return 
                (!controls.IsFlying && entity.Type.Habitat != EnumHabitat.Air)
                && (entity.Type.Habitat != EnumHabitat.Sea || !entity.Swimming)
                && !controls.IsClimbing
            ;
        }

        public override void DoApply(float dt, Entity entity, EntityPos pos, EntityControls controls)
        {
            if (entity.Swimming && controls.TriesToMove) return;
            if (!entity.ApplyGravity) return;


            if (pos.Y > -100)
            {
                pos.Motion.Y -= (gravityPerSecond * dt + Math.Max(0, -0.015f * pos.Motion.Y)) * (entity.FeetInLiquid ? 0.33f : 1f);
            }
            
        }
    }
}
