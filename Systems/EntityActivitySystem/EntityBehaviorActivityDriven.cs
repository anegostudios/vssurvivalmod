using System;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class EntityBehaviorActivityDriven : EntityBehavior
    {
        ICoreAPI Api;
        public EntityActivitySystem ActivitySystem;

        public event ActionBoolReturn OnShouldRunActivitySystem;

        public EntityBehaviorActivityDriven(Entity entity) : base(entity)
        {
            Api = entity.Api;
            if (!(entity is EntityAgent)) throw new InvalidOperationException("ActivityDriven behavior only avaialble for EntityAgent classes.");
            ActivitySystem = new EntityActivitySystem(entity as EntityAgent);
        }

        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            base.Initialize(properties, attributes);
            var path = attributes?["activityCollectionPath"]?.AsString();
            load(path);
        }

        public bool load(string p)
        {
            return ActivitySystem.Load(p == null ? null : AssetLocation.Create(p, entity.Code.Domain));
        }

        public override void OnEntitySpawn()
        {
            base.OnEntitySpawn();

            if (Api.Side == EnumAppSide.Server)
            {
                setupTaskBlocker();
            }
        }

        public override void OnEntityLoaded()
        {
            base.OnEntityLoaded();

            if (Api.Side == EnumAppSide.Server)
            {
                setupTaskBlocker();
            }
        }

        void setupTaskBlocker()
        {
            var eagent = entity as EntityAgent;
            EntityBehaviorTaskAI taskAi = entity.GetBehavior<EntityBehaviorTaskAI>();
            if (taskAi != null)
            {
                taskAi.TaskManager.OnShouldExecuteTask += (task) =>
                {
                    return eagent.MountedOn == null && ActivitySystem.ActiveActivitiesBySlot.Values.FirstOrDefault(a => a.CurrentAction == null || a.CurrentAction.Type != "standardai") == null;
                };
            }

            var ebc = entity.GetBehavior<EntityBehaviorConversable>();
            if (ebc != null) {
                ebc.CanConverse += Ebc_CanConverse;
            }
        }

        private bool Ebc_CanConverse(out string errorMessage)
        {
            var vs = Api.ModLoader.GetModSystem<VariablesModSystem>();
            bool canTalk = vs.GetVariable(EnumActivityVariableScope.Entity, "tooBusyToTalk", entity.EntityId).ToBool(false) != true;
            errorMessage = canTalk ? null : "cantconverse-toobusy";
            return canTalk;
        }

        bool active = true;

        public override void OnGameTick(float deltaTime)
        {
            base.OnGameTick(deltaTime);

            if (OnShouldRunActivitySystem != null)
            {
                bool wasActive = active;
                active = true;
                foreach (ActionBoolReturn act in OnShouldRunActivitySystem.GetInvocationList())
                {
                    if (!act.Invoke())
                    {
                        active = false;
                        break;
                    }
                }

                if (wasActive && !active)
                {
                    ActivitySystem.Pause();
                }
                if (!wasActive && active)
                {
                    ActivitySystem.Resume();
                }
            }

            if (active)
            {
                ActivitySystem.OnTick(deltaTime);
            }
        }

        public override string PropertyName() => "activitydriven";
    }

}
