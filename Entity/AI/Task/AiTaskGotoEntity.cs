using System.Collections.Generic;
using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class AiTaskGotoEntity : AiTaskBase
    {
        public Entity targetEntity;
        
        public float moveSpeed = 0.02f;
        public float seekingRange = 25f;
        public float maxFollowTime = 60;

        
        bool stuck = false;
        
        float currentFollowTime = 0;


        public AiTaskGotoEntity(EntityAgent entity, Entity target) : base(entity)
        {
            targetEntity = target;

            animMeta = new AnimationMetaData()
            {
                Code = "walk",
                Animation = "walk",
                AnimationSpeed = 1f
            }.Init();
        }
        


        public override bool ShouldExecute()
        {
            return false;
        }

        public float MinDistanceToTarget()
        {
            return System.Math.Max(0.8f, (targetEntity.CollisionBox.X2 - targetEntity.CollisionBox.X1) / 2 + (entity.CollisionBox.X2 - entity.CollisionBox.X1) / 2);
        }

        public override void StartExecute()
        {
            base.StartExecute();
            stuck = false;
            entity.PathTraverser.GoTo(targetEntity.ServerPos.XYZ, moveSpeed, MinDistanceToTarget(), OnGoalReached, OnStuck);
            currentFollowTime = 0;
        }

        public override bool ContinueExecute(float dt)
        {
            currentFollowTime += dt;

            entity.PathTraverser.CurrentTarget.X = targetEntity.ServerPos.X;
            entity.PathTraverser.CurrentTarget.Y = targetEntity.ServerPos.Y;
            entity.PathTraverser.CurrentTarget.Z = targetEntity.ServerPos.Z;

            Cuboidd targetBox = targetEntity.CollisionBox.ToDouble().Translate(targetEntity.ServerPos.X, targetEntity.ServerPos.Y, targetEntity.ServerPos.Z);
            Vec3d pos = entity.ServerPos.XYZ.Add(0, entity.CollisionBox.Y2 / 2, 0).Ahead((entity.CollisionBox.X2 - entity.CollisionBox.X1) / 2, 0, entity.ServerPos.Yaw);
            double distance = targetBox.ShortestDistanceFrom(pos);

            float minDist = MinDistanceToTarget();

            return
                currentFollowTime < maxFollowTime &&
                distance < seekingRange * seekingRange &&
                distance > minDist &&
                !stuck
            ;
        }

        public bool TargetReached()
        {
            Cuboidd targetBox = targetEntity.CollisionBox.ToDouble().Translate(targetEntity.ServerPos.X, targetEntity.ServerPos.Y, targetEntity.ServerPos.Z);
            Vec3d pos = entity.ServerPos.XYZ.Add(0, entity.CollisionBox.Y2 / 2, 0).Ahead((entity.CollisionBox.X2 - entity.CollisionBox.X1) / 2, 0, entity.ServerPos.Yaw);
            double distance = targetBox.ShortestDistanceFrom(pos);

            float minDist = MinDistanceToTarget();

            return distance < minDist;
        }


        public override void FinishExecute(bool cancelled)
        {
            base.FinishExecute(cancelled);
            entity.PathTraverser.Stop();
        }


        public override bool Notify(string key, object data)
        {
            return false;
        }


        private void OnStuck()
        {
            stuck = true;
        }

        private void OnGoalReached()
        {
            entity.PathTraverser.Active = true;
        }
    }
}
