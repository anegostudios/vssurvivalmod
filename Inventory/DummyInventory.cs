using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace Vintagestory.GameContent
{
    public class DummyInventory : InventoryBase
    {
        static int dummyId = 1;
        ItemSlot[] slots;

        public DummyInventory(ICoreAPI api) : this("dummy-" + (dummyId++), api)
        {
            slots = new ItemSlot[] { new ItemSlot(this) };
        }

        private DummyInventory(string inventoryID, ICoreAPI api) : base(inventoryID, api)
        {
        }

        private DummyInventory(string className, string instanceID, ICoreAPI api) : base(className, instanceID, api)
        {
        }

        public override ItemSlot this[int slotId] { get => slots[slotId]; set => slots[slotId] = value; }
        public override int Count => 1;

        public override void FromTreeAttributes(ITreeAttribute tree)
        {
            slots = SlotsFromTreeAttributes(tree, slots);
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            SlotsToTreeAttributes(slots, tree);
        }

        public override float GetTransitionSpeedMul(EnumTransitionType transType, ItemStack stack)
        {
            return base.GetTransitionSpeedMul(transType, stack);
        }
    }
}
