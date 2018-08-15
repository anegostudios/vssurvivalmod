using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    class EntityBehaviorCollectEntities : EntityBehavior
    {
        int waitTicks = 0;
        int lastCollectedEntityIndex = 0;
        Vec3d tmp = new Vec3d();

        float itemsPerSecond = 23f;
        float unconsumedDeltaTime;
        
        public EntityBehaviorCollectEntities(Entity entity) : base(entity)
        {
        }
        

        public override void OnGameTick(float deltaTime)
        {
            // Only running for active entities
            if (entity.State != EnumEntityState.Active || !entity.Alive) return;

            if (entity.IsActivityRunning("invulnerable"))
            {
                waitTicks = 3;
                return;
            }
            if (waitTicks-- > 0) return;

            tmp.Set(entity.ServerPos.X, entity.ServerPos.Y + entity.CollisionBox.Y1 + entity.CollisionBox.Y2 / 2, entity.ServerPos.Z);
            IEntity[] entities = entity.World.GetEntitiesAround(tmp, 1.5f, 1.5f, entityMatcher);
            if (entities.Length == 0)
            {
                unconsumedDeltaTime = 0;
                entity.World.FrameProfiler.Mark("entity-collectentities");
                return;
            }

            
            deltaTime = System.Math.Min(1f, deltaTime + unconsumedDeltaTime);

            while ((deltaTime - 1/itemsPerSecond) > 0)
            {
                IEntity targetItem = null;
                int targetIndex = 0;

                for (; targetIndex < entities.Length; targetIndex++)
                {
                    if (entities[targetIndex] == null) continue;

                    if (targetIndex >= lastCollectedEntityIndex)
                    {
                        targetItem = entities[targetIndex];
                        break;
                    }
                }

                if (targetItem == null)
                {
                    targetItem = entities[0];
                    targetIndex = 0;
                }
                if (targetItem == null) return;

                if (!OnFoundCollectible(targetItem))
                {
                    lastCollectedEntityIndex = (lastCollectedEntityIndex + 1) % entities.Length;
                } else
                {
                    entities[targetIndex] = null;
                }

                deltaTime -= 1 / itemsPerSecond;
            }

            unconsumedDeltaTime = deltaTime;

            entity.World.FrameProfiler.Mark("entity-collectentities");
        }


        public virtual bool OnFoundCollectible(IEntity foundEntity)
        {
            ItemStack itemstack = foundEntity.OnCollected(this.entity);
            bool collected = false;

            if (itemstack != null && itemstack.StackSize > 0)
            {
                collected = entity.TryGiveItemStack(itemstack);
            }            
                
            if (itemstack != null && itemstack.StackSize <= 0)
            {
                foundEntity.Die(EnumDespawnReason.PickedUp);
            }

            if (collected)
            {
                entity.World.PlaySoundAt(new AssetLocation("sounds/player/collect"), entity);
                return true;
            }

            return false;
        }

        private bool entityMatcher(IEntity foundEntity)
        {
            return foundEntity.CanCollect(entity);
        }


        public override string PropertyName()
        {
            return "collectitems";
        }
    }
}
