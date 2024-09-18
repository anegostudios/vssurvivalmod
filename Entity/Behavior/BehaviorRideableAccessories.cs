using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class EntityBehaviorRideableAccessories : EntityBehaviorAttachable
    {
        protected bool hasSaddle;

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
            }
        }

        private bool EntityBehaviorDressable_CanRide(IMountableSeat seat, out string errorMessage)
        {
            hasSaddle = false;
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

        public override void OnInteract(EntityAgent byEntity, ItemSlot itemslot, Vec3d hitPosition, EnumInteractMode mode, ref EnumHandling handled)
        {
            base.OnInteract(byEntity, itemslot, hitPosition, mode, ref handled);


        }

    }
}
