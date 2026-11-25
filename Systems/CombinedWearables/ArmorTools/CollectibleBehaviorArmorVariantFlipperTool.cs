using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class BitShapeVariantConnectedSlotsEntry
    {
        public string Variant { get; set; } = "";
        public string[] Slots { get; set; } = [];
    }

    public class BitShapeVariantsForSlot
    {
        public string CurrentVariant { get; set; } = "";

        public string CurrentSlotCode { get; set; } = "";

        public string CurrentLayerCode { get; set; } = "";

        public List<string> Variants { get; set; } = [];

        public Dictionary<string, string[]> ConnectedVariants { get; set; } = [];


        public string GetCurrentShapeVariant(ItemStack stack)
        {
            if (Variants.Count == 0) return "";

            return stack.Attributes?.GetString("shapeVariant") ?? Variants[0];
        }

        public void SetCurrentShapeVariant(ItemStack stack, string variant)
        {
            stack.Attributes?.SetString("shapeVariant", variant);
            stack.Attributes?.SetString("shapeVariantType", CurrentSlotCode);
            CurrentVariant = variant;
        }

        public void UnsetCurrentShapeVariant(ItemStack stack)
        {
            stack.Attributes?.RemoveAttribute("shapeVariant");
            stack.Attributes?.RemoveAttribute("shapeVariantType");
        }

        public bool FlipVariant(ItemStack stack, ILayeredContainer craftingStand)
        {
            if (Variants.Count == 0) return false;

            int offset = Variants.IndexOf(CurrentVariant) + 1;

            for (int variantIndex = 0; variantIndex <= Variants.Count; variantIndex++)
            {
                int offsetIndex = (variantIndex + offset) % Variants.Count;

                string nextVariant = Variants[offsetIndex];

                if (CheckIfCanForceValid(craftingStand, nextVariant))
                {
                    SetCurrentShapeVariant(stack, nextVariant);
                    CurrentVariant = nextVariant;
                    ForceValid(craftingStand);
                    return true;
                }
            }

            return false;
        }

        public bool CheckIfValid(ILayeredContainer craftingStand) => CheckIfValid(craftingStand, CurrentVariant);

        public bool CheckIfCanForceValid(ILayeredContainer craftingStand, string currentVariant)
        {
            if (currentVariant == "") return true;

            string[] forcedSlots = [];
            foreach ((string variantWildcard, string[] forcedSlotsForVariant) in ConnectedVariants)
            {
                if (WildcardUtil.Match(variantWildcard, currentVariant))
                {
                    forcedSlots = forcedSlotsForVariant;
                }
            }

            if (forcedSlots.Length == 0) return true;

            foreach (string forcedSlotCode in forcedSlots)
            {
                if (forcedSlotCode == CurrentSlotCode) continue;

                craftingStand.GetFirstSlotBy(forcedSlotCode, CurrentLayerCode, out ItemSlot? forcedSlot, out WearableBitSlotConfig? forcedConfig);

                if (forcedSlot?.Itemstack == null || forcedConfig == null) return false;

                BitShapeVariantsForSlot forcedSlotVariants = FromItemStiack(forcedSlot.Itemstack, forcedConfig);

                if (!forcedSlotVariants.Variants.Contains(currentVariant)) return false;
            }

            return true;
        }

        public bool ForceValid(ILayeredContainer craftingStand)
        {
            string[] forcedSlots = [];
            foreach ((string variantWildcard, string[] forcedSlotsForVariant) in ConnectedVariants)
            {
                if (WildcardUtil.Match(variantWildcard, CurrentVariant))
                {
                    forcedSlots = forcedSlotsForVariant;
                }
            }

            if (forcedSlots.Length == 0) return true;

            foreach (string forcedSlotCode in forcedSlots)
            {
                if (forcedSlotCode == CurrentSlotCode) continue;

                craftingStand.GetFirstSlotBy(forcedSlotCode, CurrentLayerCode, out ItemSlot? forcedSlot, out WearableBitSlotConfig? forcedConfig);

                if (forcedSlot?.Itemstack == null || forcedConfig == null) continue;

                BitShapeVariantsForSlot forcedSlotVariants = BitShapeVariantsForSlot.FromItemStiack(forcedSlot.Itemstack, forcedConfig);

                forcedSlotVariants.SetCurrentShapeVariant(forcedSlot.Itemstack, CurrentVariant);

                forcedSlot.MarkDirty();
            }

            return true;
        }

        public bool CheckIfValid(ILayeredContainer craftingStand, string currentVariant)
        {
            if (currentVariant == "") return true;

            string[] forcedSlots = [];
            foreach ((string variantWildcard, string[] forcedSlotsForVariant) in ConnectedVariants)
            {
                if (WildcardUtil.Match(variantWildcard, currentVariant))
                {
                    forcedSlots = forcedSlotsForVariant;
                }
            }

            if (forcedSlots.Length == 0) return true;

            foreach (string forcedSlotCode in forcedSlots)
            {
                if (forcedSlotCode == CurrentSlotCode) continue;

                craftingStand.GetFirstSlotBy(forcedSlotCode, CurrentLayerCode, out ItemSlot? forcedSlot, out WearableBitSlotConfig? forcedConfig);

                if (forcedSlot?.Itemstack == null || forcedConfig == null) return false;

                string forcedSlotShapeVariant = GetCurrentShapeVariant(forcedSlot.Itemstack);
                if (forcedSlotShapeVariant != CurrentVariant)
                {
                    return false;
                }
            }

            return true;
        }




        public static BitShapeVariantsForSlot FromItemStiack(ItemStack stack, WearableBitSlotConfig slotConfig)
        {
            BitShapeVariantsForSlot result = new();

            string slotCode = slotConfig.Code;
            string layerCode = slotConfig.Layer;

            BitShapeVariantConnectedSlotsEntry[] connectedSlots = stack.ItemAttributes?["attachableToEntity"]?["connectedShapeVariants"]?.AsObject<BitShapeVariantConnectedSlotsEntry[]>() ?? [];
            foreach (BitShapeVariantConnectedSlotsEntry entry in connectedSlots)
            {
                if (result.ConnectedVariants.ContainsKey(entry.Variant)) continue;

                foreach (string connectedSlotCode in entry.Slots)
                {
                    if (WildcardUtil.Match(connectedSlotCode, slotCode))
                    {
                        result.ConnectedVariants[entry.Variant] = entry.Slots;
                        break;
                    }
                }
            }

            string[]? attachedShapeVariants = stack.ItemAttributes?["attachableToEntity"]?["attachedShapeVariantsBySlotCode"]?.FirstByWildcardKey(slotCode)?.AsArray<string>()?.OfType<string>().ToArray();
            if (attachedShapeVariants == null) slotCode = "";
            attachedShapeVariants ??= stack.ItemAttributes?["attachableToEntity"]?["attachedShapeVariants"]?.AsArray<string>()?.OfType<string>().ToArray();
            if (attachedShapeVariants != null && attachedShapeVariants.Length > 0)
            {
                result.Variants = attachedShapeVariants.ToList();
            }
            
            result.CurrentSlotCode = slotCode;
            result.CurrentLayerCode = layerCode;
            result.CurrentVariant = result.GetCurrentShapeVariant(stack);

            return result;
        }
    }

    /// <summary>
    /// Tool for chaning shape and texture variant of armor bit on crafting stand
    /// </summary>
    public class CollectibleBehaviorArmorShapeVariantFlipperTool : CollectibleBehaviorArmorTool, IArmorVariantFlipperTool
    {
        public CollectibleBehaviorArmorShapeVariantFlipperTool(CollectibleObject collObj) : base(collObj)
        {
        }

        public bool CanFlipVariant(ItemSlot toolSlot, ItemSlot bitSlot, EntityAgent byEntity, WearableBitSlotConfig slotConfig, ILayeredContainer craftingStand)
        {
            return Supported(toolSlot, [bitSlot], byEntity, craftingStand);
        }

        public void FlipVariant(ItemSlot toolSlot, ItemSlot bitSlot, EntityAgent byEntity, WearableBitSlotConfig slotConfig, ILayeredContainer craftingStand)
        {
            FlipBitVariant(toolSlot, bitSlot, byEntity, slotConfig, craftingStand);

            OnUsed(toolSlot, [bitSlot], byEntity, craftingStand);
        }

        public static void FlipBitVariant(ItemSlot toolSlot, ItemSlot bitSlot, EntityAgent byEntity, WearableBitSlotConfig slotConfig, ILayeredContainer craftingStand)
        {
            if (bitSlot.Itemstack == null) return;

            BitShapeVariantsForSlot variants = BitShapeVariantsForSlot.FromItemStiack(bitSlot.Itemstack, slotConfig);

            bool flipped = variants.FlipVariant(bitSlot.Itemstack, craftingStand);

            if (flipped)
            {
                bitSlot.MarkDirty();

                //FlipAllInvalid(craftingStand, skipSlot: bitSlot);
            }
        }

        public static void FlipAllInvalid(ILayeredContainer craftingStand, ItemSlot? skipSlot = null)
        {
            WearableBitSlotConfig[] configs = craftingStand.GetSlotConfigs();
            for (int slotIndex = 0; slotIndex < configs.Length; slotIndex++)
            {
                ItemSlot slot = craftingStand.Inventory[slotIndex];

                if (slot == skipSlot || slot.Itemstack == null) continue;

                WearableBitSlotConfig config = configs[slotIndex];

                BitShapeVariantsForSlot variants = BitShapeVariantsForSlot.FromItemStiack(slot.Itemstack, config);

                if (!variants.CheckIfValid(craftingStand))
                {
                    bool flipped = variants.FlipVariant(slot.Itemstack, craftingStand);
                    if (!flipped)
                    {
                        variants.UnsetCurrentShapeVariant(slot.Itemstack);
                    }
                    slot.MarkDirty();
                }
            }
        }

        public static void Unflip(ItemSlot bitSlot)
        {
            bitSlot.Itemstack?.Attributes?.RemoveAttribute("shapeVariant");
            bitSlot.Itemstack?.Attributes?.RemoveAttribute("shapeVariantType");
            bitSlot.MarkDirty();
        }
    }

    public class CollectibleBehaviorArmorTextureVariantFlipperTool : CollectibleBehaviorArmorTool, IArmorVariantFlipperTool
    {
        public CollectibleBehaviorArmorTextureVariantFlipperTool(CollectibleObject collObj) : base(collObj)
        {
        }

        public bool CanFlipVariant(ItemSlot toolSlot, ItemSlot bitSlot, EntityAgent byEntity, WearableBitSlotConfig slotConfig, ILayeredContainer craftingStand)
        {
            return Supported(toolSlot, [bitSlot], byEntity, craftingStand);
        }

        public void FlipVariant(ItemSlot toolSlot, ItemSlot bitSlot, EntityAgent byEntity, WearableBitSlotConfig slotConfig, ILayeredContainer craftingStand)
        {
            FlipBitVariant(bitSlot, byEntity, slotConfig, craftingStand);

            OnUsed(toolSlot, [bitSlot], byEntity, craftingStand);
        }

        public static void FlipBitVariant(ItemSlot bitSlot, EntityAgent byEntity, WearableBitSlotConfig slotConfig, ILayeredContainer craftingStand)
        {
            string slotCode = slotConfig.Code;

            string[]? attachedShapeVariants = bitSlot.Itemstack?.ItemAttributes?["attachableToEntity"]?["attachedTextureVariantsBySlotCode"]?[slotConfig.Code]?.AsArray<string>()?.OfType<string>().ToArray();
            if (attachedShapeVariants == null) slotCode = "";
            attachedShapeVariants ??= bitSlot.Itemstack?.ItemAttributes?["attachableToEntity"]?["attachedTextureVariants"]?.AsArray<string>()?.OfType<string>().ToArray();
            if (attachedShapeVariants == null || attachedShapeVariants.Length <= 0) return;

            string currentShapeVariant = bitSlot.Itemstack?.Attributes?.GetString("textureVariant") ?? attachedShapeVariants[0];
            int currentShapeVariantIndex = attachedShapeVariants.IndexOf(currentShapeVariant);
            if (currentShapeVariantIndex < 0)
            {
                currentShapeVariant = attachedShapeVariants[0];
            }
            else
            {
                currentShapeVariantIndex = (currentShapeVariantIndex + 1) % attachedShapeVariants.Length;
                currentShapeVariant = attachedShapeVariants[currentShapeVariantIndex];
            }

            bitSlot.Itemstack?.Attributes?.SetString("textureVariant", currentShapeVariant);
            bitSlot.Itemstack?.Attributes?.SetString("textureVariantType", slotCode);

            bitSlot.MarkDirty();
        }

        public static void TryFlipBitVariantToDefault(ItemSlot bitSlot, EntityAgent byEntity, WearableBitSlotConfig slotConfig, ILayeredContainer craftingStand)
        {
            string? currentVariant = bitSlot.Itemstack?.Attributes?.GetString("textureVariant");
            if (currentVariant == null)
            {
                FlipBitVariant(bitSlot, byEntity, slotConfig, craftingStand);
            }
        }

        public static void Unflip(ItemSlot bitSlot)
        {
            bitSlot.Itemstack?.Attributes?.RemoveAttribute("textureVariant");
            bitSlot.Itemstack?.Attributes?.RemoveAttribute("textureVariantType");
            bitSlot.MarkDirty();
        }
    }
}
