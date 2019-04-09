using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace Vintagestory.API.Common
{
    /// <summary>
    /// A slot from which the player can only take stuff out of, but not place anything in it
    /// </summary>
    public class ItemSlotInput : ItemSlot
    {
        public int outputSlotId;

        public ItemSlotInput(InventoryBase inventory, int outputSlotId) : base(inventory)
        {
            this.outputSlotId = outputSlotId;
        }

        public override bool CanHold(ItemSlot slot)
        {
            return CanBeStackedWithOutputSlotItem(slot as ItemSlot);
        }

        public override bool CanTake()
        {
            return true;
        }

        public override bool CanTakeFrom(ItemSlot sourceSlot)
        {
            return CanBeStackedWithOutputSlotItem(sourceSlot as ItemSlot) && base.CanTakeFrom(sourceSlot);
        }

       


        public bool CanBeStackedWithOutputSlotItem(ItemSlot sourceSlot, bool notifySlot = true)
        {
            ItemSlot outslot = inventory[outputSlotId];
            if (outslot.Empty) return true;

            ItemStack compareStack = sourceSlot.Itemstack.Collectible.CombustibleProps?.SmeltedStack?.ResolvedItemstack;
            if (compareStack == null) compareStack = sourceSlot.Itemstack;
            
            if (!outslot.Itemstack.Equals(inventory.Api.World, compareStack, GlobalConstants.IgnoredStackAttributes))
            {
                outslot.Inventory.PerformNotifySlot(outputSlotId);
                return false;
            }

            return true;
        }
    }
}
