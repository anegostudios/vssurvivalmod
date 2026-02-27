using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent
{
    public class EntityBehaviorRightClickPickup : EntityBehavior
    {
        ItemStack collectedStack;

        public EntityBehaviorRightClickPickup(Entity entity) : base(entity)
        {
        }

        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            base.Initialize(properties, attributes);
            var jstack = attributes["collectedStack"].AsObject<JsonItemStack>();
            jstack?.Resolve(entity.World, "right click pickup collected stack for " + entity.Code, true);
            collectedStack = jstack?.ResolvedItemstack;
        }

        public override void OnInteract(EntityAgent byEntity, ItemSlot itemslot, Vec3d hitPosition, EnumInteractMode mode, ref EnumHandling handled)
        {
            if (mode == EnumInteractMode.Interact)
            {
                handled = EnumHandling.Handled;

                if (byEntity.World.Side == EnumAppSide.Server)
                {
                    byEntity.World.PlaySoundAt(HeldSounds.InvPlaceDefault, byEntity);

                    if (!byEntity.TryGiveItemStack(collectedStack))
                    {
                        byEntity.World.SpawnItemEntity(collectedStack, entity.Pos.XYZ);
                    }

                    entity.Die(EnumDespawnReason.Removed);
                }

                return;
            }

            base.OnInteract(byEntity, itemslot, hitPosition, mode, ref handled);
        }

        public override string PropertyName() => "rightclickpickup";
    }
}
