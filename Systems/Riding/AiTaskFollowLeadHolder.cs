using System;
using System.Drawing;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

#nullable disable

namespace Vintagestory.GameContent
{
    public class AiTaskFollowLeadHolder : AiTaskStayCloseToEntity
    {
        ClothManager cm;
        int minGeneration;
        long goalReachedEllapsedMs;
        ClothSystem cs;

        public AiTaskFollowLeadHolder(EntityAgent entity) : base(entity)
        {
        }

        public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig)
        {
            base.LoadConfig(taskConfig, aiConfig);
            minGeneration = taskConfig["minGeneration"].AsInt(0);
            cm = entity.World.Api.ModLoader.GetModSystem<ClothManager>();
        }

        public override bool ShouldExecute()
        {
            minSeekSeconds = 99;

            if (entity.WatchedAttributes.GetInt("generation") < minGeneration) return false;
            if (entity.World.ElapsedMilliseconds - goalReachedEllapsedMs < 1000) return false;

            var clothids = entity.GetBehavior<EntityBehaviorRopeTieable>()?.ClothIds?.value;

            if (clothids != null) 
            {
                for (int i = 0; i < clothids.Length; i++)
                {
                    cs = cm.GetClothSystem(clothids[i]);
                    if (cs == null) continue;

                    var point = getPinnedToPoint(cs, entity);
                    if (point != null)
                    {
                        targetEntity = point.PinnedToEntity;
                        if (targetEntity.ServerPos.DistanceTo(entity.ServerPos) < 2f) return false;
                        return true;
                    }
                }
            }

            return false;
        }



        public override void StartExecute()
        {
            float size = targetEntity.SelectionBox.XSize;
            pathTraverser.WalkTowards(targetEntity.ServerPos.XYZ, moveSpeed, size + 1f, OnGoalReached, OnStuck, EnumAICreatureType.Default);
            targetOffset.Set(0,0,0);
            stuck = false;
            base.StartExecute();
        }

        public override bool ContinueExecute(float dt)
        {
            //Check if time is still valid for task.
            if (!IsInValidDayTimeHours(false)) return false;

            float size = targetEntity.SelectionBox.XSize;
            var dist = targetEntity.ServerPos.DistanceTo(entity.ServerPos.XYZ);
            if (dist > 2)
            {
                initialTargetPos = targetEntity.ServerPos.XYZ;
                
                pathTraverser.WalkTowards(targetEntity.ServerPos.XYZ, moveSpeed, size + 1f, OnGoalReached, OnStuck, EnumAICreatureType.Default);
            }

            if (dist < size + 1f) return false;

            var point = getPinnedToPoint(cs, entity);
            if (point == null) return false;

            return true;
        }

        protected override void OnGoalReached()
        {
            goalReachedEllapsedMs = entity.World.ElapsedMilliseconds;
            base.OnGoalReached();
        }


        private ClothPoint getPinnedToPoint(ClothSystem cs, EntityAgent entity)
        {
            if (cs.FirstPoint.PinnedToEntity != null && cs.FirstPoint.PinnedToEntity.EntityId != entity.EntityId)
            {
                return cs.FirstPoint;
            }
            if (cs.LastPoint.PinnedToEntity != null && cs.LastPoint.PinnedToEntity.EntityId != entity.EntityId)
            {
                return cs.LastPoint;
            }

            return null;
        }
    }
}
