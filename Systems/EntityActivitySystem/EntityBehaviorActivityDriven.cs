using System;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;

namespace Vintagestory.GameContent
{
    public class EntityBehaviorActivityDriven : EntityBehavior
    {
        ICoreAPI Api;
        public EntityActivitySystem ActivitySystem;

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
            if (taskAi == null) return;

            taskAi.TaskManager.OnShouldExecuteTask += (task) =>
            {
                return eagent.MountedOn == null && ActivitySystem.ActiveActivitiesBySlot.Values.FirstOrDefault(a => a.CurrentAction == null || a.CurrentAction.Type != "standardai") == null;
            };
        }

        public override void OnGameTick(float deltaTime)
        {
            base.OnGameTick(deltaTime);

            ActivitySystem.OnTick(deltaTime);
        }

        public override string PropertyName() => "activitydriven";
    }

}
