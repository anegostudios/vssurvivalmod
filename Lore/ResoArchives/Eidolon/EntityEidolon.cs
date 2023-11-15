using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;

namespace Vintagestory.GameContent
{
    public class EidolonAnimManager : AnimationManager
    {
        public override void OnReceivedServerAnimations(int[] activeAnimations, int activeAnimationsCount, float[] activeAnimationSpeeds)
        {
            base.OnReceivedServerAnimations(activeAnimations, activeAnimationsCount, activeAnimationSpeeds);

            if (ActiveAnimationsByAnimCode.ContainsKey("inactive"))
            {
                StopAnimation("idle");
            }
        }
    }

    public class EntityEidolon : EntityAgent
    {
        ILoadedSound activeSound;

        AiTaskEidolonSlam slamTask;
        EntityBehaviorHealth bhHealth;

        static EntityEidolon()
        {
            AiTaskRegistry.Register("eidolonslam", typeof(AiTaskEidolonSlam));
            AiTaskRegistry.Register("eidolonmeleeattack", typeof(AiTaskEidolonMeleeAttack));
        }

        public EntityEidolon()
        {
            AnimManager = new EidolonAnimManager();
        }

        public override void Initialize(EntityProperties properties, ICoreAPI api, long InChunkIndex3d)
        {
            base.Initialize(properties, api, InChunkIndex3d);

            Notify("starttask", "inactive");
            WatchedAttributes.SetBool("showHealthbar", false);
            
            AllowDespawn = false;

            if (api.Side == EnumAppSide.Server)
            {
                slamTask = GetBehavior<EntityBehaviorTaskAI>().TaskManager.GetTask<AiTaskEidolonSlam>();
                bhHealth = GetBehavior<EntityBehaviorHealth>();
            }
        }

        bool IsAsleep
        {
            get
            {
                var tm = GetBehavior<EntityBehaviorTaskAI>()?.TaskManager;
                if (tm == null) return false;
                foreach (var val in tm.ActiveTasksBySlot)
                {
                    if (val?.Id == "inactive") return true;
                }
                return false;
            }
        }

        public override void OnGameTick(float dt)
        {
            if (Api is ICoreClientAPI capi)
            {
                bool nowActive = Alive && !this.AnimManager.IsAnimationActive("inactive");
                bool wasActive = activeSound != null && activeSound.IsPlaying;

                if (nowActive && !wasActive)
                {
                    if (activeSound == null)
                    {
                        activeSound = capi.World.LoadSound(new SoundParams()
                        {
                            Location = new AssetLocation("sounds/creature/eidolon/awake"),
                            DisposeOnFinish=false,
                            ShouldLoop=true,
                            Position = this.Pos.XYZ.ToVec3f(),
                            SoundType = EnumSoundType.Entity,
                            Volume = 0,
                            Range = 16
                        });
                    }

                    activeSound.Start();
                    activeSound.FadeTo(0.1f, 0.5f, (s)=> { });
                }

                if (!nowActive && wasActive)
                {
                    activeSound.FadeOutAndStop(2.5f);
                }

                GetBehavior<EntityBehaviorBoss>().ShouldPlayTrack = nowActive && capi.World.Player.Entity.Pos.DistanceTo(this.Pos) < 15;
            } else
            {

                if (slamTask.creatureSpawnChance <= 0 && bhHealth.Health / bhHealth.MaxHealth < 0.5)
                {
                    slamTask.creatureSpawnChance = 0.3f;
                } else
                {
                    slamTask.creatureSpawnChance = 0f;
                }


            }

            base.OnGameTick(dt);
        }

        public override void OnEntitySpawn()
        {
            base.OnEntitySpawn();
        }

        public override void OnEntityDespawn(EntityDespawnData despawn)
        {
            base.OnEntityDespawn(despawn);

            activeSound?.Stop();
            activeSound?.Dispose();
        }

        public override bool ReceiveDamage(DamageSource damageSource, float damage)
        {
            // Invulnerable to his own boulders
            if (damageSource.SourceEntity?.Code.Path.StartsWith("thrownboulder") == true)
            {
                return false;
            }
            // Invulnerable when asleep
            if (IsAsleep && damageSource.Type != EnumDamageType.Heal) return false;


            if (World.Side == EnumAppSide.Server)
            {
                // 1/(1+sqrt((x-1)/2))
                // https://www.toolfk.com/online-plotter-frame#W3sidHlwZSI6MCwiZXEiOiIxLygxK3NxcnQoKHgtMSkvMikpIiwiY29sb3IiOiIjMDAwMDAwIn0seyJ0eXBlIjoxMDAwLCJ3aW5kb3ciOlsiMCIsIjEwIiwiMCIsIjEuMSJdLCJzaXplIjpbNjQ4LDM5OF19XQ--
                // Scale damage based on how many are fighting the eidolon
                int x = nearbyPlayerCount();
                damage *= 1f / (1 + (float)Math.Sqrt((x - 1) / 2));
            }

            damageSource.KnockbackStrength = 0;
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
