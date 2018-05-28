using System;
using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;

namespace Vintagestory.GameContent
{
    class EntityMotionDrag : EntityLocomotion
    {
        double waterDragValue = GlobalConstants.WaterDrag;
        double airDragValue = GlobalConstants.AirDragAlways;

        internal override void Initialize(EntityType config)
        {
            JsonObject physics = config?.Attributes?["physics"];
            if (physics != null)
            {
                waterDragValue = 1 - (1 - GlobalConstants.WaterDrag) * (float)physics["waterDragFactor"].AsDouble(1);

                airDragValue = 1 - (1 - GlobalConstants.AirDragAlways) * (float)physics["airDragFallingFactor"].AsDouble(1);
            }
        }

        public override bool Applicable(Entity entity, EntityPos pos, EntityControls controls)
        {
            return true;
        }

        public override void DoApply(float dt, Entity entity, EntityPos pos, EntityControls controls)
        {
            if (entity.FeetInLiquid || entity.Swimming)
            {
                pos.Motion *= (float)Math.Pow(waterDragValue, dt * 33);
            } else
            {
                pos.Motion *= (float)Math.Pow(airDragValue, dt * 33);
            }

            if (controls.IsFlying)
            {
                pos.Motion *= (float)Math.Pow(GlobalConstants.AirDragFlying, dt * 33);
            }
        }
    }
}
