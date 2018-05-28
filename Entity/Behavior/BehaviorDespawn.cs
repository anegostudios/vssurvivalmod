using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace Vintagestory.GameContent
{
    public class EntityBehaviorDespawn : EntityBehavior
    {
        float? minPlayerDistance = null;
        float minSeconds = 30;
        int tick = 0;

        public float RemainingAliveTime
        {
            get { float? time = entity.Attributes.TryGetFloat("remainingAliveTime"); return time == null ? 0 : (float)time; }
            set { entity.Attributes.SetFloat("remainingAliveTime", value); }
        }


        public EntityBehaviorDespawn(Entity entity) : base(entity)
        {
        }

        public override void Initialize(EntityType entityType, JsonObject typeAttributes)
        {
            JsonObject minDist = typeAttributes["minPlayerDistance"];
            minPlayerDistance = (minDist.Exists) ? (float?)minDist.AsFloat() : null;

            minSeconds = typeAttributes["minSeconds"].AsFloat(30);
            

            if (!entity.Attributes.HasAttribute("remainingAliveTime"))
            {
                RemainingAliveTime = minSeconds;
            }
        }

        public override void OnGameTick(float deltaTime)
        {
            if (!entity.Alive) return;

            if (minPlayerDistance != null && tick++ % 20 == 0)
            {
                IPlayer player = entity.World.NearestPlayer(entity.ServerPos.X, entity.ServerPos.Y, entity.ServerPos.Z);
                if (player?.Entity != null && player.Entity.Pos.SquareDistanceTo(entity.ServerPos.XYZ) < minPlayerDistance * minPlayerDistance)
                {
                    RemainingAliveTime = minSeconds;
                    return;
                }
            }

            if ((RemainingAliveTime -= deltaTime) <= 0)
            {
                entity.Die(EnumDespawnReason.Expire, null);
                return;
            }
        }

        public override string PropertyName()
        {
            return "timeddespawn";
        }
    }
}
