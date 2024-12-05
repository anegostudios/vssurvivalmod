using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Vintagestory.GameContent
{

    public class EntityErel : EntityAgent
    {
        public override bool CanSwivel => true;
        public override bool CanSwivelNow => true;
        public override bool StoreWithChunk => false;
        public override bool AllowOutsideLoadedRange => true;

        ILoadedSound aliveSound;
        ILoadedSound glideSound;
        AiTaskManager tmgr;

        Dictionary<string, int[]> attackCooldowns = new Dictionary<string, int[]>();

        public double LastAnnoyedTotalDays
        {
            get { return WatchedAttributes.GetDouble("lastannoyedtotaldays", -9999999); }
            set { WatchedAttributes.SetDouble("lastannoyedtotaldays", value); }
        }
        public bool Annoyed
        {
            get { return WatchedAttributes.GetBool("annoyed", false); }
            set { WatchedAttributes.SetBool("annoyed", value); }
        }

        static EntityErel()
        {
            AiTaskRegistry.Register("flycircle", typeof(AiTaskFlyCircle));
            AiTaskRegistry.Register("flycircleifentity", typeof(AiTaskFlyCircleIfEntity));
            AiTaskRegistry.Register("flycircletarget", typeof(AiTaskFlyCircleTarget));
            AiTaskRegistry.Register("flywander", typeof(AiTaskFlyWander));
            AiTaskRegistry.Register("flyswoopattack", typeof(AiTaskFlySwoopAttack));
            AiTaskRegistry.Register("flydiveattack", typeof(AiTaskFlyDiveAttack));
            AiTaskRegistry.Register("firefeathersattack", typeof(AiTaskFireFeathersAttack));
            AiTaskRegistry.Register("flyleave", typeof(AiTaskFlyLeave));
        }

        public EntityErel()
        {
            SimulationRange = 1024;
        }

        public override void Initialize(EntityProperties properties, ICoreAPI api, long InChunkIndex3d)
        {
            base.Initialize(properties, api, InChunkIndex3d);

            WatchedAttributes.SetBool("showHealthbar", true);

            devaLocationPresent = api.ModLoader.GetModSystem<ModSystemDevastationEffects>().DevaLocationPresent;
            devaLocationPast = api.ModLoader.GetModSystem<ModSystemDevastationEffects>().DevaLocationPast;

            if (api is ICoreClientAPI capi)
            {
                aliveSound = capi.World.LoadSound(new SoundParams() { DisposeOnFinish = false, Location = new AssetLocation("sounds/creature/erel/alive"), ShouldLoop = true, Range = 48 });
                aliveSound.Start();
                glideSound = capi.World.LoadSound(new SoundParams() { DisposeOnFinish = false, Location = new AssetLocation("sounds/creature/erel/glide"), ShouldLoop = true, Range = 24 });
            }

            ebh = GetBehavior<EntityBehaviorHealth>();

        }

        
        public override void AfterInitialized(bool onFirstSpawn)
        {
            base.AfterInitialized(onFirstSpawn);

            Api.ModLoader.GetModSystem<ModSystemDevastationEffects>().SetErelAnnoyed(Annoyed);

            if (World.Side == EnumAppSide.Server)
            {
                tmgr = GetBehavior<EntityBehaviorTaskAI>().TaskManager;
                tmgr.OnShouldExecuteTask += Tmgr_OnShouldExecuteTask;

                if (Annoyed) tmgr.GetTask<AiTaskFlyLeave>().AllowExecute = true;

                attackCooldowns["swoop"] = new int[] { tmgr.GetTask<AiTaskFlySwoopAttack>().Mincooldown, tmgr.GetTask<AiTaskFlySwoopAttack>().Maxcooldown };
                attackCooldowns["dive"] = new int[] { tmgr.GetTask<AiTaskFlyDiveAttack>().Mincooldown, tmgr.GetTask<AiTaskFlyDiveAttack>().Maxcooldown };
                attackCooldowns["feathers"] = new int[] { tmgr.GetTask<AiTaskFireFeathersAttack>().Mincooldown, tmgr.GetTask<AiTaskFireFeathersAttack>().Maxcooldown };
            }

            updateAnnoyedState();
        }

        protected bool outSideDevaRange()
        {
            return distanceToTower() > 600;
        }
        protected bool inTowerRange() => distanceToTower() < 100;

        public double distanceToTower()
        {
            var msdevaeff = Api.ModLoader.GetModSystem<ModSystemDevastationEffects>();
            var loc = ServerPos.Dimension == 0 ? msdevaeff.DevaLocationPresent : msdevaeff.DevaLocationPast;
            return ServerPos.DistanceTo(loc);
        }

        // Don't attack or track the player if outside devastation area
        private bool Tmgr_OnShouldExecuteTask(IAiTask t)
        {
            if (t is AiTaskFlySwoopAttack || t is AiTaskFlyDiveAttack || t is AiTaskFireFeathersAttack || t is AiTaskFlyCircleTarget)
            {                
                if (outSideDevaRange())
                {
                    return false;
                }
            }

            return true;
        }

        EntityBehaviorHealth ebh;
        public override void OnEntityDespawn(EntityDespawnData despawn)
        {
            base.OnEntityDespawn(despawn);
            aliveSound?.Dispose();
        }

        Vec3d devaLocationPresent, devaLocationPast;
        float nextFlyIdleSec = -1;
        float nextFlapCruiseSec = -1;
        float prevYaw = 0;
        float annoyCheckAccum;
        public override void OnGameTick(float dt)
        {
            base.OnGameTick(dt);

            if (Api.Side == EnumAppSide.Server)
            {
                doOccasionalFlapping(dt);

                annoyCheckAccum += dt;
                if (annoyCheckAccum > 1)
                {
                    annoyCheckAccum = 0;
                    updateAnnoyedState();
                    toggleBossFightModeNearTower();
                    stopAttacksOutsideDevaRange();
                }

            } else
            {
                aliveSound.SetPosition((float)Pos.X, (float)Pos.Y, (float)Pos.Z);
                glideSound.SetPosition((float)Pos.X, (float)Pos.Y, (float)Pos.Z);

                if (AnimManager.IsAnimationActive("fly-flapactive", "fly-flapactive-fast") && glideSound.IsPlaying)
                {
                    glideSound.Stop();
                } else
                {
                    if ((AnimManager.IsAnimationActive("fly-idle", "fly-flapcruise") || AnimManager.ActiveAnimationsByAnimCode.Count == 0) && !glideSound.IsPlaying)
                    {
                        glideSound.Start();
                    }
                }
            }

            if (AnimManager.IsAnimationActive("dive", "slam")) return;
            double speed = ServerPos.Motion.Length();
            if (speed > 0.01)
            {
                ServerPos.Roll = (float)Math.Asin(GameMath.Clamp(-ServerPos.Motion.Y / speed, -1, 1));
            }
        }



        private void stopAttacksOutsideDevaRange()
        {
            if (outSideDevaRange())
            {
                foreach (var t in tmgr.ActiveTasksBySlot)
                {
                    if (t is AiTaskFlySwoopAttack || t is AiTaskFlyDiveAttack || t is AiTaskFireFeathersAttack || t is AiTaskFlyCircleTarget)
                    {
                        tmgr.StopTask(t.GetType());
                    }
                }
            }
        }

        bool wasAtBossFightArea;
        private void toggleBossFightModeNearTower()
        {
            var msdevaeff = Api.ModLoader.GetModSystem<ModSystemDevastationEffects>();
            var loc = ServerPos.Dimension == 0 ? msdevaeff.DevaLocationPresent : msdevaeff.DevaLocationPast;
            WatchedAttributes.SetBool("showHealthbar", ServerPos.Y > loc.Y + 70);



            var ctask = tmgr.GetTask<AiTaskFlyCircleIfEntity>();
            var nearTownerEntity = ctask.getEntity();
            bool atBossFightArea = nearTownerEntity != null && ServerPos.XYZ.HorizontalSquareDistanceTo(ctask.CenterPos) < 70*70;

            var swoopAtta = tmgr.GetTask<AiTaskFlySwoopAttack>();
            var diveAtta = tmgr.GetTask<AiTaskFlyDiveAttack>();
            var feathersAtta = tmgr.GetTask<AiTaskFireFeathersAttack>();

            feathersAtta.Enabled = atBossFightArea;
            diveAtta.Enabled = atBossFightArea;

            if (wasAtBossFightArea && !atBossFightArea)
            {
                swoopAtta.Mincooldown = attackCooldowns["swoop"][0];
                swoopAtta.Maxcooldown = attackCooldowns["swoop"][1];
                diveAtta.Mincooldown = attackCooldowns["dive"][0];
                diveAtta.Maxcooldown = attackCooldowns["dive"][1];
                feathersAtta.Mincooldown = attackCooldowns["feathers"][0];
                feathersAtta.Maxcooldown = attackCooldowns["feathers"][1];
            }
            if (!wasAtBossFightArea && atBossFightArea)
            {
                swoopAtta.Mincooldown = attackCooldowns["swoop"][0] / 2;
                swoopAtta.Maxcooldown = attackCooldowns["swoop"][1] / 2;
                diveAtta.Mincooldown = attackCooldowns["dive"][0] / 2;
                diveAtta.Maxcooldown = attackCooldowns["dive"][1] / 2;
                feathersAtta.Mincooldown = attackCooldowns["feathers"][0] / 2;
                feathersAtta.Maxcooldown = attackCooldowns["feathers"][1] / 2;
            }

            wasAtBossFightArea = atBossFightArea;
        }

        private void updateAnnoyedState()
        {
            if (Api.Side == EnumAppSide.Client) return;

            if (!Annoyed)
            {
                if (ebh.Health / ebh.MaxHealth < 0.6)
                {
                    Api.World.PlaySoundAt("sounds/creature/erel/annoyed", this, null, false, 1024, 1);
                    AnimManager.StartAnimation("defeat");
                    LastAnnoyedTotalDays = Api.World.Calendar.TotalDays;
                    Annoyed = true;
                    tmgr.GetTask<AiTaskFlyLeave>().AllowExecute = true;

                    Api.ModLoader.GetModSystem<ModSystemDevastationEffects>().SetErelAnnoyed(true);
                }
            }
            else
            {
                if (Api.World.Calendar.TotalDays - LastAnnoyedTotalDays > 14)
                {
                    Annoyed = false;
                    ebh.Health = ebh.MaxHealth;
                    tmgr.GetTask<AiTaskFlyLeave>().AllowExecute = false;
                    Api.ModLoader.GetModSystem<ModSystemDevastationEffects>().SetErelAnnoyed(false);
                }
            }
        }

        private void doOccasionalFlapping(float dt)
        {
            float turnSpeed = Math.Abs(GameMath.AngleRadDistance(prevYaw, ServerPos.Yaw));
            var flyspeed = ServerPos.Motion.Length();

            if (AnimManager.IsAnimationActive("dive", "slam")) return;
            
            if ((ServerPos.Motion.Y >= 0.03 || turnSpeed > 0.05 || flyspeed < 0.15) && (AnimManager.IsAnimationActive("fly-idle", "fly-flapcruise") || AnimManager.ActiveAnimationsByAnimCode.Count == 0))
            {
                AnimManager.StopAnimation("fly-flapcruise");
                AnimManager.StopAnimation("fly-idle");
                AnimManager.StartAnimation("fly-flapactive-fast");
                return;
            }

            if (ServerPos.Motion.Y <= 0.01 && turnSpeed < 0.03 && flyspeed >= 0.25 && AnimManager.IsAnimationActive("fly-flapactive", "fly-flapactive-fast"))
            {
                AnimManager.StopAnimation("fly-flapactive");
                AnimManager.StopAnimation("fly-flapactive-fast");
                AnimManager.StartAnimation("fly-idle");
            }

            prevYaw = ServerPos.Yaw;

            if (nextFlyIdleSec > 0)
            {
                nextFlyIdleSec -= dt;
                if (nextFlyIdleSec < 0)
                {
                    AnimManager.StopAnimation("fly-flapcruise");
                    AnimManager.StartAnimation("fly-idle");
                    return;
                }
            }

            if (nextFlapCruiseSec < 0)
            {
                nextFlapCruiseSec = (float)Api.World.Rand.NextDouble() * 15 + 5;
                return;
            }

            if (AnimManager.IsAnimationActive("fly-idle"))
            {
                nextFlapCruiseSec -= dt;
                if (nextFlapCruiseSec < 0)
                {
                    AnimManager.StopAnimation("fly-idle");
                    AnimManager.StartAnimation("fly-flapcruise");
                    nextFlyIdleSec = (float)(Api.World.Rand.NextDouble() * 4 + 1) * 130/30.0f;
                }
            }
        }


        public override bool ReceiveDamage(DamageSource damageSource, float damage)
        {
            if (!inTowerRange())
            {
                damage /= 2;
            }

            if (World.Side == EnumAppSide.Server)
            {
                // 1/(1+sqrt((x-1)/4))
                // https://www.toolfk.com/online-plotter-frame#W3sidHlwZSI6MCwiZXEiOiIxLygxK3NxcnQoKHgtMSkvMykpIiwiY29sb3IiOiIjMDAwMDAwIn0seyJ0eXBlIjoxMDAwLCJ3aW5kb3ciOlsiMCIsIjEwIiwiMCIsIjEuMSJdLCJzaXplIjpbNjQ3LDM5N119XQ--
                // Scale damage based on how many are fighting the erel
                int x = nearbyPlayerCount();
                damage *= 1f / (1 + (float)Math.Sqrt((x - 1) / 4));
            }

            // Cannot die
            if (ebh != null && ebh.Health - damage < 0) return false;

            return base.ReceiveDamage(damageSource, damage);
        }

        private int nearbyPlayerCount()
        {
            int cnt = 0;
            foreach (IServerPlayer plr in World.AllOnlinePlayers)
            {
                if (plr.ConnectionState != EnumClientState.Playing) continue;
                if (plr.WorldData.CurrentGameMode != EnumGameMode.Survival) continue;

                double dx = plr.Entity.Pos.X - Pos.X;
                double dz = plr.Entity.Pos.Z - Pos.Z;

                if (Math.Abs(dx) <= 7 && Math.Abs(dz) <= 7)
                {
                    cnt++;
                }
            }

            return cnt;
        }
    }
}
