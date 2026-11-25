using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Util;


namespace Vintagestory.GameContent
{
    public class CollectibleBehaviorCombinedArmor : CollectibleBehavior, ICustomArmor
    {
        public CollectibleBehaviorCombinedArmor(CollectibleObject collObj) : base(collObj)
        {
        }



        protected ICoreAPI? api { get; set; }

        protected Dictionary<int, Dictionary<string, float>> hitChancesCache { get; set; } = [];

        protected Dictionary<int, Dictionary<string, ProtectionModifiers>> protectionModifiersCacheBySlot { get; set; } = [];

        protected Dictionary<int, ProtectionModifiers> protectionModifiersCache { get; set; } = [];

        protected Dictionary<int, string> itemDesciptionCache { get; set; } = [];



        public override void OnDamageItem(IWorldAccessor world, Entity byEntity, ItemSlot itemslot, ref int amount, ref EnumHandling bhHandling)
        {
            bhHandling = EnumHandling.PreventSubsequent;

            // Do not damage item, durability is handled via ICustomArmor interface
        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            string descTextToAppend = "";
            string descText = collObj.GetItemDescText();
            if (descText.Length > 1)
            {
                int descIndex = dsc.ToString().IndexOfOrdinal(descText);
                if (descIndex > 0)
                {
                    if (descIndex > 0) descIndex--;
                    else descTextToAppend = "\n";
                    descTextToAppend += dsc.ToString(descIndex, dsc.Length - descIndex);
                    dsc.Remove(descIndex, dsc.Length - descIndex);
                }
            }

            dsc.Append(getItemDescrition(inSlot, world, withDebugInfo));

            dsc.Append(descTextToAppend);
        }

        public override void OnLoaded(ICoreAPI api)
        {
            this.api = api;
        }


        public virtual ProtectionModifiers GetProtectionModifiers(ItemSlot slot, out string hitToken)
        {
            ICombinedWearable? combinedWearable = slot.Itemstack?.Collectible?.GetCollectibleInterface<ICombinedWearable>();

            hitToken = "";

            if (combinedWearable == null) return new();

            ProtectionModifiers defaultModifiers = new();

            if (slot.Itemstack == null || api == null) return defaultModifiers;

            IEnumerable<WearableBitEntry> pieces = hitRandomSlot(slot, combinedWearable, out string? targetSlotCode);

            if (targetSlotCode == null) return defaultModifiers;

            hitToken = targetSlotCode;

            int attributesHash = combinedWearable.GetBitsCacheKey(slot.Itemstack);
            return getProtectionModifiersForSlot(pieces, attributesHash);
        }

        public virtual void DamageArmor(IWorldAccessor world, Entity byEntity, ItemSlot armorSlot, int durabilityDamage, string hitToken)
        {
            ItemStack? carrier = armorSlot.Itemstack;
            ICombinedWearable? combinedWearable = carrier?.Collectible?.GetCollectibleInterface<ICombinedWearable>();

            if (carrier == null || combinedWearable == null) return;

            IEnumerable<WearableBitEntry> pieces = combinedWearable.RetrieveBits(carrier);

            int[] layerIndexesInOrder = pieces.Select(entry => entry.LayerIndex).Distinct().Order().ToArray();
            Dictionary<int, int> layerIndexToLayerWeight = [];
            for (int layerOrderIndex = 0; layerOrderIndex < layerIndexesInOrder.Length; layerOrderIndex++)
            {
                layerIndexToLayerWeight[layerIndexesInOrder[layerOrderIndex]] = layerOrderIndex + 1;
            }

            int durabilityFractionDivider = pieces.Where(entry => entry.SlotCode == hitToken).Sum(entry => layerIndexToLayerWeight[entry.LayerIndex]);

            if (durabilityFractionDivider <= 0) return;

            DummySlot pieceTemporarySlot = new();

            int remainingDurabilityDamage = durabilityDamage;

            List<WearableBitEntry> updatePieces = [];

            IEnumerable<WearableBitEntry> piecesShuffled = pieces.Shuffle();

            foreach (WearableBitEntry entry in piecesShuffled)
            {
                if (remainingDurabilityDamage <= 0 || entry.SlotCode != hitToken)
                {
                    updatePieces.Add(entry);
                    continue;
                }

                int layerWeight = layerIndexToLayerWeight[entry.LayerIndex];
                int durabilityDamagePerPiece = Math.Max(1, durabilityDamage * layerWeight / durabilityFractionDivider);

                durabilityDamagePerPiece = Math.Min(durabilityDamagePerPiece, remainingDurabilityDamage);

                remainingDurabilityDamage -= durabilityDamagePerPiece;

                pieceTemporarySlot.Itemstack = entry.Stack;
                entry.Stack.Collectible.DamageItem(world, byEntity, pieceTemporarySlot, durabilityDamagePerPiece, destroyOnZeroDurability: false);

                if (pieceTemporarySlot.Itemstack != null)
                {
                    WearableBitEntry updatedEntry = entry;
                    updatedEntry.Stack = pieceTemporarySlot.Itemstack;
                    updatePieces.Add(updatedEntry);
                }
            }

            combinedWearable.StoreBits(carrier, updatePieces);

            // so durability values update even if no bits are broken, durability is ignored for cache key generation
            int cacheKey = combinedWearable.GetBitsCacheKey(carrier);
            itemDesciptionCache.Remove(cacheKey);

            armorSlot.MarkDirty();
        }



        protected virtual IEnumerable<WearableBitEntry> hitRandomSlot(ItemSlot slot, ICombinedWearable combinedWearable, out string? hitSlotCode)
        {
            hitSlotCode = null;

            if (slot.Itemstack == null || api == null) return [];

            double randomNumber = (float)api.World.Rand.NextDouble();

            Dictionary<string, float> hitChances = getHitChances(slot.Itemstack, combinedWearable);

            float cumulativeHitChace = 0;
            string? targetSlotCode = null;
            foreach ((string slotCode, float hitChance) in hitChances)
            {
                cumulativeHitChace += hitChance;

                if (randomNumber < cumulativeHitChace)
                {
                    targetSlotCode = slotCode;
                    break;
                }
            }

            if (targetSlotCode == null) return [];

            hitSlotCode = targetSlotCode;

            IEnumerable<WearableBitEntry> bits = combinedWearable.RetrieveBits(slot.Itemstack);

            return bits;
        }

        protected virtual Dictionary<string, float> getHitChances(ItemStack carrier, ICombinedWearable combinedWearable)
        {
            if (api == null) return [];

            int hashKey = combinedWearable.GetBitsCacheKey(carrier);

            if (hitChancesCache.TryGetValue(hashKey, out Dictionary<string, float>? result))
            {
                return result;
            }

            IEnumerable<WearableBitEntry> pieces = retrieveBitsWithExtraSlots(carrier, combinedWearable);

            result = [];

            foreach (WearableBitEntry entry in pieces)
            {
                if (result.TryGetValue(entry.SlotCode, out float chance))
                {
                    result[entry.SlotCode] = Math.Max(entry.HitChance, chance);
                }
                else
                {
                    result[entry.SlotCode] = entry.HitChance;
                }
            }

            hitChancesCache[hashKey] = result;

            return result;
        }

        protected virtual IEnumerable<WearableBitEntry> retrieveBitsWithExtraSlots(ItemStack carrier, ICombinedWearable combinedWearable)
        {
            IEnumerable<WearableBitEntry> pieces = combinedWearable.RetrieveBits(carrier);

            List<WearableBitEntry> extraSlotPieces = [];

            foreach (WearableBitEntry entry in pieces)
            {
                IEnumerable<string> extraOccupiedSlots = getBitExtraSlots(carrier, entry.SlotCode);

                foreach (string extraSlotCode in extraOccupiedSlots)
                {
                    WearableBitEntry extraSlotEntry = entry;
                    extraSlotEntry.SlotCode = extraSlotCode;
                    extraSlotPieces.Add(extraSlotEntry);
                }
            }

            pieces = pieces.Concat(extraSlotPieces);

            return pieces.OrderBy(entry => entry.SlotCode);
        }

        protected virtual IEnumerable<string> getBitExtraSlots(ItemStack carrier, string slotCode)
        {
            return carrier.ItemAttributes["attachableToEntity"]?["extraOccupiedSlotsBySlotCode"]?.FirstByWildcardKey(slotCode)
                ?.AsArray<string>()
                ?.OfType<string>()
                .Where(code => code != slotCode)
                .Distinct() ?? [];
        }

        protected virtual ProtectionModifiers getProtectionModifiersForSlot(IEnumerable<WearableBitEntry> bits, int attributesHash)
        {
            if (protectionModifiersCache.TryGetValue(attributesHash, out ProtectionModifiers? cachedModifiers))
            {
                return cachedModifiers;
            }

            IEnumerable<string> layers = bits.Select(bit => bit.LayerCode).Distinct();

            Dictionary<string, ProtectionModifiers> modifiersByLayer = layers.ToDictionary(layer => layer, _ => new ProtectionModifiers());

            foreach ((string layer, ProtectionModifiers layerProtection) in modifiersByLayer)
            {
                List<(ProtectionModifiers modifiers, float weight)> bitsModifiers = [];

                foreach (WearableBitEntry entry in bits.Where(bit => bit.LayerCode == layer))
                {
                    if (entry.Stack.Collectible?.GetCollectibleBehavior<CollectibleBehaviorArmorBitStats>(true) is not CollectibleBehaviorArmorBitStats bitModifiersProvider) continue;

                    ProtectionModifiers bitModifiers = bitModifiersProvider.GetArmorBitProtectionModifiers(entry.Stack, entry.SlotCode, entry.LayerCode) ?? new();

                    int durability = entry.Stack.Collectible.GetRemainingDurability(entry.Stack);

                    if (durability <= 0)
                    {
                        bitsModifiers.Add((new ProtectionModifiers(), entry.HitChance));
                    }
                    else
                    {
                        bitsModifiers.Add((bitModifiers, entry.HitChance));
                    }
                }

                modifiersByLayer[layer] = ProtectionModifiers.Average(bitsModifiers.ToArray());
            }

            ProtectionModifiers result = ProtectionModifiers.Add(modifiersByLayer.Values.ToArray());

            protectionModifiersCache[attributesHash] = result;

            return result;
        }

        protected virtual ProtectionModifiers getProtectionModifiers(ItemSlot slot)
        {
            ICombinedWearable? combinedWearable = slot.Itemstack?.Collectible?.GetCollectibleInterface<ICombinedWearable>();

            if (slot.Itemstack == null || api == null || combinedWearable == null)
            {
                return new();
            }

            IEnumerable<WearableBitEntry> pieces = retrieveBitsWithExtraSlots(slot.Itemstack, combinedWearable);

            if (!pieces.Any()) return new();

            int attributesHash = combinedWearable.GetBitsCacheKey(slot.Itemstack);

            return getProtectionModifiersForSlot(pieces, attributesHash);
        }

        protected virtual string getItemDescrition(ItemSlot inSlot, IWorldAccessor world, bool withDebugInfo)
        {
            ICombinedWearable? combinedWearable = inSlot.Itemstack?.Collectible?.GetCollectibleInterface<ICombinedWearable>();

            if (inSlot.Itemstack == null || combinedWearable == null) return "";

            int cacheKey = combinedWearable.GetBitsCacheKey(inSlot.Itemstack);

            if (itemDesciptionCache.TryGetValue(cacheKey, out string? description))
            {
                return description;
            }

            StringBuilder dsc = new();

            ProtectionModifiers protectionModifiers = getProtectionModifiers(inSlot);
            if (protectionModifiers.ProtectionTiers.Count > 0)
            {
                dsc.AppendLine();
                protectionModifiers.GetDescription(dsc, world.Api);
            }

            string result = dsc.ToString();

            itemDesciptionCache[cacheKey] = result;

            return result;
        }
    }
}
