using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;

namespace Vintagestory.GameContent
{
    public class EntityBehaviorRideableAccessories : EntityBehaviorAttachable
    {
        public EntityBehaviorRideableAccessories(Entity entity) : base(entity)
        {
        }

        public override void AfterInitialized(bool onFirstSpawn)
        {
            base.AfterInitialized(onFirstSpawn);

            var bh = entity.GetBehavior<EntityBehaviorRideable>();
            if (bh != null)
            {
                bh.CanRide += EntityBehaviorDressable_CanRide;
                bh.CanTurn += Bh_CanTurn;
            }
        }

        protected virtual bool Bh_CanTurn(IMountableSeat seat, out string? errorMessage)
        {
            bool hasBridle = false;
            foreach (var slot in Inventory)
            {
                if (slot.Empty || slot.Itemstack.Collectible.Attributes == null) continue;

                var attr = slot.Itemstack.Collectible.Attributes;
                if (attr.IsTrue("isBridle"))
                {
                    hasBridle = true;
                    break;
                }
            }

            errorMessage = hasBridle ? null : "nobridle";
            return hasBridle;
        }

        protected virtual bool EntityBehaviorDressable_CanRide(IMountableSeat seat, out string? errorMessage)
        {
            bool hasSaddle = false;
            foreach (var slot in Inventory)
            {
                if (slot.Empty || slot.Itemstack.Collectible.Attributes == null) continue;

                var attr = slot.Itemstack.Collectible.Attributes;
                if (attr.IsTrue("isSaddle"))
                {
                    hasSaddle = true;
                    break;
                }
            }

            errorMessage = hasSaddle ? null : "nosaddle";
            return hasSaddle;
        }

        public override bool TryEarlyLoadCollectibleMappings(IWorldAccessor worldForCollectibleResolve, Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping, bool resolveImports, EntityProperties entityProperties, JsonObject behaviorConfig)
        {
            if (entity is EntityArmorStand armorStand)
            {
                armorStand.TryEarlyUpdateOldArmorStandInventory(worldForCollectibleResolve);
            }
            return base.TryEarlyLoadCollectibleMappings(worldForCollectibleResolve, oldBlockIdMapping, oldItemIdMapping, resolveImports, entityProperties, behaviorConfig);
        }
    }
}
