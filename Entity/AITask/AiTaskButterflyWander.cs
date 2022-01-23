using System;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class AiTaskButterflyWander : AiTaskBase
    {
        protected float moveSpeed = 0.03f;
        protected float maxHeight = 7f;
        protected float? preferredLightLevel;

        protected float wanderDuration;
        protected float desiredYaw;
        protected float desiredflyHeightAboveGround;

        protected double desiredYPos;

        protected float minTurnAnglePerSec;
        protected float maxTurnAnglePerSec;
        protected float curTurnRadPerSec;


        public AiTaskButterflyWander(EntityAgent entity) : base(entity)
        {

        }

        public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig)
        {
            base.LoadConfig(taskConfig, aiConfig);
            
            if (taskConfig["movespeed"] != null)
            {
                moveSpeed = taskConfig["movespeed"].AsFloat(0.03f);
            }

            if (taskConfig["maxHeight"] != null)
            {
                maxHeight = taskConfig["maxHeight"].AsFloat(7f);
            }

            if (taskConfig["preferredLightLevel"] != null)
            {
                preferredLightLevel = taskConfig["preferredLightLevel"].AsFloat(-99);
                if (preferredLightLevel < 0) preferredLightLevel = null;
            }


            if (entity?.Properties?.Server?.Attributes != null)
            {
                minTurnAnglePerSec = (float)entity.Properties.Server?.Attributes.GetTreeAttribute("pathfinder").GetFloat("minTurnAnglePerSec", 250);
                maxTurnAnglePerSec = (float)entity.Properties.Server?.Attributes.GetTreeAttribute("pathfinder").GetFloat("maxTurnAnglePerSec", 450);
            }
            else
            {
                minTurnAnglePerSec = 250;
                maxTurnAnglePerSec = 450;
            }
        }


        public override bool ShouldExecute()
        {
            return true;
        }


        public override void StartExecute()
        {
            base.StartExecute();

            wanderDuration = 0.5f + (float)entity.World.Rand.NextDouble() * (float)entity.World.Rand.NextDouble() * 1;
            desiredYaw = (float)(entity.ServerPos.Yaw + 2 * GameMath.TWOPI * (entity.World.Rand.NextDouble() - 0.5));

            desiredflyHeightAboveGround = 1 + 4 * (float)entity.World.Rand.NextDouble() + 4 * (float)(entity.World.Rand.NextDouble() * entity.World.Rand.NextDouble());
            ReadjustFlyHeight();

            entity.Controls.Forward = true;
            curTurnRadPerSec = minTurnAnglePerSec + (float)entity.World.Rand.NextDouble() * (maxTurnAnglePerSec - minTurnAnglePerSec);
            curTurnRadPerSec *= GameMath.DEG2RAD * 50 * moveSpeed;
        }


        public override bool ContinueExecute(float dt)
        {
            if (entity.OnGround || entity.World.Rand.NextDouble() < 0.03)
            {
                ReadjustFlyHeight();
            }

            wanderDuration -= dt;

            double dy = desiredYPos - entity.ServerPos.Y;
            double yMot = GameMath.Clamp(dy, -1, 1);

            
            float yawDist = GameMath.AngleRadDistance(entity.ServerPos.Yaw, desiredYaw);

            if (!entity.FeetInLiquid)
            {
                entity.ServerPos.Yaw += GameMath.Clamp(yawDist, -curTurnRadPerSec * dt * (yMot < 0 ? 0.25f : 1), curTurnRadPerSec * dt * (yMot < 0 ? 0.25f : 1));
                entity.ServerPos.Yaw = entity.ServerPos.Yaw % GameMath.TWOPI;
            }

            double cosYaw = Math.Cos(entity.ServerPos.Yaw);
            double sinYaw = Math.Sin(entity.ServerPos.Yaw);
            entity.Controls.WalkVector.Set(sinYaw, yMot, cosYaw);
            entity.Controls.WalkVector.Mul(moveSpeed);

            if (yMot < 0) entity.Controls.WalkVector.Mul(0.75);
            

            if (entity.Swimming)
            {
                entity.Controls.WalkVector.Y = 2 * moveSpeed;
                entity.Controls.FlyVector.Y = 2 * moveSpeed;
            }

            if (entity.CollidedHorizontally)
            {
                wanderDuration -= 10 * dt;
            }


            return wanderDuration > 0;
        }


        protected void ReadjustFlyHeight()
        {
            int terrainYPos = entity.World.BlockAccessor.GetTerrainMapheightAt(entity.SidedPos.AsBlockPos);
            int tries = 10;
            while (tries-- > 0)
            {
                Block block = entity.World.BlockAccessor.GetBlock((int)entity.ServerPos.X, terrainYPos, (int)entity.ServerPos.Z);
                if (block.IsLiquid())
                {
                    terrainYPos++;
                } else
                {
                    break;
                }
            }

            desiredYPos = terrainYPos + desiredflyHeightAboveGround;
        }


        public override void FinishExecute(bool cancelled)
        {
            base.FinishExecute(cancelled);
            
        }
        
    }
}
