using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Vintagestory.GameContent
{
    public enum EnumRestReason
    {
        NoReason,
        TakingABreak,
        Night,
        Wind
    }

    public class AiTaskButterflyRest : AiTaskBase
    {
        public Vec3d MainTarget;

        // 0=goto, 1=start resting, 2=resting
        int taskState;

        float moveSpeed = 0.03f;
        float targetDistance = 0.07f;

        double searchFrequency = 0.05f;
        double restUntilTotalHours;

        BlockPos tmpPos = new BlockPos();
        EnumRestReason reason;
        WeatherSystemServer wsys;


        public AiTaskButterflyRest(EntityAgent entity) : base(entity)
        {
            (entity.Api as ICoreServerAPI).Event.DidBreakBlock += Event_DidBreakBlock;

            wsys = entity.Api.ModLoader.GetModSystem<WeatherSystemServer>();
        }

        public override void OnEntityDespawn(EntityDespawnReason reason)
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

            cooldownUntilTotalHours = entity.World.Calendar.TotalHours + mincooldownHours + entity.World.Rand.NextDouble() * (maxcooldownHours - mincooldownHours);
        }

        public override bool ShouldExecute()
        {
            if (rand.NextDouble() > searchFrequency) return false;

            reason = EnumRestReason.NoReason;

            if (cooldownUntilTotalHours < entity.World.Calendar.TotalHours)
            {
                reason = EnumRestReason.TakingABreak;
            }
            else if (entity.World.Calendar.DayLightStrength < 0.6)
            {
                // Hardcoded: Rest at night 
                reason = EnumRestReason.Night;
            }
            else if (wsys?.GetWindSpeed(entity.ServerPos.XYZ) > 0.75 || wsys?.GetRainFall(entity.ServerPos.XYZ) > 0.1)
            {
                // Hardcoded: Rest during heavy winds or during rain
                reason = EnumRestReason.Wind;
            }

            if (reason == EnumRestReason.NoReason)
            {
                return false;
            }

            
            double dx = rand.NextDouble() * 4 - 2;
            double dz = rand.NextDouble() * 4 - 2;

            for (int i = 1; i >= 0; i--)
            {

                tmpPos.Set((int)(entity.ServerPos.X + dx), 0, (int)(entity.ServerPos.Z + dz));
                tmpPos.Y = entity.World.BlockAccessor.GetTerrainMapheightAt(tmpPos) + i;

                Block block = entity.World.BlockAccessor.GetBlock(tmpPos);
                if (block.IsLiquid()) return false;

                if (block.Attributes?["butterflyFeed"].AsBool() == true)
                {
                    double topPos = block.Attributes["sitHeight"].AsDouble(block.TopMiddlePos.Y);

                    entity.WatchedAttributes.SetDouble("sitHeight", block.VertexFlags.GrassWindWave ? topPos : 0);

                    MainTarget = tmpPos.ToVec3d().Add(block.TopMiddlePos.X, topPos, block.TopMiddlePos.Z);
                    return true;
                }

                if (block.SideSolid[BlockFacing.UP.Index])
                {
                    double topPos = block.TopMiddlePos.Y;
                    entity.WatchedAttributes.SetDouble("sitHeight", block.VertexFlags.GrassWindWave ? topPos : 0);
                    MainTarget = tmpPos.ToVec3d().Add(block.TopMiddlePos.X, topPos, block.TopMiddlePos.Z);
                    return true;
                }
            }
            
            return false;
        }


        public override void StartExecute()
        {
            base.StartExecute();

            taskState = 0;
            pathTraverser.WalkTowards(MainTarget, moveSpeed, targetDistance, OnGoalReached, OnStuck);
        }

        public override bool ContinueExecute(float dt)
        {
            if (taskState==1)
            {
                entity.ServerPos.Motion.Set(0, 0, 0);
                entity.AnimManager.StartAnimation("rest");
                taskState = 2;
                double restHours = 0.5 + entity.World.Rand.NextDouble() * 3;
                restUntilTotalHours = entity.World.Calendar.TotalHours + restHours;
            }

            if (entity.World.Rand.NextDouble() > 0.05) return true;

            switch (reason)
            {
                case EnumRestReason.Night:
                    return entity.World.Calendar.DayLightStrength < 0.8;
                case EnumRestReason.TakingABreak:
                    return taskState == 0 || entity.World.Calendar.TotalHours < restUntilTotalHours;
                case EnumRestReason.Wind:
                    return wsys?.GetWindSpeed(entity.ServerPos.XYZ) > 0.2 || wsys?.GetRainFall(entity.ServerPos.XYZ) > 0.05;
                default:
                    return false;
            }
        }

        public override void FinishExecute(bool cancelled)
        {
            base.FinishExecute(cancelled);

            if (cancelled)
            {
                pathTraverser.Stop();
            }

            entity.StopAnimation("rest");
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
