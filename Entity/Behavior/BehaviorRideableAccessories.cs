using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent
{
    public class EntityBehaviorRideableAccessories : EntityBehaviorAttachable
    {
        protected bool hasBridle;

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

        private bool Bh_CanTurn(IMountableSeat seat, out string errorMessage)
        {
            hasBridle = false;
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

        private bool EntityBehaviorDressable_CanRide(IMountableSeat seat, out string errorMessage)
        {
            hasBridle = false;
            foreach (var slot in Inventory)
            {
                if (slot.Empty || slot.Itemstack.Collectible.Attributes == null) continue;
                
                var attr = slot.Itemstack.Collectible.Attributes;
                if (attr.IsTrue("isSaddle"))
                {
                    hasBridle = true;
                    break;
                }
            }

            errorMessage = hasBridle ? null : "nosaddle";
            return hasBridle;
        }

        public override void OnInteract(EntityAgent byEntity, ItemSlot itemslot, Vec3d hitPosition, EnumInteractMode mode, ref EnumHandling handled)
        {
            base.OnInteract(byEntity, itemslot, hitPosition, mode, ref handled);


        }

    }
}
