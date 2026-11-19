using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;


namespace Vintagestory.GameContent
{
    /// <summary>
    /// Wearable attachment that is combined from multiple attached pieces stored in item stack attributes.
    /// </summary>
    public class CollectibleBehaviorCombinedWearable : CollectibleBehavior, ICombinedWearable
    {
        public CollectibleBehaviorCombinedWearable(CollectibleObject collObj) : base(collObj)
        {
        }


        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            this.api = api;
        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            if (withDebugInfo)
            {
                if (inSlot.Itemstack != null)
                {
                    dsc.AppendLine($"Cache key: {GetBitsCacheKey(inSlot.Itemstack)}");
                }
            }

            dsc.AppendLineOnce();
            dsc.Append(getItemDescrition(inSlot, world, withDebugInfo));
        }

        public override int GetMaxDurability(ItemStack itemstack, int durability, ref EnumHandling bhHandling)
        {
            bhHandling = EnumHandling.PreventSubsequent;

            return getMaxDurabiltiyFromBits(itemstack);
        }

        public override void OnDamageItem(IWorldAccessor world, Entity byEntity, ItemSlot itemslot, ref int amount, ref EnumHandling bhHandling)
        {
            bhHandling = EnumHandling.PreventDefault;

            amount = 0;

            // to affect combined wearable durability one should retrieve bits, change heir durabiltiy and then store it
        }


        public virtual int GetBitsCacheKey(ItemStack carrier) => carrier.Attributes.GetInt("combinedWearableHashCode", defaultValue: 0);

        public virtual IEnumerable<WearableBitEntry> RetrieveBits(ItemStack carrier)
        {
            ITreeAttribute armorTree = carrier.Attributes.GetOrAddTreeAttribute("combinedWearable");

            List<WearableBitEntry> result = [];

            if (api == null) return result;

            foreach ((string attributeName, IAttribute attribute) in armorTree)
            {
                if (attribute is not ITreeAttribute slotTree) continue;

                WearableBitEntry entry = new(slotTree, api.World);

                entry.Stack.StackSize = 1;

                result.Add(entry);
            }

            return result;
        }

        public virtual void StoreBits(ItemStack carrier, IEnumerable<WearableBitEntry> bits)
        {
            ITreeAttribute armorTree = carrier.Attributes.GetOrAddTreeAttribute("combinedWearable");

            foreach ((string attributeName, IAttribute attribute) in armorTree)
            {
                if (attribute is not ITreeAttribute slotTree) continue;

                armorTree.RemoveAttribute(attributeName);
            }

            int pieceIndex = 0;
            foreach (WearableBitEntry entry in bits)
            {
                string attributeCode = (pieceIndex++).ToString();

                int pieceStackDurability = entry.Stack.Collectible.GetRemainingDurability(entry.Stack);

                ITreeAttribute slotTree = armorTree.GetOrAddTreeAttribute(attributeCode);
                entry.Store(slotTree);
                slotTree.SetBool("broken", pieceStackDurability <= 0);
            }

            updateBitsCacheKey(carrier);

            int durability = bits.Select(bit => bit.Stack.Collectible.GetRemainingDurability(bit.Stack)).Sum();

            carrier.Collectible.SetDurability(carrier, durability);
        }

        public virtual void CombineBits(ItemStack carrier, ILayeredContainer container, Entity byEntity, IEnumerable<int> fromSlots, bool consumeBits)
        {
            Dictionary<string, Dictionary<string, float>> hitChancesByLayerBySlot = getHitChances(container, fromSlots);

            string[] layerCodes = container.GetLayerCodes();
            WearableBitSlotConfig[] slotConfigs = container.GetSlotConfigs();
            IInventory inventory = container.Inventory;

            List<int> collectedSlots = [];
            List<WearableBitEntry> bits = [];

            for (int inventoryIndex = 0; inventoryIndex < inventory.Count; inventoryIndex++)
            {
                ItemStack? bitStack = inventory[inventoryIndex].Itemstack;

                if (bitStack == null) continue;

                WearableBitSlotConfig bitSlotConfig = slotConfigs[inventoryIndex];

                if (fromSlots.Any() && !fromSlots.Contains(inventoryIndex)) continue;

                string slotCode = bitSlotConfig.Code;
                string layerCode = bitSlotConfig.Layer;
                int layerIndex = layerCodes.IndexOf(layerCode);
                float hitChanceFactor = getHitChanceFactor(bitStack, slotCode);
                float hitChance = hitChancesByLayerBySlot[slotCode][layerCode] * hitChanceFactor;

                WearableBitEntry entry = new()
                {
                    Stack = bitStack,
                    SlotCode = slotCode,
                    LayerCode = layerCode,
                    HitChance = hitChance,
                    LayerIndex = layerIndex
                };

                bits.Add(entry);

                collectedSlots.Add(inventoryIndex);
            }

            StoreBits(carrier, bits);

            if (consumeBits)
            {
                foreach (int slotIndex in collectedSlots)
                {
                    inventory[slotIndex].TakeOutWhole();
                    inventory[slotIndex].MarkDirty();
                }
            }
        }

        public virtual void UncombineBits(ItemStack carrier, ILayeredContainer container, Entity byEntity)
        {
            IEnumerable<WearableBitEntry> bits = RetrieveBits(carrier);

            bits = unflipBits(bits, byEntity);

            bits = breakBitsWithNoDurability(bits, byEntity);

            uncombineBitsIntoInventory(bits, container, out List<ItemSlot> uncombinedBits, out List<ItemStack> bitsToDrop);

            foreach (ItemSlot bitSlot in uncombinedBits)
            {
                bitSlot.MarkDirty();
            }

            foreach (ItemStack bitStack in bitsToDrop)
            {
                if (!byEntity.TryGiveItemStack(bitStack))
                {
                    api?.World.SpawnItemEntity(bitStack, byEntity.Pos.AsBlockPos);
                }
            }
        }



        protected ICoreAPI? api { get; set; }

        protected Dictionary<int, int> maxDurabilitiesCache { get; set; } = [];

        protected Dictionary<int, string> itemDesciptionCache { get; set; } = [];

        protected bool previousDescriptionWithDebugInfo { get; set; } = false;

        protected float heavilyDamagedBitsDuravilityThreshold { get; set; } = 0.5f;



        protected virtual bool updateBitsCacheKey(ItemStack carrier)
        {
            ITreeAttribute armorTree = carrier.Attributes.GetOrAddTreeAttribute("combinedWearable");

            int previousCacheCode = carrier.Attributes.GetInt("combinedWearableHashCode");
            int newCacheCode = armorTree.GetHashCode(["durability"]);

            if (previousCacheCode != newCacheCode)
            {
                carrier.Attributes.SetInt("combinedWearableHashCode", newCacheCode);
                return true;
            }
            else
            {
                return false;
            }
        }

        protected virtual void uncombineBitsIntoInventory(IEnumerable<WearableBitEntry> bits, ILayeredContainer container, out List<ItemSlot> uncombinedBits, out List<ItemStack> bitsToDrop)
        {
            WearableBitSlotConfig[] slotConfigs = container.GetSlotConfigs();

            uncombinedBits = [];
            bitsToDrop = [];

            foreach (WearableBitEntry entry in bits)
            {
                int slotIndex = getSlotIndexByCodes(slotConfigs, entry.SlotCode, entry.LayerCode);

                if (slotIndex < 0 || slotIndex >= container.Inventory.Count || !container.Inventory[slotIndex].Empty)
                {
                    bitsToDrop.Add(entry.Stack);
                    continue;
                }

                uncombinedBits.Add(container.Inventory[slotIndex]);
                container.Inventory[slotIndex].Itemstack = entry.Stack;
                container.Inventory[slotIndex].MarkDirty();
            }
        }

        protected virtual IEnumerable<WearableBitEntry> breakBitsWithNoDurability(IEnumerable<WearableBitEntry> bits, Entity byEntity)
        {
            DummySlot temporarySlot = new();
            List<WearableBitEntry> remainingItems = [];
            foreach (WearableBitEntry entry in bits)
            {
                int durability = entry.Stack.Collectible.GetRemainingDurability(entry.Stack);
                int maxDurability = entry.Stack.Collectible.GetMaxDurability(entry.Stack);

                if (durability > 0 || maxDurability <= 0)
                {
                    remainingItems.Add(entry);
                    continue;
                }

                temporarySlot.Itemstack = entry.Stack;
                entry.Stack.Collectible.DamageItem(api?.World, byEntity, temporarySlot, 1, destroyOnZeroDurability: true);
            }

            return remainingItems;
        }

        protected virtual int getMaxDurabiltiyFromBits(ItemStack carrier)
        {
            if (api == null) return 0;

            int cacheKey = GetBitsCacheKey(carrier);

            if (maxDurabilitiesCache.TryGetValue(cacheKey, out int maxDurability))
            {
                return maxDurability;
            }

            IEnumerable<WearableBitEntry> pieces = RetrieveBits(carrier);

            if (!pieces.Any()) return 0;

            maxDurability = pieces.Select(bit => bit.Stack.Collectible.GetMaxDurability(bit.Stack)).Sum();

            maxDurabilitiesCache[cacheKey] = maxDurability;

            return maxDurability;
        }

        protected virtual float getHitChanceFactor(ItemStack bit, string slotCode)
        {
            float coverageFactor = bit.ItemAttributes?["attachableToEntity"]?["coverageFactor"]?.AsFloat(1) ?? 1;

            JsonObject? coverageFactorBySlotCode = bit.ItemAttributes?["attachableToEntity"]?["coverageFactorBySlotCode"]?.FirstByWildcardKey(slotCode);

            if (coverageFactorBySlotCode != null)
            {
                coverageFactor = coverageFactorBySlotCode.AsFloat(coverageFactor);
            }

            return coverageFactor;
        }

        protected virtual string getItemDescrition(ItemSlot inSlot, IWorldAccessor world, bool withDebugInfo)
        {
            if (inSlot.Itemstack == null) return "";

            if (previousDescriptionWithDebugInfo != withDebugInfo)
            {
                itemDesciptionCache.Clear();
                previousDescriptionWithDebugInfo = withDebugInfo;
            }

            // commented out cause otherwise durability will not update, because it is not taken into account in cache key
            /*int cacheKey = GetBitsCacheKey(inSlot.Itemstack);

            if (itemDesciptionCache.TryGetValue(cacheKey, out string? description))
            {
                return description;
            }*/

            StringBuilder dsc = new();

            IEnumerable<WearableBitEntry> pieces = RetrieveBits(inSlot.Itemstack);
            int totalPieces = pieces.Count();
            int damagedPieces = 0;
            int veryDamagedPieces = 0;
            int brokenPieces = 0;
            string mostDamagedPiece = "";
            int lowestDurability = int.MaxValue;
            foreach (WearableBitEntry entry in pieces)
            {
                int durability = entry.Stack.Collectible.GetRemainingDurability(entry.Stack);
                int maxDurabiltiy = entry.Stack.Collectible.GetMaxDurability(entry.Stack);

                if (durability == maxDurabiltiy) continue;

                if (durability == 0)
                {
                    brokenPieces++;
                }
                else
                {
                    damagedPieces++;
                    if ((float)durability/ maxDurabiltiy < heavilyDamagedBitsDuravilityThreshold)
                    {
                        veryDamagedPieces++;
                    }
                    if (lowestDurability > durability)
                    {
                        lowestDurability = durability;
                        string slotCode = Lang.Get($"armor-slot-{entry.SlotCode}");
                        string layerCode = Lang.Get($"armor-layer-{entry.LayerCode}");
                        mostDamagedPiece = $"{slotCode} ({layerCode}): {durability}/{maxDurabiltiy}";
                    }
                }
            }

            if (damagedPieces > 0 || veryDamagedPieces > 0 || brokenPieces > 0)
            {
                dsc.AppendLine();
                dsc.AppendLine(Lang.Get("combinedwearable-description-bitsdurability-header"));
                if (damagedPieces > 0) dsc.AppendLine(Lang.Get("combinedwearable-description-bitsdurability-damagedbits", damagedPieces, totalPieces));
                if (veryDamagedPieces > 0) dsc.AppendLine(Lang.Get("combinedwearable-description-bitsdurability-heavilydamagedbits", veryDamagedPieces, totalPieces, heavilyDamagedBitsDuravilityThreshold));
                if (brokenPieces > 0) dsc.AppendLine(Lang.Get("combinedwearable-description-bitsdurability-brokenbits", brokenPieces, totalPieces));
                if (mostDamagedPiece != "") dsc.AppendLine(Lang.Get("combinedwearable-description-bitsdurability-mostdamagedbit", mostDamagedPiece));
            }

            string result = dsc.ToString();

            //itemDesciptionCache[cacheKey] = result;

            return result;
        }

        protected virtual IEnumerable<WearableBitEntry> unflipBits(IEnumerable<WearableBitEntry> bits, Entity wearer)
        {
            List<WearableBitEntry> resultingBits = [];
            foreach (WearableBitEntry bit in bits)
            {
                IFlippableWearable? flippable = bit.Stack.Collectible.GetCollectibleInterface<IFlippableWearable>();

                if (flippable == null)
                {
                    resultingBits.Add(bit);
                    continue;
                }

                DummySlot dummySlot = new(bit.Stack);

                bool flipped = flippable.Unflip(wearer, dummySlot);

                if (flipped)
                {
                    if (dummySlot.Itemstack == null) continue;
                    WearableBitEntry newBit = bit;
                    newBit.Stack = dummySlot.Itemstack;
                    resultingBits.Add(newBit);
                }
                else
                {
                    resultingBits.Add(bit);
                }
            }

            return resultingBits;
        }

        protected virtual Dictionary<string, Dictionary<string, float>> getHitChances(ILayeredContainer container, IEnumerable<int> slotIndexes)
        {
            WearableBitSlotConfig[] configs = container.GetSlotConfigs();
            Dictionary<string, Dictionary<string, int>> coverages = [];
            Dictionary<string, int> totalCoverage = [];

            foreach (int slotIndex in slotIndexes)
            {
                WearableBitSlotConfig config = configs[slotIndex];

                string slotCode = config.Code;
                string layerCode = config.Layer;
                int coverage = configs[slotIndex].Coverage;

                if (!totalCoverage.ContainsKey(layerCode)) totalCoverage[layerCode] = 0;
                totalCoverage[layerCode] += coverage;
                if (!coverages.ContainsKey(slotCode)) coverages[slotCode] = [];
                coverages[slotCode][layerCode] = coverage;
            }

            Dictionary<string, Dictionary<string, float>> hitChances = coverages
                .ToDictionary(entry => entry.Key, entry => entry.Value
                    .ToDictionary(entry => entry.Key, entry => (float)entry.Value / totalCoverage[entry.Key]));

            return hitChances;
        }

        protected virtual int getSlotIndexByCodes(WearableBitSlotConfig[] slotConfigs, string slotCode, string layerCode)
        {
            for (int slotIndex = 0; slotIndex < slotConfigs.Length; slotIndex++)
            {
                WearableBitSlotConfig config = slotConfigs[slotIndex];

                if (config.Code == slotCode && config.Layer == layerCode)
                {
                    return slotIndex;
                }
            }

            return -1;
        }
    }
}
