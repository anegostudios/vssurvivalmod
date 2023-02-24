using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Vintagestory.GameContent
{
    public class AiTaskButterflyFeedOnFlowers : AiTaskBase
    {
        public Vec3d MainTarget;

        // 0=goto, 1=flower reached, 2=feeding
        int taskState;

        float moveSpeed = 0.03f;
        float targetDistance = 0.07f;

        double searchFrequency = 0.05f;

        bool awaitReached = true;       
        BlockPos tmpPos = new BlockPos();

        double feedTime;

        public AiTaskButterflyFeedOnFlowers(EntityAgent entity) : base(entity)
        {
            (entity.Api as ICoreServerAPI).Event.DidBreakBlock += Event_DidBreakBlock;
        }

        public override void OnEntityDespawn(EntityDespawnData reason)
        {
            (entity.Api as ICoreServerAPI).Event.DidBreakBlock -= Event_DidBreakBlock;
        }

        private void Event_DidBreakBlock(IServerPlayer byPlayer, int oldblockId, BlockSelection blockSel)
        {
            if (tmpPos != null && blockSel.Position.Equals(tmpPos))
            {
                taskState = 4;
            }
        }

        public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig)
        {
            base.LoadConfig(taskConfig, aiConfig);


            if (taskConfig["targetDistance"] != null)
            {
                targetDistance = taskConfig["targetDistance"].AsFloat(0.07f);
            }

            if (taskConfig["movespeed"] != null)
            {
                moveSpeed = taskConfig["movespeed"].AsFloat(0.03f);
            }

            if (taskConfig["searchFrequency"] != null)
            {
                searchFrequency = taskConfig["searchFrequency"].AsFloat(0.07f);
            }

            if (taskConfig["awaitReached"] != null)
            {
                awaitReached = taskConfig["awaitReached"].AsBool(true);
            }

            
        }

        public override bool ShouldExecute()
        {
            if (rand.NextDouble() > searchFrequency) return false;

            double dx = rand.NextDouble() * 4 - 2;
            double dz = rand.NextDouble() * 4 - 2;

            tmpPos.Set((int)(entity.ServerPos.X + dx), 0, (int)(entity.ServerPos.Z + dz));
            tmpPos.Y = entity.World.BlockAccessor.GetTerrainMapheightAt(tmpPos) + 1;

            Block block = entity.World.BlockAccessor.GetBlock(tmpPos);
            if (block.Attributes == null) return false;

            if (block.Attributes["butterflyFeed"].AsBool() == true)
            {
                Block liquidBlock = entity.World.BlockAccessor.GetBlock(tmpPos, BlockLayersAccess.Fluid);
                if (liquidBlock.BlockId != 0) return false;   // If it's non-zero, the butterfly food is currently underwater or encased in ice!

                double topPos = block.Attributes["sitHeight"].AsDouble(block.TopMiddlePos.Y);

                EnumWindBitMode windMode = (EnumWindBitMode)block.Attributes["butteflyWindMode"].AsInt((int)EnumWindBitMode.NormalWind);
                entity.WatchedAttributes.SetInt("windMode", (int)windMode);

                MainTarget = tmpPos.ToVec3d().Add(block.TopMiddlePos.X, topPos, block.TopMiddlePos.Z);
                return true;
            }
            
            return false;
        }


        public override void StartExecute()
        {
            base.StartExecute();

            taskState = 0;
            pathTraverser.WalkTowards(MainTarget, moveSpeed, targetDistance, OnGoalReached, OnStuck);

            feedTime = 3 + rand.NextDouble() * 10;
        }

        public override bool ContinueExecute(float dt)
        {
            if (taskState==1)
            {
                entity.ServerPos.Motion.Set(0, 0, 0);
                entity.AnimManager.StartAnimation("feed");
                taskState = 2;
            }
            if (taskState == 2) feedTime -= dt;
            if (feedTime <= 0)
            {
                return false;
            }

            return taskState < 3;
        }

        public override void FinishExecute(bool cancelled)
        {
            base.FinishExecute(cancelled);

            if (cancelled)
            {
                pathTraverser.Stop();
            }

            entity.StopAnimation("feed");
        }

        private void OnStuck()
        {
            taskState = 3;
        }

        private void OnGoalReached()
        {
            taskState = 1;
        }
    }
}
