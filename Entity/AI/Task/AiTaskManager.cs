using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace Vintagestory.GameContent
{
    public class AiTaskManager
    {
        public static Dictionary<string, Type> TaskTypes = new Dictionary<string, Type>();
        public static Dictionary<Type, string> TaskCodes = new Dictionary<Type, string>();

        public static void RegisterTaskType(string task, Type type)
        {
            TaskTypes[task] = type;
            TaskCodes[type] = task;
        }

        static AiTaskManager()
        {
            RegisterTaskType("wander", typeof(AiTaskWander));
            RegisterTaskType("lookaround", typeof(AiTaskLookAround));
            RegisterTaskType("meleeattack", typeof(AiTaskMeleeAttack));
            RegisterTaskType("seekentity", typeof(AiTaskSeekEntity));
            RegisterTaskType("fleeplayer", typeof(AiTaskFleePlayer));
            RegisterTaskType("stayclosetoentity", typeof(AiTaskStayCloseToEntity));
            RegisterTaskType("getoutofwater", typeof(AiTaskGetOutOfWater));
            RegisterTaskType("idle", typeof(AiTaskIdle));
        }


        public AiTaskManager(Entity entity)
        {
            this.entity = entity;
        }

        Entity entity;
        List<IAiTask> Tasks = new List<IAiTask>();
        IAiTask[] ActiveTasksBySlot = new IAiTask[8];

        public void AddTask(IAiTask task)
        {
            Tasks.Add(task);
        }

        public void RemoveTask(IAiTask task)
        {
            Tasks.Remove(task);
        }

        public void OnGameTick(float dt)
        {
            foreach (IAiTask task in Tasks)
            {
                int slot = task.Slot;

                if ((ActiveTasksBySlot[slot] == null || task.Priority > ActiveTasksBySlot[slot].PriorityForCancel) && task.ShouldExecute())
                {
                    if (ActiveTasksBySlot[slot] != null)
                    {
                        ActiveTasksBySlot[slot].FinishExecute(true);
                    }

                    ActiveTasksBySlot[slot] = task;
                    task.StartExecute();
                }
            }

            
            for (int i = 0; i < ActiveTasksBySlot.Length; i++)
            {
                IAiTask task = ActiveTasksBySlot[i];
                if (task == null) continue;

                if (!task.ContinueExecute(dt))
                {
                    task.FinishExecute(false);
                    ActiveTasksBySlot[i] = null;
                }
            }


            if (entity.World.EntityDebugMode)
            {
                string tasks = "";
                int j = 0;
                for (int i = 0; i < ActiveTasksBySlot.Length; i++)
                {
                    IAiTask task = ActiveTasksBySlot[i];
                    if (task == null) continue;
                    if (j++ > 0) tasks += ", ";
                    tasks += TaskCodes[task.GetType()] + "("+task.Priority+")";
                }
                entity.DebugAttributes.SetString("AI Tasks", tasks.Length > 0 ? tasks : "-");
            }
        }

        internal void Notify(string key, object data)
        {
            for (int i = 0; i < Tasks.Count; i++)
            {
                IAiTask task = Tasks[i];

                if (task.Notify(key, data))
                {
                    int slot = Tasks[i].Slot;

                    if ((ActiveTasksBySlot[slot] == null || task.Priority > ActiveTasksBySlot[slot].PriorityForCancel))
                    {
                        if (ActiveTasksBySlot[slot] != null)
                        {
                            ActiveTasksBySlot[slot].FinishExecute(true);
                        }

                        ActiveTasksBySlot[slot] = task;
                        task.StartExecute();
                    }
                }
            }
        }

        internal void OnStateChanged(EnumEntityState beforeState)
        {
            foreach (IAiTask task in Tasks)
            {
                task.OnStateChanged(beforeState);
            }
        }
    }
}
