using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Vintagestory.GameContent
{
    public sealed class ModSystemFishInstaFlee : ModSystem
    {
        public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Server;

        public override void StartServerSide(ICoreServerAPI api)
        {
            api.Event.HandInteract += event_HandInteract;
        }

        private void event_HandInteract(IServerPlayer player, EnumHandInteractNw enumHandInteract, float secondsPassed, ref EnumHandling handling)
        {
            if (
                player.CurrentEntitySelection?.Entity is not EntityFish fishEntity ||
                !(enumHandInteract == EnumHandInteractNw.StartHeldItemUse || enumHandInteract == EnumHandInteractNw.StepHeldItemUse) ||
                !fishEntity.Swimming
                )
            {
                return;
            }

            fishEntity.TryFleeAway();
        }
    }

    public class EntityFish : EntityAgent
    {
        static EntityFish()
        {
            AiTaskRegistry.Register<AiTaskFishMoveFast>("fishmovefast");
            AiTaskRegistry.Register<AiTaskFishOutOfWater>("fishoutofwater");
        }

        protected AiTaskManager? taskManager;

        public double NoBaitBobberSeekChance { get; set; } = 0.02; // Very uninteresting without bait
        public double BaitBobberSeekChance { get; set; } = 0.5;
        public double FleeAwaySpeed { get; set; } = 0.5;


        public override void AfterInitialized(bool onFirstSpawn)
        {
            base.AfterInitialized(onFirstSpawn);

            if (Api.Side == EnumAppSide.Server)
            {
                taskManager = GetBehavior<EntityBehaviorTaskAI>()?.TaskManager;

                if (taskManager?.GetTask("seekbobber") is AiTaskSeekEntity seekBobberTask)
                {
                    seekBobberTask.OnIsSuitableTarget = bobberBaitCheck;
                }
            }
        }

        public override void OnInteract(EntityAgent byEntity, ItemSlot slot, Vec3d hitPosition, EnumInteractMode mode)
        {
            if (mode != EnumInteractMode.Attack || !Swimming)
            {
                base.OnInteract(byEntity, slot, hitPosition, mode);
                return;
            }

            if (World.Side == EnumAppSide.Server)
            {
                TryFleeAway();
            }
        }

        public virtual void BoltForward(double speed)
        {
            Pos.Motion.X = Math.Sin(Pos.Yaw) * speed;
            Pos.Motion.Z = Math.Cos(Pos.Yaw) * speed;
        }

        public virtual bool TryFleeAway()
        {
            if (taskManager == null)
            {
                return false;
            }

            taskManager.ExecuteTask<AiTaskFleeEntity>();
            if (taskManager.IsTaskActive<AiTaskFleeEntity>())
            {
                BoltForward(FleeAwaySpeed);
                return true;
            }

            return false;
        }


        protected virtual bool bobberBaitCheck(Entity entity)
        {
            EntityBobber? bobberEntity = entity as EntityBobber;
            if (bobberEntity?.BaitStack == null)
            {
                return World.Rand.NextDouble() < NoBaitBobberSeekChance;
            }

            return World.Rand.NextDouble() < BaitBobberSeekChance;
        }
    }
}
