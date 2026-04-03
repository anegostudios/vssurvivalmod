using System.Collections.Generic;
using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;

namespace Vintagestory.GameContent
{
    /// <summary>
    /// Allows to attach specific items to entity.
    /// <br/>Requires <see cref="EntityBehaviorSelectionBoxes"/>
    /// <br/>If <see cref="EntityBehaviorRideable"/> is present, requires two attached items with isSaddle and isBridle attributes for entity to be ridden.
    /// <br/>Uses the "rideableaccessories" code
    /// </summary>
    /// <example><code lang="json">
    ///"behaviors": [
    /// {
    ///  "code": "rideableaccessories",
    ///  "wearableSlots": [
    ///      { "code": "head", "forCategoryCodes": ["bridle"], "attachmentPointCode": "HeadAP" },
    ///      { "code": "face", "forCategoryCodes": ["face"], "attachmentPointCode": "FaceAP" },
    ///      { "code": "neck", "forCategoryCodes": ["lantern"], "attachmentPointCode": "NeckAP" },
    ///      { "code": "middleback", "forCategoryCodes": ["saddle"], "attachmentPointCode": "MidAP" },
    ///      { "code": "middlebackunder", "forCategoryCodes": ["blanket"], "attachmentPointCode": "MidUnderAP" },
    ///      { "code": "lowerback", "forCategoryCodes": ["bedroll", "pillion"], "attachmentPointCode": "RearAP" },
    ///      { "code": "lowerbackside", "forCategoryCodes": ["sidebags"], "attachmentPointCode": "RearSideAP" },
    ///      {
    ///          "code": "frontrightside",
    ///          "forCategoryCodes": ["utilities", "pot", "weaponfalx", "weapon1m", "bowshort", "bowlong"],
    ///          "attachmentPointCode": "RFrontAP"
    ///      },
    ///      {
    ///          "code": "frontleftside",
    ///          "forCategoryCodes": ["utilities", "pot", "weaponfalx", "weapon1m", "bowshort", "bowlong"],
    ///          "attachmentPointCode": "LFrontAP"
    ///      },
    ///      { "code": "gearleft", "forCategoryCodes": ["gear"], "attachmentPointCode": "TempGear1AP" },
    ///      { "code": "gearback", "forCategoryCodes": ["gear"], "attachmentPointCode": "TempGear2AP" },
    ///      { "code": "gearright", "forCategoryCodes": ["gear"], "attachmentPointCode": "TempGear3AP" }
    ///  ]
    /// },
    ///],
    /// </code></example>
    [DocumentAsJson]
    [AddDocumentationProperty("isBridle", "Use this on a collectible type. If True, this collectible is recognized as a bridle when attached to an entity", "System.Boolean", "Optional", "False", true)]
    [AddDocumentationProperty("isSaddle", "Use this on a collectible type. If True, this collectible is recognized as a saddle when attached to an entity?", "System.Boolean", "Optional", "False", true)]
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
