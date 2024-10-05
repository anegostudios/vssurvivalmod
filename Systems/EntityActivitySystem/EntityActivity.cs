using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace Vintagestory.GameContent
{
    public enum EnumConditionLogicOp
    {
        OR,
        AND
    } 

    [JsonObject(MemberSerialization.OptIn)]
    public class EntityActivity : IEntityActivity
    {
        [JsonProperty]
        public int Slot { get; set; } = 0;
        [JsonProperty]
        public double Priority { get; set; } = 1;
        [JsonProperty]
        public string Name { get; set; }
        [JsonProperty]
        public IActionCondition[] Conditions { get; set; } = new IActionCondition[0];
        [JsonProperty]
        public IEntityAction[] Actions { get; set; } = new IEntityAction[0];
        [JsonProperty]
        public EnumConditionLogicOp ConditionsOp { get; set; } = EnumConditionLogicOp.AND;

        public IEntityAction CurrentAction => currentActionIndex < 0 ? null : Actions[currentActionIndex];

        public bool Finished { get; set; }

        public int currentActionIndex=-1;
        EntityActivitySystem vas;

        public EntityActivity() { }
        public EntityActivity(EntityActivitySystem vas)
        {
            this.vas = vas;
        }

        public void OnLoaded(EntityActivitySystem vas)
        {
            this.vas = vas;
            if (Actions != null) foreach (var act in Actions) act.OnLoaded(vas);
            if (Conditions != null) foreach (var tri in Conditions) tri.OnLoaded(vas);
        }


        public void Cancel()
        {
            CurrentAction?.Cancel();
            currentActionIndex = -1;
            Finished = true;
        }

        public void Start()
        {
            Finished = false;
            currentActionIndex = 0;
            CurrentAction.Start(this);
            if (vas.Debug)
            {
                vas.Entity.World.Logger.Debug("ActivitySystem entity {0}, starting new Activity - {1}", vas.Entity.EntityId, this.Name);
                vas.Entity.World.Logger.Debug("starting next action {0}", CurrentAction.Type);
            }
        }

        public void Finish()
        {
            CurrentAction?.Finish();
        }

        public void OnTick(float dt)
        {
            if (CurrentAction == null) return;
            
            CurrentAction.OnTick(dt);
            if (CurrentAction.IsFinished())
            {
                CurrentAction.Finish();
                if (currentActionIndex < Actions.Length - 1)
                {
                    currentActionIndex++;
                    CurrentAction.Start(this);
                    if (vas.Debug) vas.Entity.World.Logger.Debug("ActivitySystem entity {0}, starting next Action - {1}", vas.Entity.EntityId, CurrentAction.Type);
                }
                else
                {
                    currentActionIndex = -1;
                    Finished = true;
                }
            }
        }
        

        public void LoadState(ITreeAttribute tree)
        {
            loadState(Actions, tree, "action");
            loadState(Conditions, tree, "condition");
        }

        public void StoreState(ITreeAttribute tree)
        {
            if (Actions != null) storeState(Actions, tree, "action");
            if (Conditions != null) storeState(Conditions, tree, "condition");            
        }


        public void storeState(IStorableTypedComponent[] elems, ITreeAttribute tree, string key)
        {
            for (int i = 0; i < elems.Length; i++)
            {
                var atree = new TreeAttribute();
                elems[i].StoreState(atree);
                atree.SetString("type", elems[i].Type);
                tree[key + i] = atree;
            }
        }

        public void loadState(IStorableTypedComponent[] elems, ITreeAttribute tree, string key)
        {
            int i = 0;
            while (i < elems.Length)
            {
                var atree = tree.GetTreeAttribute(key + i);
                if (atree == null) break;
                elems[i].LoadState(atree);
                i++;
            }
        }

        public override string ToString()
        {
            return base.ToString();
        }

        public EntityActivity Clone()
        {
            JsonSerializerSettings settings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All };
            string json = JsonConvert.SerializeObject(this, Formatting.Indented, settings);
            var ea = JsonUtil.ToObject<EntityActivity>(json, "", settings);
            ea.OnLoaded(vas);
            return ea;
        }

    }

}
