using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent
{
    public class EntityBehaviorPettable : EntityBehavior, IPettable
    {
        long lastPetTotalMs;
        float petDurationS;

        public EntityBehaviorPettable(Entity entity) : base(entity)
        {
        }

        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            base.Initialize(properties, attributes);
                
            if (entity.World.Side == EnumAppSide.Server)
            {
                int generation = entity.WatchedAttributes.GetInt("generation", 0);
                if (generation >= attributes["minGeneration"].AsInt(1))
                {
                    EntityBehaviorTaskAI taskAi = entity.GetBehavior<EntityBehaviorTaskAI>();
                    taskAi.TaskManager.OnShouldExecuteTask += TaskManager_OnShouldExecuteTask;
                }
            }
        }


        private bool TaskManager_OnShouldExecuteTask(IAiTask task)
        {
            if (petDurationS >= 0.6)
            {
                //Console.WriteLine("getting pet");
                return false;
            }

            return true;
        }


        public override void OnInteract(EntityAgent byEntity, ItemSlot itemslot, Vec3d hitPosition, EnumInteractMode mode, ref EnumHandling handled)
        {
            base.OnInteract(byEntity, itemslot, hitPosition, mode, ref handled);

            if (byEntity is EntityPlayer && byEntity.Controls.RightMouseDown && byEntity.RightHandItemSlot.Empty && byEntity.Pos.DistanceTo(entity.Pos) < 1.2)
            {
                //Console.WriteLine(entity.World.Side + " " + petDurationS);
                if (entity.World.ElapsedMilliseconds - lastPetTotalMs < 500)
                {
                    petDurationS += (entity.World.ElapsedMilliseconds - lastPetTotalMs) / 1000f;
                }
                else petDurationS = 0;

                lastPetTotalMs = entity.World.ElapsedMilliseconds;

                if (petDurationS >= 0.6 && entity.World.Side == EnumAppSide.Server)
                {
                    AiTaskManager tmgr = entity.GetBehavior<EntityBehaviorTaskAI>().TaskManager;
                    tmgr.StopTask(typeof(AiTaskWander));
                    tmgr.StopTask(typeof(AiTaskSeekEntity));
                    tmgr.StopTask(typeof(AiTaskGotoEntity));
                }

            }
            else
            {
                petDurationS = 0;
            }
        }

        public override void OnGameTick(float deltaTime)
        {
            if (entity.World.ElapsedMilliseconds - lastPetTotalMs > 400)
            {
                petDurationS = 0;
            }

            base.OnGameTick(deltaTime);
        }

        public override string PropertyName()
        {
            return "pettable";
        }

        public bool CanPet(Entity byEntity)
        {
            return true;
        }
    }
}
