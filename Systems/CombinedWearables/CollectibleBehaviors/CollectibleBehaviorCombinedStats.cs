using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;


namespace Vintagestory.GameContent
{
    public class CollectibleBehaviorCombinedStats : CollectibleBehavior, IWearableStatsSupplier
    {
        public CollectibleBehaviorCombinedStats(CollectibleObject collObj) : base(collObj)
        {
        }



        protected ICoreAPI? api { get; set; }

        protected Dictionary<int, StatModifiers> statModifiersCache { get; set; } = [];

        protected Dictionary<int, float> warmthCache { get; set; } = [];

        protected Dictionary<int, AssetLocation[]> footStepSoundsCache { get; set; } = [];

        protected Dictionary<int, string> itemDesciptionCache { get; set; } = [];

        protected bool isArmor { get; set; } = true;

        protected EnumCharacterDressType characterDressType { get; set; } = EnumCharacterDressType.Unknown;



        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            this.api = api;
            characterDressType = Enum.Parse<EnumCharacterDressType>(collObj.Attributes?["clothesCategory"]?.AsString() ?? "Unknown", ignoreCase: true);
            isArmor =
                characterDressType == EnumCharacterDressType.ArmorBody ||
                characterDressType == EnumCharacterDressType.ArmorHead ||
                characterDressType == EnumCharacterDressType.ArmorLegs;
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


        public virtual bool IsArmorType(ItemSlot slot) => isArmor;

        public virtual EnumCharacterDressType GetDressType(ItemSlot slot) => characterDressType;

        public virtual StatModifiers GetStatModifiers(ItemSlot slot)
        {
            StatModifiers result = new();
            if (slot.Itemstack == null || api == null)
            {
                return result;
            }

            ICombinedWearable? combinedWearable = slot.Itemstack.Collectible.GetCollectibleInterface<ICombinedWearable>();

            if (combinedWearable == null) return result;

            int attributesHash = combinedWearable.GetBitsCacheKey(slot.Itemstack);
            if (statModifiersCache.TryGetValue(attributesHash, out StatModifiers? cachedResult))
            {
                return cachedResult;
            }

            Dictionary<string, int> piecesCount = [];
            IEnumerable<WearableBitEntry> pieces = combinedWearable.RetrieveBits(slot.Itemstack);
            foreach (WearableBitEntry entry in pieces)
            {
                string code = entry.Stack.Collectible.Code;

                if (piecesCount.TryGetValue(code, out int pieceCount))
                {
                    piecesCount[code] += 1;
                    pieceCount++;
                }
                else
                {
                    piecesCount[code] = 1;
                    pieceCount = 1;
                }
            }

            foreach (WearableBitEntry entry in pieces)
            {
                if (entry.Stack.Collectible?.GetCollectibleBehavior<CollectibleBehaviorArmorBitStats>(true) is not CollectibleBehaviorArmorBitStats bitStatsProvider) continue;

                string carrierCode = slot.Itemstack.Collectible.Code;

                bitStatsProvider.GetStatModifiers(entry.Stack, entry.SlotCode, entry.LayerCode, carrierCode, out StatModifiers totalModifiers, out StatModifiers statsBySlotCode, out int maxPieces);

                bool broken = entry.Stack.Collectible.GetRemainingDurability(entry.Stack) == 0;

                result.AddFrom(statsBySlotCode, broken);

                string pieceCode = entry.Stack.Collectible.Code;

                if (!piecesCount.ContainsKey(pieceCode)) continue;
                int piecesNumber = piecesCount[pieceCode];
                piecesCount.Remove(pieceCode);

                float totalStatsFraction = (float)Math.Min(piecesNumber, maxPieces) / maxPieces;
                if (maxPieces == 0) totalStatsFraction = 1;

                result.AddFrom(totalModifiers, broken, multiplier: totalStatsFraction);
            }

            statModifiersCache[attributesHash] = result;

            return result;
        }

        public virtual ProtectionModifiers? GetProtectionModifiers(ItemSlot slot)
        {
            return new();
        }

        public virtual AssetLocation[] GetFootStepSounds(ItemSlot slot)
        {
            if (slot.Itemstack == null) return [];

            ICombinedWearable? combinedWearable = slot.Itemstack.Collectible.GetCollectibleInterface<ICombinedWearable>();

            if (combinedWearable == null) return [];

            int attributesHash = combinedWearable.GetBitsCacheKey(slot.Itemstack);
            if (footStepSoundsCache.TryGetValue(attributesHash, out AssetLocation[]? cachedResult))
            {
                return cachedResult;
            }

            Dictionary<string, float> sounds = [];
            IEnumerable<WearableBitEntry> pieces = combinedWearable.RetrieveBits(slot.Itemstack);
            foreach (WearableBitEntry entry in pieces)
            {
                (string path, float weight) = getFootStepSoundsPath(entry.Stack, entry.SlotCode);

                if (weight < 0) continue;

                if (sounds.ContainsKey(path))
                {
                    sounds[path] += weight;
                }
                else
                {
                    sounds[path] = weight;
                }
            }

            if (sounds.Count <= 0)
            {
                footStepSoundsCache[attributesHash] = [];
                return [];
            }

            float maxWeight = 0;
            string sound = "";

            foreach ((string soundPath, float weight) in sounds)
            {
                if (weight > maxWeight)
                {
                    maxWeight = weight;
                    sound = soundPath;
                }
            }

            AssetLocation loc = AssetLocation.Create(sound, collObj.Code.Domain).WithPathPrefixOnce("sounds/");

            AssetLocation[]? soundsLocations;
            if (sound.EndsWith('*'))
            {
                loc.Path = loc.Path.TrimEnd('*');
                soundsLocations = api?.Assets.GetLocations(loc.Path, loc.Domain).ToArray() ?? [];
            }
            else
            {
                soundsLocations = [loc];
            }

            footStepSoundsCache[attributesHash] = soundsLocations;

            return soundsLocations;
        }

        public virtual float GetMaxWarmth(ItemSlot slot)
        {
            float result = 0;
            if (slot.Itemstack == null || api == null)
            {
                return result;
            }

            ICombinedWearable? combinedWearable = slot.Itemstack.Collectible.GetCollectibleInterface<ICombinedWearable>();

            if (combinedWearable == null) return result;

            int attributesHash = combinedWearable.GetBitsCacheKey(slot.Itemstack);
            if (warmthCache.TryGetValue(attributesHash, out float cachedResult))
            {
                return cachedResult;
            }

            Dictionary<string, int> piecesCount = [];
            IEnumerable<WearableBitEntry> pieces = combinedWearable.RetrieveBits(slot.Itemstack);
            foreach (WearableBitEntry entry in pieces)
            {
                string code = entry.Stack.Collectible.Code;

                if (piecesCount.TryGetValue(code, out int pieceCount))
                {
                    piecesCount[code] += 1;
                    pieceCount++;
                }
                else
                {
                    piecesCount[code] = 1;
                    pieceCount = 1;
                }
            }

            foreach (WearableBitEntry entry in pieces)
            {
                string pieceCode = entry.Stack.Collectible.Code;
                string carrierCode = slot.Itemstack.Collectible.Code;

                if (!piecesCount.ContainsKey(pieceCode)) continue;
                int piecesNumber = piecesCount[pieceCode];
                piecesCount.Remove(pieceCode);

                float totalWarmth = entry.Stack.ItemAttributes?["attachableToEntity"]?["warmth"]?.FirstByWildcardKey(carrierCode)?.AsFloat() ?? 0;
                int maxPieces = entry.Stack.ItemAttributes?["attachableToEntity"]?["warmth"]?.FirstByWildcardKey(carrierCode)?["maxBits"]?.AsInt(0) ?? 0;

                float totalStatsFraction = (float)Math.Min(piecesNumber, maxPieces) / maxPieces;
                if (maxPieces == 0) totalStatsFraction = 1;

                result += totalWarmth * totalStatsFraction;
            }

            foreach (WearableBitEntry entry in pieces)
            {
                float warmthBySlotCode = entry.Stack.ItemAttributes?["attachableToEntity"]?["warmthBySlotCode"]?.FirstByWildcardKey(entry.SlotCode)?.AsFloat(0) ?? 0;

                result += warmthBySlotCode;
            }

            warmthCache[attributesHash] = result;

            return result;
        }


        protected virtual (string path, float weight) getFootStepSoundsPath(ItemStack stack, string slotCode)
        {
            string? path = stack.ItemAttributes?["attachableToEntity"]?["footStepSoundBySlotCode"]?.FirstByWildcardKey(slotCode)?.AsString();
            float weight = stack.ItemAttributes?["attachableToEntity"]?["footStepSoundWeightBySlotCode"]?.FirstByWildcardKey(slotCode)?.AsFloat() ?? 0;

            return (path ?? "", path == null ? -1 : weight);
        }

        protected virtual string getItemDescrition(ItemSlot inSlot, IWorldAccessor world, bool withDebugInfo)
        {
            if (inSlot.Itemstack == null) return "";

            ICombinedWearable? combinedWearable = inSlot.Itemstack.Collectible.GetCollectibleInterface<ICombinedWearable>();

            if (combinedWearable == null) return "";

            // other wise durability is not updates
            /*int cacheKey = combinedWearable.GetBitsCacheKey(inSlot.Itemstack);

            if (itemDesciptionCache.TryGetValue(cacheKey, out string? description))
            {
                return description;
            }*/

            StringBuilder dsc = new();

            EnumCharacterDressType dressType = GetDressType(inSlot);
            if (dressType == EnumCharacterDressType.Unknown)
            {
                dsc.AppendLine(Lang.Get("Clothing Category: Unknown"));
            }
            else
            {
                dsc.AppendLine(Lang.Get("Clothing Category: {0}", Lang.Get("clothcategory-" + dressType.ToString().ToLowerInvariant())));
            }

            bool broken = inSlot.Itemstack.Collectible.GetRemainingDurability(inSlot.Itemstack) == 0;

            dsc.AppendLine();
            GetStatModifiers(inSlot)?.GetDescription(dsc, broken);
            

            string result = dsc.ToString();

            //itemDesciptionCache[cacheKey] = result;

            return result;
        }
    }
}
