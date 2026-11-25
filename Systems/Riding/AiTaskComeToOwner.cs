using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace Vintagestory.GameContent
{

    public class AiTaskComeToOwner : AiTaskStayCloseToEntity
    {
        long lastExecutedMs;

        public AiTaskComeToOwner(EntityAgent entity, JsonObject taskConfig, JsonObject aiConfig) : base(entity, taskConfig, aiConfig) { }

        protected override void SetDefaultValues()
        {
            base.SetDefaultValues();
            minSeekSeconds = 10f;
        }

        public override bool ShouldExecute()
        {
            var tree = entity.WatchedAttributes.GetTreeAttribute("ownedby");
            if (tree == null)
            {
                lastExecutedMs = -99999;
                return false;
            }
            string uid = tree.GetString("uid");
            var plr = entity.World.PlayerByUid(uid);
            targetEntity = plr?.Entity;

            float lastCallDelta = (entity.World.ElapsedMilliseconds - lastExecutedMs) / 1000f;
            return lastCallDelta < 20 && base.ShouldExecute();
        }


        public override void StartExecute()
        {
            lastExecutedMs = entity.World.ElapsedMilliseconds;

            // Set owner here too, because the flute calls start directly
            var tree = entity.WatchedAttributes.GetTreeAttribute("ownedby");
            if (tree == null)
            {
                return;
            }
            string uid = tree.GetString("uid");
            var plr = entity.World.PlayerByUid(uid);
            targetEntity = plr?.Entity;

            base.StartExecute();
        }
    }
}
