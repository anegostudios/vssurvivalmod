using System;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace Vintagestory.GameContent
{
    public class AiTaskFollowLeadHolder : AiTaskStayCloseToEntity
    {
        protected ClothManager clothManager;
        protected ClothSystem? clothSystem;

        public AiTaskFollowLeadHolder(EntityAgent entity, JsonObject taskConfig, JsonObject aiConfig) : base(entity, taskConfig, aiConfig)
        {
            clothManager = entity.World.Api.ModLoader.GetModSystem<ClothManager>()
                ?? throw new Exception("FollowLeadHolder depends on the ClothManager mod system.");
        }

        protected override void SetDefaultValues()
        {
            base.SetDefaultValues();
            ExecutionChance = 1;
            arrivalDistance = 2f;
            maxDistance = 4f;
            retargetDistance = 0.5f;
        }

        public override bool ShouldExecute()
        {
            // Deliberately skip cooldown check

            if (rand.NextDouble() > ExecutionChance) return false;
            if (!PreconditionsSatisfied()) return false;

            var clothids = entity.GetBehavior<EntityBehaviorRopeTieable>()?.ClothIds?.value;

            if (clothids == null) return false;

            for (int i = 0; i < clothids.Length; i++)
            {
                clothSystem = clothManager.GetClothSystem(clothids[i]);
                if (clothSystem == null) continue;

                var point = GetOtherEndOfRope(clothSystem, entity);
                if (point?.PinnedToEntity != null)
                {
                    targetEntity = point.PinnedToEntity;
                    return targetEntity.Alive && targetEntity.Pos.SquareDistanceTo(entity.Pos) > maxDistance * maxDistance;
                }
            }

            return false;
        }

        public override void StartExecute()
        {
            base.StartExecute();
            targetOffset.Set(0,0,0);
        }

        public override bool ContinueExecute(float dt)
        {
            if (clothSystem == null) return false;
            targetEntity = GetOtherEndOfRope(clothSystem, entity)?.PinnedToEntity;
            return base.ContinueExecute(dt);
        }

        public static ClothPoint? GetOtherEndOfRope(ClothSystem clothSystem, EntityAgent entity)
        {
            if (clothSystem.FirstPoint.PinnedToEntity != null && clothSystem.FirstPoint.PinnedToEntity.EntityId == entity.EntityId)
            {
                return clothSystem.LastPoint;
            }
            if (clothSystem.LastPoint.PinnedToEntity != null && clothSystem.LastPoint.PinnedToEntity.EntityId == entity.EntityId)
            {
                return clothSystem.FirstPoint;
            }

            return null;
        }
    }
}
