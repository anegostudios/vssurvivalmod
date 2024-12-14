using System;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public abstract class AiTaskTargetableAt : AiTaskBaseTargetable
    {
        public Vec3d SpawnPos;
        public Vec3d CenterPos;

        protected AiTaskTargetableAt(EntityAgent entity) : base(entity)
        {
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
    }

    public class AiTaskFlyCircle : AiTaskTargetableAt
    {
        bool stayNearSpawn;
        float minRadius;
        float maxRadius;
        float height;
        protected double desiredYPos;
        protected float moveSpeed = 0.04f;

        double dir = 1;
        float dirchangeCoolDown = 0;


        double nowRadius;
        

        public AiTaskFlyCircle(EntityAgent entity) : base(entity)
        {
        }

        public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig)
        {
            base.LoadConfig(taskConfig, aiConfig);

            stayNearSpawn = taskConfig["stayNearSpawn"].AsBool(false);
            minRadius = taskConfig["minRadius"].AsFloat(10f);
            maxRadius = taskConfig["maxRadius"].AsFloat(20f);
            height = taskConfig["height"].AsFloat(5f);
            moveSpeed = taskConfig["moveSpeed"].AsFloat(0.04f);            
        }

        public override bool ShouldExecute() { return true; }


        public override void StartExecute()
        {
            nowRadius = minRadius + (float)world.Rand.NextDouble() * (maxRadius - minRadius);

            float yaw = (float)world.Rand.NextDouble() * GameMath.TWOPI;
            var rndx = nowRadius * Math.Sin(yaw);
            var rndz = nowRadius * Math.Cos(yaw);


            if (stayNearSpawn)
            {
                CenterPos = SpawnPos;
                
            } else {

                CenterPos = entity.ServerPos.XYZ.Add(rndx, 0, rndz);
            }
            

            base.StartExecute();
        }

        public override bool ContinueExecute(float dt)
        {
            if ((int)CenterPos.Y / BlockPos.DimensionBoundary != entity.Pos.Dimension) return false;
            if (entity.OnGround || entity.World.Rand.NextDouble() < 0.03)
            {
                ReadjustFlyHeight();
            }

            double dy = desiredYPos - entity.ServerPos.Y;
            double yMot = GameMath.Clamp(dy, -0.33, 0.33);

            double dx = entity.ServerPos.X - CenterPos.X;
            double dz = entity.ServerPos.Z - CenterPos.Z;
            double rad = Math.Sqrt(dx*dx + dz*dz);
            double offs = nowRadius - rad;

            float targetYaw = (float)Math.Atan2(dx, dz) + GameMath.PIHALF + 0.1f * (float)dir;

            entity.ServerPos.Yaw += GameMath.AngleRadDistance(entity.ServerPos.Yaw, targetYaw) * dt;

            //entity.World.SpawnParticles(1, ColorUtil.ColorFromRgba(255,0,0,255), CenterPos, CenterPos, new Vec3f(), new Vec3f(), 1, 1, 3);

            float bla = (float)GameMath.Clamp(offs / 20.0, -1, 1);
            double cosYaw = Math.Cos(entity.ServerPos.Yaw - bla);
            double sinYaw = Math.Sin(entity.ServerPos.Yaw - bla);
            entity.Controls.WalkVector.Set(sinYaw, yMot, cosYaw);
            entity.Controls.WalkVector.Mul(moveSpeed);
            if (yMot < 0) entity.Controls.WalkVector.Mul(0.5);

            if (entity.Swimming)
            {
                entity.Controls.WalkVector.Y = 2 * moveSpeed;
                entity.Controls.FlyVector.Y = 2 * moveSpeed;
            }

            dirchangeCoolDown = Math.Max(0, dirchangeCoolDown - dt);
            if (entity.CollidedHorizontally && dirchangeCoolDown <= 0)
            {
                dirchangeCoolDown = 2;
                dir *= -1;
            }

            return entity.Alive;
        }


        protected void ReadjustFlyHeight()
        {
            int terrainYPos = entity.World.BlockAccessor.GetTerrainMapheightAt(entity.SidedPos.AsBlockPos);
            int tries = 10;
            while (tries-- > 0)
            {
                Block block = entity.World.BlockAccessor.GetBlock((int)entity.ServerPos.X, terrainYPos, (int)entity.ServerPos.Z, BlockLayersAccess.Fluid);
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
