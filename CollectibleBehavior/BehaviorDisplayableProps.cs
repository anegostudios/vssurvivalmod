using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public interface IDisplayableProps
    {
        public DisplayableAttributes? GetDisplayableProps(ItemSlot inSlot, string displayType);
    }

    public class CollectibleBehaviorTypedDisplayableProps(CollectibleObject collObj) : CollectibleBehavior(collObj), IDisplayableProps
    {
        public Dictionary<string, Dictionary<string, DisplayableAttributes>>? PropsByCode;

        public override void Initialize(JsonObject properties)
        {
            base.Initialize(properties);
            PropsByCode = properties["displayable"].AsObject<Dictionary<string, Dictionary<string, DisplayableAttributes>>>();
        }

        public virtual DisplayableAttributes? GetDisplayableProps(ItemSlot inSlot, string displayType)
        {
            if (PropsByCode == null || inSlot.Itemstack?.Attributes.GetString("type", "") is not { } type) return null;
            var propsByDisplay = PropsByCode.FirstOrDefault(code => WildcardUtil.Match(code.Key, type)).Value;

            return propsByDisplay?.TryGetValue(displayType);
        }
    }
}
