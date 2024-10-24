using System;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public enum EnumTurretState
    {
        Idle,
        TurretMode,
        TurretModeLoad,
        TurretModeHold,
        TurretModeFired,
        TurretModeReload,
        TurretModeUnload,
        Stop
    }

    // When in sensing range
    // Enter turret mode

    // When in firing range and in turret mode
    // Load bolt
    //   If target too close: Unload
    //   If target too far: Wait 2-3 seconds. If still too far. Unload
    //   If outside sensing range exit turret mode. Stop task.
    // Fire bolt
    // Reload
    // If target too coose or outside sensing range - Stop here.
    // Load bolt from turret pose
    public class AiTaskTurretMode : AiTaskBaseTargetable
    {
        int durationMs;
        int releaseAtMs;
        long lastSearchTotalMs;
        protected int searchWaitMs = 2000;
        float accum = 0;
        bool didThrow;
        float minTurnAnglePerSec;
        float maxTurnAnglePerSec;
        float curTurnRadPerSec;
        float projectileDamage;
        AssetLocation projectileCode;
        float maxTurnAngleRad;
        float maxOffAngleThrowRad;
        float spawnAngleRad;
        bool immobile;

        float sensingRange;
        float firingRangeMin;
        float firingRangeMax;
        float abortRange;
        EnumTurretState currentState;
        float currentStateTime;

        bool executing = false;

        public AiTaskTurretMode(EntityAgent entity) : base(entity)
        {
        }

        public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig)
        {
            base.LoadConfig(taskConfig, aiConfig);
            this.durationMs = taskConfig["durationMs"].AsInt(1500);
            this.releaseAtMs = taskConfig["releaseAtMs"].AsInt(1000);
            this.projectileDamage = taskConfig["projectileDamage"].AsFloat(1f);

            this.sensingRange = taskConfig["sensingRange"].AsFloat(30f);
            this.firingRangeMin = taskConfig["firingRangeMin"].AsFloat(14f);
            this.firingRangeMax = taskConfig["firingRangeMax"].AsFloat(26f);
            this.abortRange = taskConfig["abortRange"].AsFloat(14f);

            this.projectileCode = AssetLocation.Create(taskConfig["projectileCode"].AsString("thrownstone-{rock}"), entity.Code.Domain);

            this.immobile = taskConfig["immobile"].AsBool(false);
            maxTurnAngleRad = taskConfig["maxTurnAngleDeg"].AsFloat(360) * GameMath.DEG2RAD;
            maxOffAngleThrowRad = taskConfig["maxOffAngleThrowDeg"].AsFloat(0) * GameMath.DEG2RAD;
            spawnAngleRad = entity.Attributes.GetFloat("spawnAngleRad");
        }

        public override void AfterInitialize()
        {
            base.AfterInitialize();

            entity.AnimManager.OnAnimationStopped += AnimManager_OnAnimationStopped;
        }

        private void AnimManager_OnAnimationStopped(string anim)
        {
            if (!executing || targetEntity == null) return;
            updateState();
        }

        public override bool ShouldExecute()
        {
            // React immediately on hurt, otherwise only 1/10 chance of execution
            if (rand.NextDouble() > 0.1f && (whenInEmotionState == null || IsInEmotionState(whenInEmotionState) != true)) return false;

            if (!PreconditionsSatisifed()) return false;
            if (lastSearchTotalMs + searchWaitMs > entity.World.ElapsedMilliseconds) return false;
            if (whenInEmotionState == null && rand.NextDouble() > 0.5f) return false;
            if (cooldownUntilMs > entity.World.ElapsedMilliseconds) return false;

            lastSearchTotalMs = entity.World.ElapsedMilliseconds;

            float range = sensingRange;
            targetEntity = partitionUtil.GetNearestEntity(entity.ServerPos.XYZ, range, (e) => IsTargetableEntity(e, range) && hasDirectContact(e, range, range/2f) && aimableDirection(e), EnumEntitySearchType.Creatures);

            return targetEntity != null && !inAbortRange;
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

            currentState = EnumTurretState.Idle;
            currentStateTime = 0;
            executing = true;
        }

        
        bool inFiringRange
        {
            get {
                var range = targetEntity.ServerPos.DistanceTo(entity.ServerPos);
                return range >= firingRangeMin && range <= firingRangeMax;
            }
        }
        bool inSensingRange => targetEntity.ServerPos.DistanceTo(entity.ServerPos) <= sensingRange;
        bool inAbortRange => targetEntity.ServerPos.DistanceTo(entity.ServerPos) <= abortRange;

        void updateState()
        {
            switch (currentState)
            {
                case EnumTurretState.Idle:
                    if (inFiringRange)
                    {
                        entity.StartAnimation("load");
                        currentState = EnumTurretState.TurretMode;
                        currentStateTime = 0;
                        return;
                    }

                    if (inSensingRange)
                    {
                        entity.StartAnimation("turret");
                        currentState = EnumTurretState.TurretMode;
                        currentStateTime = 0;
                        //System.Diagnostics.Debug.WriteLine("enter turret mode");
                    }
                    return;

                case EnumTurretState.TurretMode:
                    if (!isAnimDone("turret")) return;

                    if (inAbortRange)
                    {
                        abort();
                        return;
                    }

                    if (inFiringRange)
                    {
                        currentState = EnumTurretState.TurretModeLoad;
                        //System.Diagnostics.Debug.WriteLine("enter turret load mode");
                        entity.StopAnimation("turret");
                        entity.StartAnimation("load-fromturretpose");
                        currentStateTime = 0;
                        return;
                    }

                    if (currentStateTime > 5)
                    {
                        currentState = EnumTurretState.Stop;
                        //System.Diagnostics.Debug.WriteLine("enter turret stop mode");
                        entity.StopAnimation("turret");
                    }
                    return;

                case EnumTurretState.TurretModeLoad:
                    if (!isAnimDone("load")) return;

                    entity.StartAnimation("hold");
                    currentState = EnumTurretState.TurretModeHold;
                    //System.Diagnostics.Debug.WriteLine("enter turret hold mode");
                    currentStateTime = 0;
                    return;

                case EnumTurretState.TurretModeHold:
                    if (inFiringRange || inAbortRange)
                    {
                        if (currentStateTime > 0.5)
                        {
                            fireProjectile();
                            currentState = EnumTurretState.TurretModeFired;
                            //System.Diagnostics.Debug.WriteLine("enter turret fire mode");
                            
                            entity.StopAnimation("hold");
                            entity.StartAnimation("fire");
                        }
                        return;
                    }

                    if (currentStateTime > 2)
                    {
                        currentState = EnumTurretState.TurretModeUnload;
                        //System.Diagnostics.Debug.WriteLine("enter turret unload mode");
                        
                        entity.StopAnimation("hold");
                        entity.StartAnimation("unload");
                    }

                    return;

                case EnumTurretState.TurretModeUnload:
                    if (!isAnimDone("unload")) return;

                    //System.Diagnostics.Debug.WriteLine("enter turret stop mode");
                    currentState = EnumTurretState.Stop;
                    return;

                case EnumTurretState.TurretModeFired:
                    var range = sensingRange;
                    if (inAbortRange || !targetEntity.Alive || !targetablePlayerMode((targetEntity as EntityPlayer)?.Player) || !hasDirectContact(targetEntity, range, range / 2f))
                    {
                        abort();
                        return;
                    }

                    if (inSensingRange)
                    {
                        //System.Diagnostics.Debug.WriteLine("enter turret reload mode");
                        
                        currentState = EnumTurretState.TurretModeReload;
                        entity.StartAnimation("reload");
                        return;
                    }

                    return;

                case EnumTurretState.TurretModeReload:
                    if (!isAnimDone("reload")) return;

                    if (inAbortRange)
                    {
                        abort();
                        return;
                    }

                    //entity.StartAnimation("turret");
                    //System.Diagnostics.Debug.WriteLine("enter turret load mode");
                    
                    currentState = EnumTurretState.TurretModeLoad;
                    return;
            }
        }

        private void abort()
        {
            currentState = EnumTurretState.Stop;
            //System.Diagnostics.Debug.WriteLine("stop");
            entity.StopAnimation("hold");
            entity.StopAnimation("turret");

            var tm = entity.GetBehavior<EntityBehaviorTaskAI>().TaskManager;
            tm.GetTask<AiTaskStayInRange>().targetEntity = this.targetEntity;
            tm.ExecuteTask<AiTaskStayInRange>();
        }

        private bool isAnimDone(string anim)
        {
            var tstate = entity.AnimManager.GetAnimationState(anim);
            return !tstate.Running || tstate.AnimProgress >= 0.95;
        }

        private void fireProjectile()
        {
            var loc = projectileCode.Clone();

            if (projectileCode.Path.Contains('{'))
            {
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
            }

            EntityProperties type = entity.World.GetEntityType(loc);
            if (type == null)
            {
                throw new Exception("No such projectile exists - " + loc);
            }
            var entitypr = entity.World.ClassRegistry.CreateEntity(type) as EntityProjectile;
            entitypr.FiredBy = entity;
            entitypr.Damage = projectileDamage;
            entitypr.ProjectileStack = new ItemStack(entity.World.GetItem(new AssetLocation("stone-granite")));
            entitypr.NonCollectible = true;

            Vec3d pos = entity.ServerPos.XYZ.Add(0, entity.LocalEyePos.Y, 0);
            Vec3d targetPos = targetEntity.ServerPos.XYZ.Add(0, targetEntity.LocalEyePos.Y, 0) + targetEntity.ServerPos.Motion * 8;

            var dist = pos.SquareDistanceTo(targetPos) + entity.World.Rand.NextDouble();
            double distf = Math.Pow(dist, 0.1);
            Vec3d velocity = (targetPos - pos).Normalize() * GameMath.Clamp(distf - 1f, 0.1f, 1f);
            velocity.Y += (GameMath.Sqrt(dist) - 10) / 200.0;

            entitypr.ServerPos.SetPosWithDimension(
                entity.ServerPos.BehindCopy(0.21).XYZ.Add(0, entity.LocalEyePos.Y, 0)
            );

            entitypr.ServerPos.Motion.Set(velocity);
            entitypr.SetInitialRotation();
            entitypr.Pos.SetFrom(entitypr.ServerPos);
            entitypr.World = entity.World;
            entity.World.SpawnEntity(entitypr);
        }

        public override bool ContinueExecute(float dt)
        {
            currentStateTime += dt;
            updateState();

            float desiredYaw = getAimYaw(targetEntity);
            desiredYaw = GameMath.Clamp(desiredYaw, spawnAngleRad - maxTurnAngleRad, spawnAngleRad + maxTurnAngleRad);

            float yawDist = GameMath.AngleRadDistance(entity.ServerPos.Yaw, desiredYaw);
            entity.ServerPos.Yaw += GameMath.Clamp(yawDist, -curTurnRadPerSec * dt, curTurnRadPerSec * dt);
            entity.ServerPos.Yaw = entity.ServerPos.Yaw % GameMath.TWOPI;

            return currentState != EnumTurretState.Stop;
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

        public override void FinishExecute(bool cancelled)
        {
            base.FinishExecute(cancelled);

            entity.StopAnimation("turret");
            entity.StopAnimation("hold");
            executing = false;
        }
    }
}
