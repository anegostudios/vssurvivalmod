using Newtonsoft.Json;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.Essentials;

#nullable disable

namespace Vintagestory.GameContent
{

    public class EntityActivitySystem
    {
        public List<IEntityActivity> AvailableActivities = new List<IEntityActivity>();
        /// <summary>
        /// Topmost is currently executing, the others are paused
        /// </summary>
        public Dictionary<int, IEntityActivity> ActiveActivitiesBySlot = new Dictionary<int, IEntityActivity>();

        public string Code { get; set; }

        public bool Debug { get; set; } = ActivityModSystem.Debug;


        public PathTraverserBase linepathTraverser;
        public WaypointsTraverser wppathTraverser;
        public EntityAgent Entity;
        float accum;

        private BlockPos activityOffset;
        public BlockPos ActivityOffset
        {
            get
            {
                if (activityOffset == null)
                {
                    activityOffset = Entity.WatchedAttributes.GetBlockPos("importOffset", new BlockPos(Entity.Pos.Dimension));
                }

                return activityOffset;
            }
            set
            {
                activityOffset = value;
                Entity.WatchedAttributes.SetBlockPos("importOffset", activityOffset);
            }
        }

        public EntityActivitySystem(EntityAgent entity)
        {
            Entity = entity;
        }

        public bool StartActivity(string code, float priority=9999f, int slot=-1)
        {
            int index = AvailableActivities.IndexOf(item => item.Code == code);
            if (index < 0) return false;

            var activity = AvailableActivities[index];
            if (slot < 0) slot = activity.Slot;
            if (priority < 0) priority = (float)activity.Priority;

            if (ActiveActivitiesBySlot.TryGetValue(activity.Slot, out var activeAct))
            {
                if (activeAct.Priority > priority) return false;
                activeAct.Cancel();
            }

            ActiveActivitiesBySlot[activity.Slot] = activity;
            activity.Priority = priority;
            activity.Start();

            return true;
        }

        /// <summary>
        /// Abort all active activities
        /// </summary>
        /// <returns></returns>
        public bool CancelAll()
        {
            bool stopped = false;
            foreach (var val in ActiveActivitiesBySlot.Values)
            {
                if (val == null) continue;
                val.Cancel();
                stopped = true;
            }
            return stopped;
        }

        bool pauseAutoSelection=false;
        public void PauseAutoSelection(bool paused)
        {
            pauseAutoSelection = paused;
        }


        public void Pause(EnumInteruptionType interuptionType)
        {
            foreach (var val in ActiveActivitiesBySlot.Values)
            {
                val?.Pause(interuptionType);
            }
        }

        public void Resume()
        {
            foreach (var val in ActiveActivitiesBySlot.Values)
            {
                val?.Resume();
            }
        }

        bool clearDelay = false;
        public void ClearNextActionDelay()
        {
            clearDelay = true;
        }

        public void OnTick(float dt)
        {
            linepathTraverser.OnGameTick(dt);
            wppathTraverser.OnGameTick(dt);

            accum += dt;
            if (accum < 0.25 && !clearDelay) return;
            clearDelay = false;

            foreach (var key in ActiveActivitiesBySlot.Keys)
            {
                var activity = ActiveActivitiesBySlot[key];
                if (activity == null) continue;

                if (activity.Finished)
                {
                    activity.Finish();
                    Entity.Attributes.SetString("lastActivity", activity.Code);
                    if (Debug) Entity.World.Logger.Debug("ActivitySystem entity {0} activity {1} has finished", Entity.EntityId, activity.Name);
                    ActiveActivitiesBySlot.Remove(key);
                    continue;
                }

                activity.OnTick(accum);

                Entity.World.FrameProfiler.Mark("behavior-activitydriven-tick-", activity.Code);
            }

            accum = 0;

            if (!pauseAutoSelection)
            {
                foreach (var activity in AvailableActivities)
                {
                    int slot = activity.Slot;
                    if (ActiveActivitiesBySlot.TryGetValue(slot, out var activeActivity) && activeActivity != null)
                    {
                        if (activeActivity.Priority >= activity.Priority) continue;
                    }

                    bool execute = activity.ConditionsOp == EnumConditionLogicOp.AND;

                    for (int i = 0; i < activity.Conditions.Length && (execute || activity.ConditionsOp == EnumConditionLogicOp.OR); i++)
                    {
                        var cond = activity.Conditions[i];
                        var ok = cond.ConditionSatisfied(Entity);
                        if (cond.Invert) ok = !ok;

                        if (activity.ConditionsOp == EnumConditionLogicOp.OR)
                        {
                            if (Debug && ok) Entity.World.Logger.Debug("ActivitySystem entity {0} activity condition {1} is satisfied, will execute {2}", Entity.EntityId, activity.Conditions[i].Type, activity.Name);
                            execute |= ok;
                        }
                        else
                        {
                            execute &= ok;
                        }

                    }

                    if (execute)
                    {
                        ActiveActivitiesBySlot.TryGetValue(slot, out var act);
                        act?.Cancel();

                        ActiveActivitiesBySlot[slot] = activity;
                        activity?.Start();
                    }
                }
            }


            if (Entity.World.EntityDebugMode)
            {
                StringBuilder sb = new StringBuilder();
                foreach (var val in ActiveActivitiesBySlot)
                {
                    sb.Append(val.Key + ": " + val.Value.Name + "/" + val.Value.CurrentAction?.Type);
                }
                Entity.DebugAttributes.SetString("activities", sb.ToString());
            }
        }

        public bool Load(AssetLocation activityCollectionPath)
        {
            linepathTraverser = new StraightLineTraverser(Entity);
            wppathTraverser = new WaypointsTraverser(Entity);

            AvailableActivities.Clear();
            ActiveActivitiesBySlot.Clear();

            if (activityCollectionPath == null)
            {
                //Entity.World.Logger.Error("Unable to load activity system. No activityCollectionPath defined");
                return false;
            }

            var file = Entity.Api.Assets.TryGet(activityCollectionPath.WithPathPrefixOnce("config/activitycollections/").WithPathAppendixOnce(".json"));
            if (file == null)
            {
                Entity.World.Logger.Error("Unable to load activity file " + activityCollectionPath + " not such file found");
                return false;
            }

            JsonSerializerSettings settings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All };
            var coll = file.ToObject<EntityActivityCollection>(settings);

            AvailableActivities.AddRange(coll.Activities);

            coll.OnLoaded(this);
            return true;
        }

        public void StoreState(TreeAttribute attributes, bool forClient)
        {
            if (forClient) return;
            storeStateActivities(attributes, "executingActions", ActiveActivitiesBySlot.Values);
        }

        public void LoadState(TreeAttribute attributes, bool forClient)
        {
            if (forClient) return;

            ActiveActivitiesBySlot.Clear();
            foreach (var val in loadStateActivities(attributes, "executingActions"))
            {
                ActiveActivitiesBySlot[val.Slot] = val;
            }
        }


        private void storeStateActivities(TreeAttribute attributes, string key, IEnumerable<IEntityActivity> activities)
        {
            ITreeAttribute ctree = new TreeAttribute();
            attributes[key] = ctree;

            int i = 0;
            foreach (var val in activities)
            {
                ITreeAttribute attr = new TreeAttribute();
                val.StoreState(attr);
                ctree["activitiy" + i++] = attr;
            }
        }

        private IEnumerable<IEntityActivity> loadStateActivities(TreeAttribute attributes, string key)
        {
            List<IEntityActivity> activities = new List<IEntityActivity>();

            ITreeAttribute ctree = attributes.GetTreeAttribute(key);
            if (ctree == null) return activities;

            int i = 0;
            while (i < 200)
            {
                var tree = ctree.GetTreeAttribute("activity" + i++);
                if (tree == null) break;

                EntityActivity activity = new EntityActivity();
                activity.LoadState(tree);

                activities.Add(activity);
            }

            return activities;
        }

    }

}
