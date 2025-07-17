using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;

#nullable disable

namespace Vintagestory.GameContent
{
    public class AiTaskFlyCircleIfEntity : AiTaskFlyCircle
    {
        protected float seekingRangeVer = 25f;
        protected float seekingRangeHor = 25f;

        public AiTaskFlyCircleIfEntity(EntityAgent entity, JsonObject taskConfig, JsonObject aiConfig) : base(entity, taskConfig, aiConfig)
        {
            seekingRangeHor = taskConfig["seekingRangeHor"].AsFloat(25);
            seekingRangeVer = taskConfig["seekingRangeVer"].AsFloat(25);
        }

        public override bool ShouldExecute()
        {
            CenterPos = SpawnPos;

            if (CenterPos == null) return false;
            long ellapsedMs = entity.World.ElapsedMilliseconds;
            if (cooldownUntilMs > ellapsedMs)
            {
                return false;
            }

            // Don't try more than once a second
            cooldownUntilMs = entity.World.ElapsedMilliseconds + 1000;

            if (!PreconditionsSatisifed()) return false;
            targetEntity = getEntity();

            return targetEntity != null && base.ShouldExecute();
        }

        public override bool ContinueExecute(float dt)
        {
            return base.ContinueExecute(dt) && isNear();
        }

        private bool isNear()
        {
            return targetEntity.ServerPos.SquareHorDistanceTo(CenterPos) <= seekingRangeHor * seekingRangeHor && targetEntity.ServerPos.Dimension == entity.ServerPos.Dimension;
        }

        public Entity getEntity()
        {
            if(CenterPos == null) return null;
            return entity.World.GetNearestEntity(CenterPos, seekingRangeHor, seekingRangeVer, (e) =>
            {
                var eplr = (e as EntityPlayer);
                return eplr != null && targetablePlayerMode(eplr.Player);
            });
        }
    }
}
