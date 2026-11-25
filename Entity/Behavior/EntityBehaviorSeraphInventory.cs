using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;

#nullable disable

namespace Vintagestory.GameContent
{
    public class EntityBehaviorSeraphInventory : EntityBehaviorTexturedClothing
    {
        public override string PropertyName() => "seraphinventory";
        EntityAgent eagent;
        public override InventoryBase Inventory => inv;

        public override string InventoryClassName => "seraphinventory";

        InventoryGear inv;

        // These slots are rendered as held items, not step parented onto the seraph
        protected override bool ShouldAddGearToShape(ItemSlot slot) => slot != inv[15] && slot != inv[16];

        public EntityBehaviorSeraphInventory(Entity entity) : base(entity)
        {
            eagent = entity as EntityAgent;
            inv = new InventoryGear(null, null);
        }

        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            Api = entity.World.Api;

            inv.LateInitialize("gearinv-" + entity.EntityId, Api);
            loadInv();

            eagent.WatchedAttributes.RegisterModifiedListener("wearablesInv", wearablesModified);

            base.Initialize(properties, attributes);
        }

        private void wearablesModified()
        {
            loadInv();
            eagent.MarkShapeModified();
        }
    }
}
