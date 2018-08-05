using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace Vintagestory.GameContent
{
    public class EntityBehaviorDespawn : EntityBehavior
    {
        float? minPlayerDistance = null;
        float? belowLightLevel = null;

        float minSeconds = 30;
        int tick = 0;

        public float DeathTime
        {
            get { float? time = entity.Attributes.TryGetFloat("deathTime"); return time == null ? 0 : (float)time; }
            set { entity.Attributes.SetFloat("deathTime", value); }
        }

        public EntityBehaviorDespawn(Entity entity) : base(entity)
        {
        }

        public override void Initialize(EntityType entityType, JsonObject typeAttributes)
        {
            JsonObject minDist = typeAttributes["minPlayerDistance"];
            minPlayerDistance = (minDist.Exists) ? (float?)minDist.AsFloat() : null;

            JsonObject belowLight = typeAttributes["belowLightLevel"];
            belowLightLevel = (belowLight.Exists) ? (float?)belowLight.AsFloat() : null;
            

            minSeconds = typeAttributes["minSeconds"].AsFloat(30);
        }

        public override void OnGameTick(float deltaTime)
        {
            if (!entity.Alive) return;

            bool shouldTest = tick++ % 30 == 0;

            if (shouldTest && (LightLevelOk() || PlayerDistanceOk()))
            {
                DeathTime = 0;
                return;
            }

            deltaTime = System.Math.Min(deltaTime, 2);
            
            if ((DeathTime += deltaTime) > minSeconds)
            {
                entity.Die(EnumDespawnReason.Expire, null);
                return;
            }
        }


        public bool PlayerDistanceOk()
        {
            if (minPlayerDistance == null) return true;

            IPlayer player = entity.World.NearestPlayer(entity.ServerPos.X, entity.ServerPos.Y, entity.ServerPos.Z);

            return player?.Entity != null && player.Entity.Pos.SquareDistanceTo(entity.ServerPos.XYZ) < minPlayerDistance * minPlayerDistance;
        }

        public bool LightLevelOk()
        {
            if (belowLightLevel == null) return true;
            int level = entity.World.BlockAccessor.GetLightLevel(entity.ServerPos.AsBlockPos, EnumLightLevelType.MaxLight);

            return level >= belowLightLevel;

            
        }

        public override string PropertyName()
        {
            return "timeddespawn";
        }
    }
}
