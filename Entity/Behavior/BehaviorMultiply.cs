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
    public class EntityBehaviorMultiply : EntityBehavior
    {
        ITreeAttribute multiplyTree;
        JsonObject attributes;
        long callbackId;

        internal float QuantityPerDay
        {
            get { return attributes["quantityPerDay"].AsFloat(0.1f); }
        }

        internal AssetLocation SpawnEntityCode
        {
            get { return new AssetLocation(attributes["spawnEntityCode"].AsString("")); }
        }

        internal string RequiresNearbyEntityCode
        {
            get { return attributes["requiresNearbyEntityCode"].AsString(""); }
        }

        internal float RequiresNearbyEntityRange
        {
            get { return attributes["requiresNearbyEntityRange"].AsFloat(5); }
        }

        internal int GrowthCapQuantity
        {
            get { return attributes["growthCapQuantity"].AsInt(10); }
        }

        internal float GrowthCapRange
        {
            get { return attributes["growthCapRange"].AsFloat(10); }
        }

        internal AssetLocation[] GrowthCapEntityCodes
        {
            get { return AssetLocation.toLocations(attributes["growthCapEntityCodes"].AsStringArray(new string[0])); }
        }


        internal double TimeLastMultiply
        {
            get { return multiplyTree.GetDouble("timeLastMultiply"); }
            set { multiplyTree.SetDouble("timeLastMultiply", value); }
        }



        public EntityBehaviorMultiply(Entity entity) : base(entity)
        {

        }

        public override void Initialize(EntityType entityType, JsonObject attributes)
        {
            base.Initialize(entityType, attributes);

            this.attributes = attributes;

            multiplyTree = entity.WatchedAttributes.GetTreeAttribute("multiply");

            if (multiplyTree == null)
            {
                entity.WatchedAttributes.SetAttribute("multiply", multiplyTree = new TreeAttribute());
                TimeLastMultiply = entity.World.Calendar.TotalHours;
            }

            callbackId = entity.World.RegisterCallback(CheckMultiply, 3000);

            
        }

        private void CheckMultiply(float dt)
        {
            if (!entity.Alive) return;

            callbackId = entity.World.RegisterCallback(CheckMultiply, 3000);

            if (entity.World.Calendar == null) return;
            
            if (!HasRequiredEntityNearby())
            {
                TimeLastMultiply = entity.World.Calendar.TotalHours;
                return;
            }

            if (IsGrowthCapped())
            {
                TimeLastMultiply = entity.World.Calendar.TotalHours;
                return;
            }

            double daysPassed = (entity.World.Calendar.TotalHours - TimeLastMultiply) / 24f;

            if (QuantityPerDay * daysPassed >= 1)
            {
                TimeLastMultiply += 24;
                EntityType childType = entity.World.GetEntityType(SpawnEntityCode);
                Entity childEntity = entity.World.ClassRegistry.CreateEntity(childType);

                Random rand = entity.World.Rand;
                childEntity.ServerPos.SetFrom(entity.ServerPos);
                childEntity.ServerPos.Motion.X += (rand.NextDouble() - 0.5f) / 20f;
                childEntity.ServerPos.Motion.Z += (rand.NextDouble() - 0.5f) / 20f;

                childEntity.Pos.SetFrom(childEntity.ServerPos);
                entity.World.SpawnEntity(childEntity);
                entity.Attributes.SetString("origin", "reproduction");
            }
        }

        private bool HasRequiredEntityNearby()
        {
            if (RequiresNearbyEntityCode == null) return true;

            return entity.World.GetEntitiesAround(entity.ServerPos.XYZ, RequiresNearbyEntityRange, RequiresNearbyEntityRange, (e) =>
            {
                return RequiresNearbyEntityCode.Equals(e.Type.Code.Path);
            }).Length > 0;
        }

        public bool IsGrowthCapped()
        {
            bool haveUnloadedchunk = false;

            AssetLocation[] entityCodes = GrowthCapEntityCodes;
            int count = CountEntitiesAround(entity.ServerPos.XYZ, GrowthCapRange, GrowthCapRange, (e) =>
            {
                return entityCodes.Contains(e.Type.Code);
            }, ref haveUnloadedchunk);

            return haveUnloadedchunk || count >= GrowthCapQuantity;
        }


        public int CountEntitiesAround(Vec3d position, float horRange, float vertRange, ActionConsumable<Entity> matches, ref bool unloadedchunk)
        {
            int chunksize = entity.World.BlockAccessor.ChunkSize;
            int mincx = (int)((position.X - horRange) / chunksize);
            int maxcx = (int)((position.X + horRange) / chunksize);
            int mincy = (int)((position.Y - vertRange) / chunksize);
            int maxcy = (int)((position.Y + vertRange) / chunksize);
            int mincz = (int)((position.Z - horRange) / chunksize);
            int maxcz = (int)((position.Z + horRange) / chunksize);

            int count = 0;

            float horRangeSq = horRange * horRange;

            for (int cx = mincx; cx <= maxcx; cx++)
            {
                for (int cy = mincy; cy <= maxcy; cy++)
                {
                    for (int cz = mincz; cz <= maxcz; cz++)
                    {
                        IWorldChunk chunk = this.entity.World.BlockAccessor.GetChunk(cx, cy, cz);
                        if (chunk == null)
                        {
                            unloadedchunk = true;
                            return 0;
                        }

                        if (chunk.Entities == null) continue;
                        Entity ent;

                        for (int i = 0; i < chunk.EntitiesCount; i++)
                        {
                            ent = chunk.Entities[i];

                            if (ent == null || !ent.ServerPos.InRangeOf(position, horRangeSq, vertRange) || !matches(ent) || entity.State == EnumEntityState.Despawned) continue;

                            count++;
                        }
                    }
                }
            }

            return count;
        }


        public override void OnEntityDespawn(EntityDespawnReason despawn)
        {
            entity.World.UnregisterCallback(callbackId);
        }



        public override string PropertyName()
        {
            return "multiply";
        }
    }
}
