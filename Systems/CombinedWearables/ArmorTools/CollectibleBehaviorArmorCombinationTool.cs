using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace Vintagestory.GameContent
{
    /// <summary>
    /// Tool for combining armor bits on crafting stand
    /// </summary>
    public class CollectibleBehaviorArmorCombinationTool : CollectibleBehaviorArmorTool, IArmorCombinationTool
    {
        public CollectibleBehaviorArmorCombinationTool(CollectibleObject collObj) : base(collObj)
        {
        }


        public bool ConsumeArmorBits { get; set; } = true;


        public override void Initialize(JsonObject properties)
        {
            base.Initialize(properties);

            ConsumeArmorBits = properties["consumecombineditems"]?.AsBool(ConsumeArmorBits) ?? ConsumeArmorBits;
        }

        public virtual bool ShouldConsumeArmorbits(ItemSlot toolSlot, IEnumerable<ItemSlot> bitsSlots, EntityAgent byEntity, ILayeredContainer craftingStand)
        {
            return ConsumeArmorBits;
        }

        public virtual bool CanCombine(ItemSlot toolSlot, IEnumerable<ItemSlot> bitsSlots, EntityAgent byEntity, ILayeredContainer craftingStand)
        {
            return Supported(toolSlot, bitsSlots, byEntity, craftingStand);
        }

        public virtual void OnBeforeCombined(ItemSlot toolSlot, IEnumerable<ItemSlot> bitsSlots, EntityAgent byEntity, ILayeredContainer craftingStand)
        {
            OnUsed(toolSlot, bitsSlots, byEntity, craftingStand);
        }

        public virtual void OnAfterCombined(ItemSlot toolSlot, ItemSlot combinedSlot, EntityAgent byEntity, ILayeredContainer craftingStand)
        {

        }
    }
}
