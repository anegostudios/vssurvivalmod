using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;

namespace Vintagestory.GameContent
{
    public class EntityInAir : EntityLocomotion
    {
        public static float airMovingStrength = 0.05f;
        double wallDragFactor = 0.3f;

        internal override void Initialize(EntityType config)
        {
            JsonObject physics = config?.Attributes?["physics"];
            if (physics != null)
            {
                wallDragFactor = 0.3 * (float)physics["wallDragFactor"].AsDouble(1);
            }
        }

        public override bool Applicable(Entity entity, EntityPos pos, EntityControls controls)
        {
            return controls.IsFlying || (!entity.Collided && !entity.FeetInLiquid);
        }

        public override void DoApply(float dt, Entity entity, EntityPos pos, EntityControls controls)
        {
            if (controls.IsFlying)
            {
                pos.Motion.Add(controls.FlyVector.X, (controls.Up || controls.Down) ? 0 : controls.FlyVector.Y, controls.FlyVector.Z);

                float moveSpeed = dt * GlobalConstants.BaseMoveSpeed * controls.MovespeedMultiplier / 2;

                pos.Motion.Add(0, (controls.Up ? moveSpeed : 0) + (controls.Down ? -moveSpeed : 0), 0);

            } else
            {
                if (controls.IsClimbing)
                {
                    pos.Motion.Add(controls.WalkVector);
                    pos.Motion.X *= (1 - wallDragFactor);
                    pos.Motion.Y *= (1 - wallDragFactor);
                    pos.Motion.Z *= (1 - wallDragFactor);

                } else
                {
                    pos.Motion.Add(controls.WalkVector.X * airMovingStrength, controls.WalkVector.Y * airMovingStrength, controls.WalkVector.Z * airMovingStrength);
                }
                
            }
        }
    }
}
