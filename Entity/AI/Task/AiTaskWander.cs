using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class AiTaskWander : AiTaskBase
    {
        public Vec3d MainTarget;

        bool done;
        float moveSpeed = 0.03f;
        float wanderChance = 0.015f;
        float maxHeight = 7f;
        float? preferredLightLevel;

        bool awaitReached = true;

        NatFloat wanderRange = NatFloat.createStrongerInvexp(3, 30);
        BlockPos tmpPos = new BlockPos();


        public AiTaskWander(EntityAgent entity) : base(entity)
        {
            
        }

        public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig)
        {
            base.LoadConfig(taskConfig, aiConfig);

            if (taskConfig["movespeed"] != null)
            {
                moveSpeed = taskConfig["movespeed"].AsFloat(0.03f);
            }

            if (taskConfig["wanderChance"] != null)
            {
                wanderChance = taskConfig["wanderChance"].AsFloat(0.015f);
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

            if (taskConfig["awaitReached"] != null)
            {
                awaitReached = taskConfig["awaitReached"].AsBool(true);
            }
            
        }

        public override bool ShouldExecute()
        {
            if (rand.NextDouble() > wanderChance) return false;

            List<Vec3d> goodtargets = new List<Vec3d>();

            int tries = 9;
            while (tries-- > 0)
            {
                int terrainYPos = entity.World.BlockAccessor.GetTerrainMapheightAt(tmpPos);

                float dx = wanderRange.nextFloat() * (rand.Next(2) * 2 - 1);
                float dy = wanderRange.nextFloat() * (rand.Next(2) * 2 - 1);
                float dz = wanderRange.nextFloat() * (rand.Next(2) * 2 - 1);

                
                MainTarget = entity.ServerPos.XYZ.Add(dx, dy, dz);
                MainTarget.Y = Math.Min(MainTarget.Y, terrainYPos + maxHeight);

                tmpPos.X = (int)MainTarget.X;
                tmpPos.Z = (int)MainTarget.Z;
                
                if ((entity.Controls.IsClimbing && !entity.Type.FallDamage) || (entity.Type.Habitat != EnumHabitat.Land))
                {
                    if (entity.Type.Habitat == EnumHabitat.Sea)
                    {
                        Block block = entity.World.BlockAccessor.GetBlock(tmpPos);
                        return block.IsLiquid();
                    }

                    return true;
                }
                else
                {
                    int yDiff = (int)entity.ServerPos.Y - terrainYPos;

                    double slopeness = yDiff / Math.Max(1, GameMath.Sqrt(MainTarget.HorizontalSquareDistanceTo(entity.ServerPos.XYZ)) - 2);

                    tmpPos.Y = terrainYPos;
                    Block block = entity.World.BlockAccessor.GetBlock(tmpPos);
                    Block belowblock = entity.World.BlockAccessor.GetBlock(tmpPos.X, tmpPos.Y - 1, tmpPos.Z);

                    bool canStep = block.CollisionBoxes == null || block.CollisionBoxes.Max((cuboid) => cuboid.Y2) <= 1f;
                    bool canStand = belowblock.CollisionBoxes != null && belowblock.CollisionBoxes.Length > 0;

                    if (slopeness < 3 && canStand && canStep)
                    {
                        if (preferredLightLevel == null) return true;
                        goodtargets.Add(MainTarget);
                    }
                }
            }

            int smallestdiff = 999;
            Vec3d bestTarget = null;
            for (int i = 0; i < goodtargets.Count; i++)
            {
                int lightdiff = Math.Abs((int)preferredLightLevel - entity.World.BlockAccessor.GetLightLevel(goodtargets[i].AsBlockPos, EnumLightLevelType.MaxLight));

                if (lightdiff < smallestdiff)
                {
                    smallestdiff = lightdiff;
                    bestTarget = goodtargets[i];
                }
            }

            if (bestTarget != null)
            {
                MainTarget = bestTarget;
                return true;
            }

            return false;
        }


        public override void StartExecute()
        {
            base.StartExecute();

            done = false;
            entity.PathTraverser.GoTo(MainTarget, moveSpeed, OnGoalReached, OnStuck);
        }

        public override bool ContinueExecute(float dt)
        {
            if (!awaitReached) return false;

            //entity.World.SpawnParticles(1, ColorUtil.WhiteArgb, target.AddCopy(new Vec3f(-0.1f, -0.1f, -0.1f)), target.AddCopy(new Vec3f(0.1f, 0.1f, 0.1f)), new Vec3f(), new Vec3f(), 1f, 0f);

            // If we are a climber dude and encountered a wall, let's not try to get behind the wall
            // We do that by removing the coord component that would make the entity want to walk behind the wall
            if (entity.Controls.IsClimbing && entity.Type.CanClimbAnywhere && entity.ClimbingOnFace != null)
            {
                BlockFacing facing = entity.ClimbingOnFace;

                if (Math.Sign(facing.Normali.X) == Math.Sign(entity.PathTraverser.CurrentTarget.X - entity.ServerPos.X))
                {
                    entity.PathTraverser.CurrentTarget.X = entity.ServerPos.X;
                }

                if (Math.Sign(facing.Normali.Z) == Math.Sign(entity.PathTraverser.CurrentTarget.Z - entity.ServerPos.Z))
                {
                    entity.PathTraverser.CurrentTarget.Z = entity.ServerPos.Z;
                }
            }

            return !done;
        }

        public override void FinishExecute(bool cancelled)
        {
            base.FinishExecute(cancelled);

            if (cancelled)
            {
                entity.PathTraverser.Stop();
            }
        }

        private void OnStuck()
        {
            done = true;
        }

        private void OnGoalReached()
        {
            done = true;
        }
    }
}
