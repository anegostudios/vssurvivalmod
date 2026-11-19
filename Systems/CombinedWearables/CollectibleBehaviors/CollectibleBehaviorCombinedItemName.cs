using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Config;


namespace Vintagestory.GameContent
{
    public class CollectibleBehaviorCombinedItemName : CollectibleBehavior
    {
        public CollectibleBehaviorCombinedItemName(CollectibleObject collObj) : base(collObj)
        {
        }



        protected Dictionary<int, string> itemNameCache { get; set; } = [];



        public override void GetHeldItemName(StringBuilder sb, ItemStack itemStack)
        {
            ICombinedWearable? combinedWearable = itemStack.Collectible.GetCollectibleInterface<ICombinedWearable>();

            if (combinedWearable == null) return;

            int cacheKey = combinedWearable.GetBitsCacheKey(itemStack);

            if (itemNameCache.TryGetValue(cacheKey, out string? name))
            {
                sb.Clear();
                sb.Append(Lang.Get(name));
                return;
            }

            IEnumerable<WearableBitEntry> pieces = combinedWearable.RetrieveBits(itemStack);

            if (!pieces.Any())
            {
                return;
            }

            int outerLayer = pieces.Select(entry => entry.LayerIndex).Max();

            Dictionary<string, int> suggestedNames = [];
            string? resultingName = null;
            int maxNumberOfPiecesPerName = 0;

            foreach (WearableBitEntry piece in pieces.Where(entry => entry.LayerIndex == outerLayer))
            {
                string? suggestedName = getBitItemName(piece);

                if (suggestedName == null) continue;

                if (suggestedNames.ContainsKey(suggestedName))
                {
                    suggestedNames[suggestedName] += 1;
                }
                else
                {
                    suggestedNames[suggestedName] = 1;
                }

                if (maxNumberOfPiecesPerName < suggestedNames[suggestedName])
                {
                    maxNumberOfPiecesPerName = suggestedNames[suggestedName];
                    resultingName = suggestedName;
                }
            }

            if (resultingName == null) return;

            itemNameCache[cacheKey] = resultingName;

            sb.Clear();
            sb.Append(Lang.Get(resultingName));
        }


        protected virtual string? getBitItemName(WearableBitEntry bitEntry)
        {
            string? name = bitEntry.Stack.Collectible.Attributes?["attachableToEntity"]?["itemNameBySlotCode"]?.FirstByWildcardKey(bitEntry.SlotCode)?.AsString();

            return name;
        }
    }
}
