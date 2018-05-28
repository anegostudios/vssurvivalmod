using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class EntityOnGround : EntityLocomotion
    {
        long lastJump;
        double groundDragFactor = 0.3f;

        Vec3d motionDelta = new Vec3d();

        internal override void Initialize(EntityType config)
        {
            JsonObject physics = config?.Attributes?["physics"];
            if (physics != null)
            {
                groundDragFactor = 0.3 * (float)physics["groundDragFactor"].AsDouble(1);
            }
        }

        public override bool Applicable(Entity entity, EntityPos pos, EntityControls controls)
        {
            return entity.OnGround;
        }

        public override void DoApply(float dt, Entity entity, EntityPos pos, EntityControls controls)
        {
            Block belowBlock = entity.World.BlockAccessor.GetBlock((int)pos.X, (int)(pos.Y - 0.05f), (int)pos.Z);           

            if (!entity.Swimming)
            {
                double multiplier = (entity as EntityAgent).GetWalkSpeedMultiplier(groundDragFactor);

                motionDelta.Set(
                    motionDelta.X + (controls.WalkVector.X * multiplier - motionDelta.X) * belowBlock.DragMultiplier, 
                    0,
                    motionDelta.Z + (controls.WalkVector.Z * multiplier - motionDelta.Z) * belowBlock.DragMultiplier
                );

                pos.Motion.Add(motionDelta.X, 0, motionDelta.Z);
            }

            if (controls.Jump && entity.World.ElapsedMilliseconds - lastJump > 500)
            {
                lastJump = entity.World.ElapsedMilliseconds;
                
                pos.Motion.Y = GlobalConstants.BaseJumpForce * dt;
                EntityPlayer entityPlayer = entity as EntityPlayer;
                IPlayer player = entityPlayer != null ? entityPlayer.World.PlayerByUid(entityPlayer.PlayerUID) : null;
                entity.PlayEntitySound("jump", player, false);
            }

            if (!entity.Swimming)
            {
                pos.Motion.X *= (1 - groundDragFactor);
                pos.Motion.Z *= (1 - groundDragFactor);
            }
        }
    }
}
