using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class AiTaskStayCloseToEntity : AiTaskBase
    {
        Entity targetEntity;
        float moveSpeed = 0.03f;
        float range = 8f;
        float maxDistance = 3f;
        string entityCode;
        bool stuck = false;
        bool onlyIfLowerId = false;

        Vec3d targetOffset = new Vec3d();

        public AiTaskStayCloseToEntity(EntityAgent entity) : base(entity)
        {
        }

        public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig)
        {
            base.LoadConfig(taskConfig, aiConfig);

            if (taskConfig["movespeed"] != null)
            {
                moveSpeed = taskConfig["movespeed"].AsFloat(0.03f);
            }

            if (taskConfig["searchRange"] != null)
            {
                range = taskConfig["searchRange"].AsFloat(8f);
            }

            if (taskConfig["maxDistance"] != null)
            {
                maxDistance = taskConfig["maxDistance"].AsFloat(3f);
            }

            if (taskConfig["onlyIfLowerId"] != null)
            {
                onlyIfLowerId = taskConfig["onlyIfLowerId"].AsBool();
            }

            entityCode = taskConfig["entityCode"].AsString();
        }


        public override bool ShouldExecute()
        {
            if (rand.NextDouble() > 0.01f) return false;

            if (targetEntity == null || !targetEntity.Alive)
            {
                targetEntity = entity.World.GetNearestEntity(entity.ServerPos.XYZ, range, 2, (e) => {
                    return e.Type.Code.Path.Equals(entityCode) && (!onlyIfLowerId || e.Entityid < entity.Entityid);
                });
            }

            if (targetEntity != null && (!targetEntity.Alive || targetEntity.ShouldDespawn)) targetEntity = null;
            if (targetEntity == null) return false;

            double x = targetEntity.ServerPos.X;
            double y = targetEntity.ServerPos.Y;
            double z = targetEntity.ServerPos.Z;

            double dist = entity.ServerPos.SquareDistanceTo(x, y, z);

            return dist > maxDistance * maxDistance;
        }


        public override void StartExecute()
        {
            base.StartExecute();

            float size = targetEntity.CollisionBox.X2 - targetEntity.CollisionBox.X1;

            entity.PathTraverser.GoTo(targetEntity.ServerPos.XYZ, moveSpeed, size + 0.2f, OnGoalReached, OnStuck);

            targetOffset.Set(entity.World.Rand.NextDouble() * 2 - 1, 0, entity.World.Rand.NextDouble() * 2 - 1);

            stuck = false;
        }


        public override bool ContinueExecute(float dt)
        {
            double x = targetEntity.ServerPos.X + targetOffset.X;
            double y = targetEntity.ServerPos.Y;
            double z = targetEntity.ServerPos.Z + targetOffset.Z;

            entity.PathTraverser.CurrentTarget.X = x;
            entity.PathTraverser.CurrentTarget.Y = y;
            entity.PathTraverser.CurrentTarget.Z = z;

            if (entity.ServerPos.SquareDistanceTo(x, y, z) < maxDistance * maxDistance / 4)
            {
                entity.PathTraverser.Stop();
                return false;
            }

            return targetEntity.Alive && !stuck && entity.PathTraverser.Active;
        }
        
        private void OnStuck()
        {
            stuck = true;
        }

        private void OnGoalReached()
        {
        
        }
    }
}
