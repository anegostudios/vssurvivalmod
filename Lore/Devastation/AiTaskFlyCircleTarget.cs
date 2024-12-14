using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class AiTaskFlyCircleTarget : AiTaskFlyCircle
    {
        protected float seekingRangeVer = 25f;
        protected float seekingRangeHor = 25f;

        public AiTaskFlyCircleTarget(EntityAgent entity) : base(entity)
        {
        }

        public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig)
        {
            base.LoadConfig(taskConfig, aiConfig);

            seekingRangeHor = taskConfig["seekingRangeHor"].AsFloat(25);
            seekingRangeVer = taskConfig["seekingRangeVer"].AsFloat(25);
        }

        public override bool ShouldExecute()
        {
            long ellapsedMs = entity.World.ElapsedMilliseconds;
            if (cooldownUntilMs > ellapsedMs)
            {
                return false;
            }

            // Don't try more than once a second
            cooldownUntilMs = entity.World.ElapsedMilliseconds + 1000;

            if (!PreconditionsSatisifed()) return false;

            Vec3d pos = entity.ServerPos.XYZ.Add(0, entity.SelectionBox.Y2 / 2, 0).Ahead(entity.SelectionBox.XSize / 2, 0, entity.ServerPos.Yaw);

            if (entity.World.ElapsedMilliseconds - attackedByEntityMs > 30000)
            {
                attackedByEntity = null;
            }
            if (retaliateAttacks && attackedByEntity != null && attackedByEntity.Alive && attackedByEntity.IsInteractable && IsTargetableEntity(attackedByEntity, 15, true))
            {
                targetEntity = attackedByEntity;
            }
            else
            {
                targetEntity = entity.World.GetNearestEntity(pos, seekingRangeHor, seekingRangeVer, (e) =>
                {
                    return IsTargetableEntity(e, seekingRangeHor) && hasDirectContact(e, seekingRangeHor, seekingRangeVer);
                });
            }

            return targetEntity != null && base.ShouldExecute();
        }

        public override void StartExecute()
        {
            base.StartExecute();
            CenterPos = targetEntity.ServerPos.XYZ;
        }

        public override bool ContinueExecute(float dt)
        {
            CenterPos = targetEntity.ServerPos.XYZ;
            return base.ContinueExecute(dt);
        }
    }
}
