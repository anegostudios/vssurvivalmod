using System;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent
{
    public class AiTaskFlyWander : AiTaskBase
    {
        bool stayNearSpawn;
        float radius;
        float height;
        float minDistance;
        protected double desiredYPos;
        protected float moveSpeed = 0.04f;
        public Vec3d SpawnPos;

        public Vec3d targetPos;

        float targetTolerangeRange;

        public AiTaskFlyWander(EntityAgent entity, JsonObject taskConfig, JsonObject aiConfig) : base(entity, taskConfig, aiConfig)
        {
            stayNearSpawn = taskConfig["stayNearSpawn"].AsBool(false);
            radius = taskConfig["radius"].AsFloat(10f);
            height = taskConfig["height"].AsFloat(5f);
            minDistance = taskConfig["minDistance"].AsFloat(10f);
            moveSpeed = taskConfig["moveSpeed"].AsFloat(0.04f);
            targetTolerangeRange = taskConfig["targetTolerangeRange"].AsFloat(15f);
        }

        public override void OnEntityLoaded()
        {
            loadOrCreateSpawnPos();
        }

        public override void OnEntitySpawn()
        {
            loadOrCreateSpawnPos();
        }

        public void loadOrCreateSpawnPos()
        {
            if (entity.WatchedAttributes.HasAttribute("spawnPosX"))
            {
                SpawnPos = new Vec3d(
                    entity.WatchedAttributes.GetDouble("spawnPosX"),
                    entity.WatchedAttributes.GetDouble("spawnPosY"),
                    entity.WatchedAttributes.GetDouble("spawnPosZ")
                );
            }
            else
            {
                SpawnPos = entity.ServerPos.XYZ;
                entity.WatchedAttributes.SetDouble("spawnPosX", SpawnPos.X);
                entity.WatchedAttributes.SetDouble("spawnPosY", SpawnPos.Y);
                entity.WatchedAttributes.SetDouble("spawnPosZ", SpawnPos.Z);
            }
        }

        public override bool ShouldExecute()
        {
            if (cooldownUntilMs > entity.World.ElapsedMilliseconds) return false;
            return true;
        }

        public override void StartExecute()
        {
            base.StartExecute();

            var fromPos = stayNearSpawn ? SpawnPos : entity.ServerPos.XYZ;

            double rndx=0;
            double rndz=0;
            for (int i = 0; i < 10; i++)
            {
                float yaw = (float)world.Rand.NextDouble() * GameMath.TWOPI;
                rndx = radius * Math.Sin(yaw);
                rndz = radius * Math.Cos(yaw);

                if (fromPos.AddCopy(rndx, 0, rndz).HorizontalSquareDistanceTo(entity.ServerPos.XYZ) > minDistance) break;
            }

            targetPos = fromPos.AddCopy(rndx, 0, rndz);
        }

        public override bool 
            ContinueExecute(float dt)
        {
            //Check if time is still valid for task.
            if (!IsInValidDayTimeHours(false)) return false;

            if (entity.OnGround || entity.World.Rand.NextDouble() < 0.03)
            {
                ReadjustFlyHeight();
            }

            double dy = desiredYPos - entity.ServerPos.Y;
            double yMot = GameMath.Clamp(dy, -0.2, 0.2);

            double dx = targetPos.X - entity.ServerPos.X;
            double dz = targetPos.Z - entity.ServerPos.Z;

            float targetYaw = (float)Math.Atan2(dx, dz);

            entity.ServerPos.Yaw += GameMath.AngleRadDistance(entity.ServerPos.Yaw, targetYaw) * dt;

            double cosYaw = Math.Cos(entity.ServerPos.Yaw);
            double sinYaw = Math.Sin(entity.ServerPos.Yaw);

            entity.Controls.WalkVector.Set(sinYaw, yMot, cosYaw);
            entity.Controls.WalkVector.Mul(moveSpeed);

            if (yMot < 0) entity.Controls.WalkVector.Mul(0.5);

            if (entity.Swimming)
            {
                entity.Controls.WalkVector.Y = 2 * moveSpeed;
                entity.Controls.FlyVector.Y = 2 * moveSpeed;
            }

            return entity.Alive && (dx*dx+dz*dz) > targetTolerangeRange * targetTolerangeRange;
        }


        protected void ReadjustFlyHeight()
        {
            int terrainYPos = entity.World.BlockAccessor.GetTerrainMapheightAt(entity.SidedPos.AsBlockPos);
            int tries = 10;
            while (tries-- > 0)
            {
                Block block = entity.World.BlockAccessor.GetBlockRaw((int)entity.ServerPos.X, terrainYPos, (int)entity.ServerPos.Z, BlockLayersAccess.Fluid);
                if (block.IsLiquid())
                {
                    terrainYPos++;
                }
                else
                {
                    break;
                }
            }

            desiredYPos = terrainYPos + height;
        }

    }
}
