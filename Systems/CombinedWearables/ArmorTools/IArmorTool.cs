using System.Collections.Generic;
using Vintagestory.API.Common;

namespace Vintagestory.GameContent
{
    /// <summary>
    /// Tool for repairing armor bits on crafting stand
    /// </summary>
    public interface IArmorRepairTool
    {
        /// <summary>
        /// If tool can repair at least one piece from provided slots
        /// </summary>
        /// <param name="toolSlot">Slot containing tool</param>
        /// <param name="bitsSlots">A collection of slots clicked by a tool</param>
        /// <param name="byEntity">Entity that used the tool</param>
        /// <returns>If tool can repair at least one piece from provided slots</returns>
        bool CanRepair(ItemSlot toolSlot, IEnumerable<ItemSlot> bitsSlots, EntityAgent byEntity, ILayeredContainer craftingStand);

        /// <summary>
        /// Repairs armor pieces in pieces slots
        /// </summary>
        /// <param name="toolSlot">Slot containing tool</param>
        /// <param name="bitsSlots">A collection of slots clicked by a tool</param>
        /// <param name="byEntity">Entity that used the tool</param>
        void Repair(ItemSlot toolSlot, IEnumerable<ItemSlot> bitsSlots, EntityAgent byEntity, ILayeredContainer craftingStand);
    }


    /// <summary>
    /// Tool for removing armor bits from crafting stand
    /// </summary>
    public interface IArmorDisassemblyTool
    {
        /// <summary>
        /// If tool can remove armor bit in slot from crafting stand
        /// </summary>
        /// <param name="toolSlot">Slot containing tool</param>
        /// <param name="bitSlot">Slot with bit that tool tries to remove</param>
        /// <param name="byEntity">Entity that used the tool</param>
        /// <param name="slotConfig">Config of the slot that contains bit</param>
        /// <returns>If tool can remove armor bit in slot from crafting stand</returns>
        bool CanDisassemble(ItemSlot toolSlot, ItemSlot bitSlot, EntityAgent byEntity, WearableBitSlotConfig slotConfig, ILayeredContainer craftingStand);

        /// <summary>
        /// Called before piece is removed from slot
        /// </summary>
        /// <param name="toolSlot">Slot containing tool</param>
        /// <param name="bitSlot">Slot with piece that tool tries to remove</param>
        /// <param name="byEntity">Entity that used the tool</param>
        /// <param name="slotConfig">Config of the slot that contains piece</param>
        void OnBeforeDisassembled(ItemSlot toolSlot, ItemSlot bitSlot, EntityAgent byEntity, WearableBitSlotConfig slotConfig, ILayeredContainer craftingStand);
    }


    /// <summary>
    /// Tool for chaning variant of armor bit on crafting stand
    /// </summary>
    public interface IArmorVariantFlipperTool
    {
        /// <summary>
        /// If tool can change shape and texture variant in provided slot
        /// </summary>
        /// <param name="toolSlot">Slot containing tool</param>
        /// <param name="bitSlot">Slot with armor piece</param>
        /// <param name="byEntity">Entity that used the tool</param>
        /// <param name="slotConfig">Config of the slot that contains piece</param>
        /// <returns>If tool can remove armor piece in slot from crafting stand</returns>
        bool CanFlipVariant(ItemSlot toolSlot, ItemSlot bitSlot, EntityAgent byEntity, WearableBitSlotConfig slotConfig, ILayeredContainer craftingStand);

        /// <summary>
        /// Changes shape and texture varaint of armor piece in provided slot
        /// </summary>
        /// <param name="toolSlot">Slot containing tool</param>
        /// <param name="bitSlot">Slot with armor piece</param>
        /// <param name="byEntity">Entity that used the tool</param>
        /// <param name="slotConfig">Config of the slot that contains piece</param>
        void FlipVariant(ItemSlot toolSlot, ItemSlot bitSlot, EntityAgent byEntity, WearableBitSlotConfig slotConfig, ILayeredContainer craftingStand);
    }


    /// <summary>
    /// Tool for combining armor bits on crafting stand
    /// </summary>
    public interface IArmorCombinationTool
    {
        /// <summary>
        /// If tool can combine pieces into final armor item
        /// </summary>
        /// <param name="toolSlot">Slot containing tool</param>
        /// <param name="bitsSlots">All slots containing pieces that will be combined</param>
        /// <param name="byEntity">Entity that used the tool</param>
        /// <returns>If tool can combine pieces into final armor item</returns>
        bool CanCombine(ItemSlot toolSlot, IEnumerable<ItemSlot> bitsSlots, EntityAgent byEntity, ILayeredContainer craftingStand);

        /// <summary>
        /// Called before pieces are combined
        /// </summary>
        /// <param name="toolSlot">Slot containing tool</param>
        /// <param name="bitsSlots">All slots containing pieces that will be combined</param>
        /// <param name="byEntity">Entity that used the tool</param>
        void OnBeforeCombined(ItemSlot toolSlot, IEnumerable<ItemSlot> bitsSlots, EntityAgent byEntity, ILayeredContainer craftingStand);

        /// <summary>
        /// Called after pieces are combined
        /// </summary>
        /// <param name="toolSlot">Slot containing tool</param>
        /// <param name="combinedSlot">Slot containing resulting item</param>
        /// <param name="byEntity">Entity that used the tool</param>
        void OnAfterCombined(ItemSlot toolSlot, ItemSlot combinedSlot, EntityAgent byEntity, ILayeredContainer craftingStand);

        /// <summary>
        /// If pieces should be removed from crafting stand on combining
        /// </summary>
        /// <param name="toolSlot">Slot containing tool</param>
        /// <param name="bitsSlots">All slots containing pieces that will be combined</param>
        /// <param name="byEntity">Entity that used the tool</param>
        /// <returns>If pieces should be removed from crafting stand on combining</returns>
        bool ShouldConsumeArmorbits(ItemSlot toolSlot, IEnumerable<ItemSlot> bitsSlots, EntityAgent byEntity, ILayeredContainer craftingStand);
    }
}
