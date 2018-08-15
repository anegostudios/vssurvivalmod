using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class EntityBehaviorGrow : EntityBehavior
    {
        ITreeAttribute growTree;
        JsonObject typeAttributes;
        long callbackId;

        internal float HoursToGrow
        {
            get { return typeAttributes["hoursToGrow"].AsFloat(96); }
        }

        internal AssetLocation[] AdultEntityCodes
        {
            get { return AssetLocation.toLocations(typeAttributes["adultEntityCodes"].AsStringArray(new string[0])); }
        }

        internal double TimeSpawned
        {
            get { return growTree.GetDouble("timeSpawned"); }
            set { growTree.SetDouble("timeSpawned", value); }
        }



        public EntityBehaviorGrow(Entity entity) : base(entity)
        {
        }

        public override void Initialize(EntityType entityType, JsonObject typeAttributes)
        {
            base.Initialize(entityType, typeAttributes);

            this.typeAttributes = typeAttributes;

            growTree = entity.WatchedAttributes.GetTreeAttribute("grow");

            if (growTree == null)
            {
                entity.WatchedAttributes.SetAttribute("grow", growTree = new TreeAttribute());
                TimeSpawned = entity.World.Calendar.TotalHours;
            }

            callbackId = entity.World.RegisterCallback(CheckGrowth, 3000);
        }


        private void CheckGrowth(float dt)
        {
            if (!entity.Alive) return;

            if (entity.World.Calendar.TotalHours >= TimeSpawned + HoursToGrow)
            {
                AssetLocation[] entityCodes = AdultEntityCodes;
                if (entityCodes.Length == 0) return;
                AssetLocation code = entityCodes[entity.World.Rand.Next(entityCodes.Length)];

                EntityType adultType = entity.World.GetEntityType(code);

                Cuboidf collisionBox = new Cuboidf()
                {
                    X1 = -adultType.HitBoxSize.X / 2,
                    Z1 = -adultType.HitBoxSize.X / 2,
                    X2 = adultType.HitBoxSize.X / 2,
                    Z2 = adultType.HitBoxSize.X / 2,
                    Y2 = adultType.HitBoxSize.Y
                };

                // Delay adult spawning if we're colliding
                if (entity.World.CollisionTester.IsColliding(entity.World.BlockAccessor, collisionBox, entity.ServerPos.XYZ, false))
                {
                    callbackId = entity.World.RegisterCallback(CheckGrowth, 3000);
                    return;
                }

                Entity adult = entity.World.ClassRegistry.CreateEntity(adultType);

                adult.ServerPos.SetFrom(entity.ServerPos);
                adult.Pos.SetFrom(adult.ServerPos);

                entity.Die(EnumDespawnReason.Expire, null);
                entity.World.SpawnEntity(adult);
            } else
            {
                callbackId = entity.World.RegisterCallback(CheckGrowth, 3000);
            }

            entity.World.FrameProfiler.Mark("entity-checkgrowth");
        }


        public override void OnEntityDespawn(EntityDespawnReason despawn)
        {
            entity.World.UnregisterCallback(callbackId);
        }


        public override string PropertyName()
        {
            return "grow";
        }
    }
}
