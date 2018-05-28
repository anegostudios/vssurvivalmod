using System;
using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace Vintagestory.GameContent
{
    public class EntityBehaviorTaskAI : EntityBehavior
    {
        AiTaskManager taskManager;


        public EntityBehaviorTaskAI(Entity entity) : base(entity)
        {
            taskManager = new AiTaskManager(entity);
        }


        public override void Initialize(EntityType config, JsonObject aiconfig)
        {
            if (!(entity is EntityAgent))
            {
                entity.World.Logger.Error("The task ai currently only works on entities inheriting from. EntityLiving. Will ignore loading tasks for entity {0} ", config.Code);
                return;
            }

            JsonObject[] tasks = aiconfig["aitasks"]?.AsArray();
            if (tasks == null) return;

            foreach(JsonObject taskConfig in tasks) 
            {
                string taskCode = taskConfig["code"]?.AsString();
                Type taskType = null;
                if (!AiTaskManager.TaskTypes.TryGetValue(taskCode, out taskType))
                {
                    entity.World.Logger.Error("Task with code {0} for entity {1} does not exist. Ignoring.", taskCode, config.Code);
                    continue;
                }

                IAiTask task = (IAiTask)Activator.CreateInstance(taskType, (EntityAgent)entity);
                task.LoadConfig(taskConfig, aiconfig);

                taskManager.AddTask(task);
            }
        }


        public override void OnGameTick(float deltaTime)
        {
            // AI is only running for active entities
            if (entity.State != EnumEntityState.Active) return;

            taskManager.OnGameTick(deltaTime);
        }


        public override void OnStateChanged(EnumEntityState beforeState, ref EnumHandling handled)
        {
            taskManager.OnStateChanged(beforeState);
        }


        public override void Notify(string key, object data)
        {
            taskManager.Notify(key, data);
        }

        public override string PropertyName()
        {
            return "taskai";
        }
    }
}
