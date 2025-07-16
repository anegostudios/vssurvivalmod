using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent
{
    public class AiTaskThrowAtEntity : AiTaskBaseTargetable
    {
        int durationMs;
        int releaseAtMs;
        long lastSearchTotalMs;
        float maxDist = 15f;
        protected int searchWaitMs = 2000;
        float accum = 0;
        bool didThrow;
        float minTurnAnglePerSec;
        float maxTurnAnglePerSec;
        float curTurnRadPerSec;
        float projectileDamage;
        int projectileDamageTier;
        AssetLocation projectileCode;
        float maxTurnAngleRad;
        float maxOffAngleThrowRad;
        float spawnAngleRad;
        float yawInaccuracy;
        bool immobile;

        public AiTaskThrowAtEntity(EntityAgent entity, JsonObject taskConfig, JsonObject aiConfig) : base(entity, taskConfig, aiConfig)
        {
            this.durationMs = taskConfig["durationMs"].AsInt(1500);
            this.releaseAtMs = taskConfig["releaseAtMs"].AsInt(1000);
            this.projectileDamage = taskConfig["projectileDamage"].AsFloat(1f);
            this.projectileDamageTier = taskConfig["projectileDamageTier"].AsInt(0);
            this.maxDist = taskConfig["maxDist"].AsFloat(15f);
            this.yawInaccuracy = taskConfig["yawInaccuracy"].AsFloat(0f);

            this.projectileCode = AssetLocation.Create(taskConfig["projectileCode"].AsString("thrownstone-{rock}"), entity.Code.Domain);

            this.immobile = taskConfig["immobile"].AsBool(false);
            maxTurnAngleRad = taskConfig["maxTurnAngleDeg"].AsFloat(360) * GameMath.DEG2RAD;
            maxOffAngleThrowRad = taskConfig["maxOffAngleThrowDeg"].AsFloat(0) * GameMath.DEG2RAD;
            spawnAngleRad = entity.Attributes.GetFloat("spawnAngleRad");
        }


        public override bool ShouldExecute()
        {
            // React immediately on hurt, otherwise only 1/10 chance of execution
            if (rand.NextDouble() > 0.1f && (WhenInEmotionState == null || IsInEmotionState(WhenInEmotionState) != true)) return false;

            if (!PreconditionsSatisifed()) return false;
            if (lastSearchTotalMs + searchWaitMs > entity.World.ElapsedMilliseconds) return false;
            if (WhenInEmotionState == null && rand.NextDouble() > 0.5f) return false;
            if (cooldownUntilMs > entity.World.ElapsedMilliseconds) return false;

            float range = maxDist;
            lastSearchTotalMs = entity.World.ElapsedMilliseconds;

            targetEntity = partitionUtil.GetNearestEntity(entity.ServerPos.XYZ, range, (e) => IsTargetableEntity(e, range) && hasDirectContact(e, range, range/2f) && aimableDirection(e), EnumEntitySearchType.Creatures);

            return targetEntity != null;
        }

        private bool aimableDirection(Entity e)
        {
            if (!immobile) return true;

            float aimYaw = getAimYaw(e);

            return aimYaw > spawnAngleRad - maxTurnAngleRad - maxOffAngleThrowRad && aimYaw < spawnAngleRad + maxTurnAngleRad + maxOffAngleThrowRad;
        }

        public override void StartExecute()
        {
            base.StartExecute();

            accum = 0;
            didThrow = false;

            ITreeAttribute pathfinder = entity?.Properties.Server?.Attributes?.GetTreeAttribute("pathfinder");
            if (pathfinder != null)
            {
                minTurnAnglePerSec = pathfinder.GetFloat("minTurnAnglePerSec", 250);
                maxTurnAnglePerSec = pathfinder.GetFloat("maxTurnAnglePerSec", 450);
            }
            else
            {
                minTurnAnglePerSec = 250;
                maxTurnAnglePerSec = 450;
            }

            curTurnRadPerSec = minTurnAnglePerSec + (float)entity.World.Rand.NextDouble() * (maxTurnAnglePerSec - minTurnAnglePerSec);
            curTurnRadPerSec *= GameMath.DEG2RAD * 50 * 0.02f;
        }



        public override bool 
            ContinueExecute(float dt)
        {
            //Check if time is still valid for task.
            if (!IsInValidDayTimeHours(false)) return false;

            float desiredYaw = getAimYaw(targetEntity);
            desiredYaw = GameMath.Clamp(desiredYaw, spawnAngleRad - maxTurnAngleRad, spawnAngleRad + maxTurnAngleRad);

            float yawDist = GameMath.AngleRadDistance(entity.ServerPos.Yaw, desiredYaw);
            entity.ServerPos.Yaw += GameMath.Clamp(yawDist, -curTurnRadPerSec * dt, curTurnRadPerSec * dt);
            entity.ServerPos.Yaw = entity.ServerPos.Yaw % GameMath.TWOPI;

            if (animMeta != null)
            {
                animMeta.EaseInSpeed = 1f;
                animMeta.EaseOutSpeed = 1f;
                entity.AnimManager.StartAnimation(animMeta);
            }

            accum += dt;

            if (accum > releaseAtMs / 1000f && !didThrow)
            {
                didThrow = true;

                var loc = projectileCode.Clone();
                string rocktype = "granite";
                var ba = entity.World.BlockAccessor;
                var mc = ba.GetMapChunkAtBlockPos(entity.Pos.AsBlockPos);
                if (mc != null)
                {
                    int lz = (int)entity.Pos.Z % GlobalConstants.ChunkSize;
                    int lx = (int)entity.Pos.X % GlobalConstants.ChunkSize;
                    var rockBlock = entity.World.Blocks[mc.TopRockIdMap[lz * GlobalConstants.ChunkSize + lx]];
                    rocktype = rockBlock.Variant["rock"] ?? "granite";
                }
                loc.Path = loc.Path.Replace("{rock}", rocktype);


                EntityProperties type = entity.World.GetEntityType(loc);
                if (type == null)
                {
                    throw new Exception("No such projectile exists - " + loc);
                }
                var entitypr = entity.World.ClassRegistry.CreateEntity(type) as EntityThrownStone;
                entitypr.FiredBy = entity;
                entitypr.Damage = projectileDamage;
                entitypr.DamageTier = projectileDamageTier;
                entitypr.ProjectileStack = new ItemStack(entity.World.GetItem(new AssetLocation("stone-granite")));
                entitypr.NonCollectible = true;

                Vec3d pos = entity.ServerPos.XYZ.Add(0, entity.LocalEyePos.Y, 0);
                Vec3d targetPos = targetEntity.ServerPos.XYZ.Add(0, targetEntity.LocalEyePos.Y, 0) + targetEntity.ServerPos.Motion * 8;

                double distf = Math.Pow(pos.SquareDistanceTo(targetPos), 0.1);
                Vec3d velocity = (targetPos - pos).Normalize() * GameMath.Clamp(distf - 1f, 0.1f, 1f);

                if (yawInaccuracy > 0)
                {
                    var rnd = entity.World.Rand;
                    velocity = velocity.RotatedCopy((float)(rnd.NextDouble() * yawInaccuracy - yawInaccuracy / 2.0));
                }

                entitypr.ServerPos.SetPosWithDimension(
                    entity.ServerPos.BehindCopy(0.21).XYZ.Add(0, entity.LocalEyePos.Y, 0)
                );

                entitypr.ServerPos.Motion.Set(velocity);

                entitypr.Pos.SetFrom(entitypr.ServerPos);
                entitypr.World = entity.World;
                entity.World.SpawnPriorityEntity(entitypr);
            }

            return accum < durationMs / 1000f;
        }

        private float getAimYaw(Entity targetEntity)
        {
            Vec3f targetVec = new Vec3f();

            targetVec.Set(
                (float)(targetEntity.ServerPos.X - entity.ServerPos.X),
                (float)(targetEntity.ServerPos.Y - entity.ServerPos.Y),
                (float)(targetEntity.ServerPos.Z - entity.ServerPos.Z)
            );

            float desiredYaw = (float)Math.Atan2(targetVec.X, targetVec.Z);
            
            return desiredYaw;
        }
    }
}
