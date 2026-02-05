using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

#nullable disable

namespace Vintagestory.GameContent
{
    public class ModSystemFishInstaFlee : ModSystem
    {
        public override bool ShouldLoad(EnumAppSide side) => side == EnumAppSide.Server;

        public override void StartServerSide(ICoreServerAPI api)
        {
            api.Event.HandInteract += Event_HandInteract;
        }

        private void Event_HandInteract(IServerPlayer player, EnumHandInteractNw enumHandInteract, float secondsPassed, ref EnumHandling handling)
        {
            if (player.CurrentEntitySelection?.Entity is EntityFish efish && (enumHandInteract == EnumHandInteractNw.StartHeldItemUse || enumHandInteract == EnumHandInteractNw.StepHeldItemUse))
            {
                if (!efish.Swimming) return;

                efish.GetBehavior<EntityBehaviorTaskAI>().TaskManager.ExecuteTask<AiTaskFleeEntity>();
                efish.Pos.Motion.X = Math.Sin(efish.Pos.Yaw)/2;
                efish.Pos.Motion.Z = Math.Cos(efish.Pos.Yaw)/2;
            }
        }
    }

    public class EntityFish : EntityAgent
    {
        static EntityFish()
        {
            AiTaskRegistry.Register<AiTaskFishMoveFast>("fishmovefast");
            AiTaskRegistry.Register<AiTaskFishOutOfWater>("fishoutofwater");
        }

        public EntityFish() { }

        AiTaskManager tm;

        public override void AfterInitialized(bool onFirstSpawn)
        {
            base.AfterInitialized(onFirstSpawn);

            if (Api.Side == EnumAppSide.Server)
            {
                tm = GetBehavior<EntityBehaviorTaskAI>().TaskManager;
                (tm.GetTask("seekbobber") as AiTaskSeekEntity).OnIsSuitableTarget = bobberBaitCheck;
            }
        }


        private bool bobberBaitCheck(Entity entity)
        {
            var ebobber = entity as EntityBobber;
            if (ebobber.BaitStack == null) return World.Rand.NextDouble() < 0.02; // Very uninteresting without bait

            return World.Rand.NextDouble() < 0.5;
        }

        public override void OnGameTick(float dt)
        {
            base.OnGameTick(dt);
        }

        public override void OnInteract(EntityAgent byEntity, ItemSlot slot, Vec3d hitPosition, EnumInteractMode mode)
        {
            if (mode == EnumInteractMode.Attack && Swimming)
            {
                if (World.Side == EnumAppSide.Server)
                {
                    tm.ExecuteTask<AiTaskFleeEntity>();
                }
                Pos.Motion.X = Math.Sin(Pos.Yaw) / 2;
                Pos.Motion.Z = Math.Cos(Pos.Yaw) / 2;
                return;
            }

            base.OnInteract(byEntity, slot, hitPosition, mode);
        }
    }
}
