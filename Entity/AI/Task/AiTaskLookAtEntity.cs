using System;
using System.Collections.Generic;
using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class AiTaskLookAtEntity : AiTaskBase
    {
        public Entity targetEntity;

        public float moveSpeed = 0.02f;
        public float seekingRange = 25f;
        public float maxFollowTime = 60;

        float minTurnAnglePerSec;
        float maxTurnAnglePerSec;
        float curTurnRadPerSec;

        public AiTaskLookAtEntity(EntityAgent entity, Entity target) : base(entity)
        {
            targetEntity = target;
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

            if (entity?.Type?.Server?.Attributes != null)
            {
                minTurnAnglePerSec = (float)entity.Type.Server?.Attributes["pathfinder"]["minTurnAnglePerSec"].AsFloat(250);
                maxTurnAnglePerSec = (float)entity.Type.Server?.Attributes["pathfinder"]["maxTurnAnglePerSec"].AsFloat(450);
            }
            else
            {
                minTurnAnglePerSec = 250;
                maxTurnAnglePerSec = 450;
            }

            curTurnRadPerSec = minTurnAnglePerSec + (float)entity.World.Rand.NextDouble() * (maxTurnAnglePerSec - minTurnAnglePerSec);
            curTurnRadPerSec *= GameMath.DEG2RAD * 50 * 0.02f;
        }

        public override bool ContinueExecute(float dt)
        {
            Vec3f targetVec = new Vec3f();

            targetVec.Set(
                (float)(targetEntity.ServerPos.X - entity.ServerPos.X),
                (float)(targetEntity.ServerPos.Y - entity.ServerPos.Y),
                (float)(targetEntity.ServerPos.Z - entity.ServerPos.Z)
            );

            float desiredYaw = (float)Math.Atan2(targetVec.X, targetVec.Z);

            float yawDist = GameMath.AngleRadDistance(entity.ServerPos.Yaw, desiredYaw);
            entity.ServerPos.Yaw += GameMath.Clamp(yawDist, -curTurnRadPerSec * dt, curTurnRadPerSec * dt);
            entity.ServerPos.Yaw = entity.ServerPos.Yaw % GameMath.TWOPI;

            return Math.Abs(yawDist) > 0.01;
        }

        public bool TargetReached()
        {
            Cuboidd targetBox = targetEntity.CollisionBox.ToDouble().Translate(targetEntity.ServerPos.X, targetEntity.ServerPos.Y, targetEntity.ServerPos.Z);
            Vec3d pos = entity.ServerPos.XYZ.Add(0, entity.CollisionBox.Y2 / 2, 0).Ahead((entity.CollisionBox.X2 - entity.CollisionBox.X1) / 2, 0, entity.ServerPos.Yaw);
            double distance = targetBox.ShortestDistanceFrom(pos);

            float minDist = MinDistanceToTarget();

            return distance < minDist;
        }

        

        public override bool Notify(string key, object data)
        {
            return false;
        }
    }
}
