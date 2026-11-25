using Vintagestory.API.Common;

namespace Vintagestory.GameContent
{
    /// <summary>
    /// Tool for removing armor bit from crafting stand
    /// </summary>
    public class CollectibleBehaviorArmorDisassemlyTool : CollectibleBehaviorArmorTool, IArmorDisassemblyTool
    {
        public CollectibleBehaviorArmorDisassemlyTool(CollectibleObject collObj) : base(collObj)
        {
        }


        public bool CanDisassemble(ItemSlot toolSlot, ItemSlot bitSlot, EntityAgent byEntity, WearableBitSlotConfig slotConfig, ILayeredContainer craftingStand)
        {
            return Supported(toolSlot, [bitSlot], byEntity, craftingStand);
        }

        public void OnBeforeDisassembled(ItemSlot toolSlot, ItemSlot bitSlot, EntityAgent byEntity, WearableBitSlotConfig slotConfig, ILayeredContainer craftingStand)
        {
            OnUsed(toolSlot, [bitSlot], byEntity, craftingStand);
        }
    }
}
