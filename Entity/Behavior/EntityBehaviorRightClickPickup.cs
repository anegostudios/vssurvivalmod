using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent
{
    public class EntityBehaviorRightClickPickup : EntityBehavior
    {
        ItemStack collectedStack;
        bool requireSneaking;

        public EntityBehaviorRightClickPickup(Entity entity) : base(entity)
        {
        }

        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            base.Initialize(properties, attributes);
            var jstack = attributes["collectedStack"].AsObject<JsonItemStack>();
            jstack?.Resolve(entity.World, "right click pickup collected stack for " + entity.Code, true);
            collectedStack = jstack?.ResolvedItemstack;

            requireSneaking = attributes.IsTrue("requireSneaking");
        }

        public override void OnInteract(EntityAgent byEntity, ItemSlot itemslot, Vec3d hitPosition, EnumInteractMode mode, ref EnumHandling handled)
        {
            if (mode == EnumInteractMode.Interact)
            {
                handled = EnumHandling.Handled;

                if (byEntity.World.Side == EnumAppSide.Server)
                {
                    if (requireSneaking && !byEntity.Controls.ShiftKey)
                    {
                        handled = EnumHandling.PassThrough;
                        return;
                    }

                    EntityBehaviorAttachable behaviorAttachable = entity.GetBehavior<EntityBehaviorAttachable>();
                    if (behaviorAttachable != null && (behaviorAttachable?.Inventory.Empty == false || !byEntity.RightHandItemSlot.Empty))
                    {
                        handled = EnumHandling.PassThrough;
                        return;
                    }

                    byEntity.World.PlaySoundAt(HeldSounds.InvPlaceDefault, byEntity);

                    if (!byEntity.TryGiveItemStack(collectedStack))
                    {
                        byEntity.World.SpawnItemEntity(collectedStack, entity.Pos.XYZ);
                    }

                    entity.Die(EnumDespawnReason.PickedUp);
                }

                return;
            }

            base.OnInteract(byEntity, itemslot, hitPosition, mode, ref handled);
        }

        public override WorldInteraction[] GetInteractionHelp(IClientWorldAccessor world, EntitySelection es, IClientPlayer player, ref EnumHandling handled)
        {
            var interactions = ObjectCacheUtil.GetOrCreate(world.Api, "rightClickPickupEntityInteractions", () =>
            {
                return new WorldInteraction[] {
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-behavior-rightclickpickup",
                        MouseButton = EnumMouseButton.Right,
                        HotKeyCode = requireSneaking ? "sneak" : null,
                        RequireFreeHand = true
                    }
                };
            });

            handled = EnumHandling.Handled;
            return interactions;
        }

        public override string PropertyName() => "rightclickpickup";
    }
}
