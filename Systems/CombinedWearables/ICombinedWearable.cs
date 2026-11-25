using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;


namespace Vintagestory.GameContent
{
    /// <summary>
    /// If wearable is armor and player receives damage from entity into slot conaining it, protection modifiers will be taken from this interface.
    /// When receiving durability damage this way hitToken 'DamageArmor' receives will be the same that 'GetProtectionModifiers' provided for this hit.
    /// </summary>
    public interface ICustomArmor
    {
        /// <summary>
        /// Returns protection modifiers each time damage is dealt to the wearer.
        /// </summary>
        /// <param name="armorSlot">Slot containing the armor</param>
        /// <param name="hitToken">String value that will be passed to <see cref="DamageArmor"/></param>
        /// <returns></returns>
        ProtectionModifiers GetProtectionModifiers(ItemSlot armorSlot, out string hitToken);

        /// <summary>
        /// Called when wearable should receive durability damage from entity taking damage.<br/>
        /// </summary>
        /// <param name="world"></param>
        /// <param name="byEntity"></param>
        /// <param name="armorSlot">slot that contains wearable</param>
        /// <param name="durabilityDamage"></param>
        /// <param name="hitToken">String value prodived by <see cref="GetProtectionModifiers"/></param>
        void DamageArmor(IWorldAccessor world, Entity byEntity, ItemSlot armorSlot, int durabilityDamage, string hitToken);
    }

    /// <summary>
    /// Wearable item combined from multiple bits
    /// </summary>
    public interface ICombinedWearable
    {
        int GetBitsCacheKey(ItemStack carrier);

        IEnumerable<WearableBitEntry> RetrieveBits(ItemStack carrier);

        void StoreBits(ItemStack carrier, IEnumerable<WearableBitEntry> bits);

        void CombineBits(ItemStack carrier, ILayeredContainer container, Entity byEntity, IEnumerable<int> fromSlots, bool consumeBits);

        void UncombineBits(ItemStack carrier, ILayeredContainer container, Entity byEntity);
    }

    /// <summary>
    /// Container holding inventory divided into layers.<br/>
    /// Number of slots is a multiple of number of layers.<br/>
    /// Slot layer index is a reminder from diving slot index by number of layers.
    /// </summary>
    public interface ILayeredContainer
    {
        /// <summary>
        /// Unrelying inventory
        /// </summary>
        IInventory Inventory { get; }

        /// <summary>
        /// Slot code by slot index in inventory
        /// </summary>
        string[] GetSlotCodes();

        /// <summary>
        /// Layer by layer index.<br/>
        /// Slot layer index is a reminder from diving slot index by number of layers.
        /// </summary>
        string[] GetLayerCodes();

        /// <summary>
        /// Slot configs by slot index in inventory
        /// </summary>
        /// <returns></returns>
        WearableBitSlotConfig[] GetSlotConfigs();


        bool GetFirstSlotBy(string slotCode, string layerCode, [NotNullWhen(true)] out ItemSlot? slot, [NotNullWhen(true)] out WearableBitSlotConfig? config);
    }

    /// <summary>
    /// Attaches wearable bit shape to entity instead of default way of adding gear to entity shape
    /// </summary>
    public interface IWearableBitAttachedShape
    {
        void AddGearToShape(ITextureAtlasAPI targetAtlas, Shape entityShape, WearableBitEntry bit, string shapePathForLogging, ref string[] willDeleteElements, IDictionary<string, CompositeTexture> collectedTextures, string additionalPrefix = "", float carrierDamageEffect = 0);
    }
}
