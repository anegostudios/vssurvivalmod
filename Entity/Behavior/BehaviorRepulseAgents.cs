using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class EntityBehaviorRepulseAgents : EntityBehavior
    {
        Cuboidd entityCuboid = new Cuboidd();
        Vec3d pushVector = new Vec3d();
        double touchDistance;

        public EntityBehaviorRepulseAgents(Entity entity) : base(entity)
        {
            
        }

        public override void Initialize(EntityType entityType, JsonObject attributes)
        {
            base.Initialize(entityType, attributes);

            touchDistance = Math.Sqrt((entity.CollisionBox.X2 - entity.CollisionBox.X1) * (entity.CollisionBox.X2 - entity.CollisionBox.X1));
        }

        public override void OnGameTick(float deltaTime)
        {
            if (entity.State == EnumEntityState.Inactive) return;

            Vec3d pos = entity.LocalPos.XYZ;
            
            pushVector.Set(0, 0, 0);
            Vec3d p = new Vec3d();

            entity.World.GetEntitiesAround(pos, 10, 5, (e) => {
                EntityPos epos = e.LocalPos;
                double distanceSq = epos.SquareDistanceTo(pos);

                if (e != entity && distanceSq < touchDistance * touchDistance && e.HasBehavior("repulseagents") && e.IsInteractable)
                {
                    p.Set(pos.X - epos.X, pos.Y - epos.Y, pos.Z - epos.Z);
                    p.Normalize().Mul(1 - GameMath.Sqrt(distanceSq) / touchDistance);
                    pushVector.Add(p.X, p.Y, p.Z);
                }

                return false;
            });

            
            pushVector.X = GameMath.Clamp(pushVector.X, -3, 3);
            pushVector.Y = GameMath.Clamp(pushVector.Y, -3, 3);
            pushVector.Z = GameMath.Clamp(pushVector.Z, -3, 3);
            entity.LocalPos.Motion.Add(pushVector.X / 15, pushVector.Y / 30, pushVector.Z / 15);

            entity.World.FrameProfiler.Mark("entity-repulse");
        }

        public override string PropertyName()
        {
            return "repulseagents";
        }
    }
}
