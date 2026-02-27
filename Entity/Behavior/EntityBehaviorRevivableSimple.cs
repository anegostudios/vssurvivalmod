using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent
{
    public class EntityBehaviorRevivableSimple : EntityBehavior
    {
        public EntityBehaviorRevivableSimple(Entity entity) : base(entity) { }
        public override string PropertyName() => "revivablesimple";

        ItemStack reviveStack;

        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            base.Initialize(properties, attributes);

            var jstack = attributes["reviveStack"].AsObject<JsonItemStack>();

            jstack.Resolve(entity.Api.World, string.Format("entity {0} revivablesimple stack", entity.Code), true);
            reviveStack = jstack.ResolvedItemstack;
        }

        public override void OnInteract(EntityAgent byEntity, ItemSlot itemslot, Vec3d hitPosition, EnumInteractMode mode, ref EnumHandling handled)
        {
            if (!byEntity.Controls.ShiftKey) return;

            var ebh = entity.GetBehavior<EntityBehaviorHealth>();

            bool healable = ebh != null && ebh.Health < ebh.MaxHealth;

            if (healable &&  reviveStack != null && itemslot.Itemstack.Satisfies(reviveStack))
            {
                entity.World.PlaySoundAt(new AssetLocation("sounds/effect/latch"), entity, null, true, 16);

                itemslot.TakeOut(1);
                itemslot.MarkDirty();

                var health = ebh.Health;
                if (!entity.Alive) entity.Revive();

                ebh.Health = health + 15;
            }
        }

        public override WorldInteraction[] GetInteractionHelp(IClientWorldAccessor world, EntitySelection es, IClientPlayer player, ref EnumHandling handled)
        {
            if (entity.Alive) return null;

            return [
                new WorldInteraction() {
                    ActionLangCode = "entityinteraction-revive",
                    Itemstacks = [reviveStack],
                    HotKeyCode = "shift",
                    MouseButton = EnumMouseButton.Right
                }
            ];
        }
    }
}
