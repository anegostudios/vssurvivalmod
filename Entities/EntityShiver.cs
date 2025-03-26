using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class EntityShiver : EntityAgent, IMeleeAttackListener
    {
        int mouthType;
        string mouthOpen;
        string mouthIdle;
        string mouthClose;
        string mouthAttack;
        AiTaskManager aiTaskManager;

        bool strokeActive;

        public override bool AdjustCollisionBoxToAnimation => base.AdjustCollisionBoxToAnimation || AnimManager.IsAnimationActive("stroke-start", "stroke-idle", "stroke-end");

        public override void AfterInitialized(bool onFirstSpawn)
        {
            base.AfterInitialized(onFirstSpawn);

            this.hitAndRunChance = Properties.Attributes["hitAndRunChance"].AsFloat(0);


            if (WatchedAttributes.HasAttribute("mouthType")) {
                mouthType = WatchedAttributes.GetInt("mouthType");
            } else
            {
                WatchedAttributes.SetInt("mouthType", mouthType = Api.World.Rand.Next(3));
            }

            mouthOpen = "mouth-open" + (mouthType + 1);
            mouthIdle = "mouth-idle" + (mouthType + 1);
            mouthClose = "mouth-close" + (mouthType + 1);
            mouthAttack = "mouth-attack" + (mouthType + 1);

            if (Api.Side == EnumAppSide.Server)
            {
                aiTaskManager = GetBehavior<EntityBehaviorTaskAI>().TaskManager;
                aiTaskManager.OnTaskStarted += TaskManager_OnTaskStarted;
                aiTaskManager.OnTaskStopped += TaskManager_OnTaskStopped;
                aiTaskManager.OnShouldExecuteTask += AiTaskManager_OnShouldExecuteTask;
            }
        }

        private bool AiTaskManager_OnShouldExecuteTask(IAiTask t)
        {
            return !strokeActive;
        }

        public override void OnGameTick(float dt)
        {
            base.OnGameTick(dt);

            if (!Alive && Api.Side == EnumAppSide.Server && AnimManager.IsAnimationActive("stroke-start", "stroke-idle", "stroke-end", "despair"))
            {
                AnimManager.StopAnimation("stroke-start");
                AnimManager.StopAnimation("stroke-idle");
                AnimManager.StopAnimation("stroke-end");
            }

            if (Alive && Api.Side == EnumAppSide.Server && Api.World.Rand.NextDouble() < 0.0008 && !AnimManager.IsAnimationActive("stroke-start", "stroke-idle", "stroke-end", "despair"))
            {
                strokeActive = true;
                aiTaskManager.StopTasks();
                this.AnimManager.StartAnimation("stroke-start");
                World.PlaySoundAt(new AssetLocation("sounds/creature/shiver/shock"), this, null, true, 16);

                Api.Event.RegisterCallback((dt) => {
                    this.AnimManager.StartAnimation("stroke-idle");
                }, (int)(20 * 1000 / 30f));

                Api.Event.RegisterCallback((dt) => {
                    
                    AnimManager.StopAnimation("stroke-idle");
                    AnimManager.StartAnimation("stroke-end");
                    Api.Event.RegisterCallback((dt) => strokeActive = false, (int)(36*1000/30f));
                }, (int)(Api.World.Rand.NextDouble() * 3 + 3)*1000);
            }
        }

        private void TaskManager_OnTaskStopped(IAiTask task)
        {
            if (task is AiTaskSeekEntity)
            {
                Api.Event.UnregisterCallback(callbackid);
                callbackid = 0;
                this.AnimManager.StopAnimation(mouthOpen);
                this.AnimManager.StopAnimation(mouthIdle);
                this.AnimManager.StopAnimation(mouthClose);
            }
            if (task is AiTaskMeleeAttack)
            {
                this.AnimManager.StartAnimation(mouthAttack);
            }
        }

        long callbackid;

        private void TaskManager_OnTaskStarted(IAiTask task)
        {
            if (task is AiTaskSeekEntity)
            {
                this.AnimManager.StartAnimation(mouthOpen);
                callbackid = Api.Event.RegisterCallback((dt) => AnimManager.StartAnimation(mouthIdle), (int)(51 * 1000/30f));
            }
        }

        public override void Die(EnumDespawnReason reason = EnumDespawnReason.Death, DamageSource damageSourceForDeath = null)
        {
            base.Die(reason, damageSourceForDeath);

            this.AnimManager.StopAnimation(mouthOpen);
            this.AnimManager.StopAnimation(mouthIdle);
            this.AnimManager.StopAnimation(mouthClose);
        }

        protected float hitAndRunChance = 0;
        public void DidAttack(Entity targetEntity)
        {
            if (!targetEntity.Alive) return;

            if (World.Rand.NextDouble() < hitAndRunChance)
            {
                var bhEmo = GetBehavior<EntityBehaviorEmotionStates>();
                bhEmo?.TryTriggerState("fleeondamage", 0, targetEntity.EntityId);
            } else
            {
                Api.Event.RegisterCallback((dt) =>
                {
                    var tmpPos = targetEntity.ServerPos.Copy();
                    tmpPos.Yaw -= GameMath.PIHALF * (1 - 2 * Api.World.Rand.Next(2));
                    var targetPos = tmpPos.AheadCopy(4f).XYZ;

                    double dx = (targetPos.X + targetEntity.ServerPos.Motion.X * 80 - ServerPos.X) / 30;
                    double dz = (targetPos.Z + targetEntity.ServerPos.Motion.Z * 80 - ServerPos.Z) / 30;
                    ServerPos.Motion.Add(
                        dx,
                        GameMath.Max(0.13, (targetEntity.ServerPos.Y - ServerPos.Y) / 30),
                        dz
                    );

                    float yaw = (float)Math.Atan2(dx, dz);
                    ServerPos.Yaw = yaw;
                }, 500);
            }
        }
    }
}
