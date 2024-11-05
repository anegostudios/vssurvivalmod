using Vintagestory.API.Common;

namespace Vintagestory.GameContent
{
    public class AiTaskComeToOwner : AiTaskStayCloseToEntity
    {
        long lastExecutedMs;

        public AiTaskComeToOwner(EntityAgent entity) : base(entity)
        {
        }

        public override bool ShouldExecute()
        {
            var tree = entity.WatchedAttributes.GetTreeAttribute("ownedby");
            if (tree == null)
            {
                lastExecutedMs = -99999;
                return false;
            }

            float lastCallDelta = (entity.World.ElapsedMilliseconds - lastExecutedMs) / 1000f;
            if (lastCallDelta < 20) return base.ShouldExecute();
            return false;
        }


        public override void StartExecute()
        {   
            lastExecutedMs = entity.World.ElapsedMilliseconds;

            var tree = entity.WatchedAttributes.GetTreeAttribute("ownedby");
            if (tree == null) return;

            string uid = tree.GetString("uid");
            var plr = entity.World.PlayerByUid(uid);
            targetEntity = plr?.Entity;

            if (targetEntity != null)
            {
                float size = targetEntity.SelectionBox.XSize;
                pathTraverser.NavigateTo_Async(targetEntity.ServerPos.XYZ, moveSpeed, size + 0.2f, OnGoalReached, OnStuck, tryTeleport, 1000, 1);
                targetOffset.Set(entity.World.Rand.NextDouble() * 2 - 1, 0, entity.World.Rand.NextDouble() * 2 - 1);
                stuck = false;
            }

            base.StartExecute();
        }

        public override bool CanContinueExecute()
        {
            return pathTraverser.Ready;
        }

        public override bool ContinueExecute(float dt)
        {
            return targetEntity != null && base.ContinueExecute(dt);
        }
    }
}
