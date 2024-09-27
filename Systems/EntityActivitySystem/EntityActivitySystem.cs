using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;
using Vintagestory.Essentials;

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

        public bool Debug { get; set; } = true;


        public PathTraverserBase linepathTraverser;
        public PathTraverserBase wppathTraverser;
        public EntityAgent Entity;
        float accum;

        public EntityActivitySystem(EntityAgent entity)
        {
            Entity = entity;
        }

        public bool StartActivity(string name)
        {
            var index = AvailableActivities.IndexOf<IEntityActivity>(item => item.Name == name);
            if (index < 0) return false;

            var activity = AvailableActivities[index];

            if (ActiveActivitiesBySlot.TryGetValue(activity.Slot, out var activeAct))
            {
                activeAct.Cancel();
            }

            ActiveActivitiesBySlot[activity.Slot] = activity;
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

        public void OnTick(float dt)
        {
            linepathTraverser.OnGameTick(dt);
            wppathTraverser.OnGameTick(dt);

            accum += dt;
            if (accum < 0.25) return;
            accum = 0;

            foreach (var key in ActiveActivitiesBySlot.Keys)
            {
                var activity = ActiveActivitiesBySlot[key];
                if (activity == null) continue;

                if (activity.Finished)
                {
                    activity.Finish();
                    Entity.Attributes.SetString("lastActivity", activity.Name);
                    if (Debug) Entity.World.Logger.Debug("ActivitySystem entity {0} activity {1} has finished", Entity.EntityId, activity.Name);
                    ActiveActivitiesBySlot.Remove(key);
                    continue;
                }

                activity.OnTick(dt);
            }

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

                    for (int i = 0; i < activity.Conditions.Length; i++)
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
                Entity.World.Logger.Error("Unable to load activity file " + file + " not such file found");
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
