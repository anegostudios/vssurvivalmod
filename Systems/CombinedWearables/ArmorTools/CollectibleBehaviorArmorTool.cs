using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    /// <summary>
    /// Base class for armor crafting tools that implements common functionality of checking if bits are supported, and spending durability on use
    /// </summary>
    public class CollectibleBehaviorArmorTool : CollectibleBehavior
    {
        public CollectibleBehaviorArmorTool(CollectibleObject collObj) : base(collObj)
        {
        }

        public TagSet? SupportedArmorBitsByTags { get; set; }

        public AssetLocation? SupportedArmorBitsByWildcard { get; set; }

        public int LooseDurabilityOnCombined { get; set; } = 0;

        public int LooseDurabilityOnCombinedPerBit { get; set; } = 0;

        public bool BreakTool { get; set; } = true;

        public bool Consume { get; set; } = false;



        public override void Initialize(JsonObject properties)
        {
            base.Initialize(properties);

            SupportedArmorBitsByWildcard = properties["bitsWildcard"]?.AsObject<AssetLocation>();
            SupportedArmorBitsByTags = properties["bitsTags"]?.AsObject<TagSet>();
            LooseDurabilityOnCombined = properties["durabilityCost"]?.AsInt(LooseDurabilityOnCombined) ?? LooseDurabilityOnCombined;
            LooseDurabilityOnCombinedPerBit = properties["durabilityCostPerBit"]?.AsInt(LooseDurabilityOnCombinedPerBit) ?? LooseDurabilityOnCombinedPerBit;
            BreakTool = properties["breakTool"]?.AsBool(BreakTool) ?? BreakTool;
            Consume = properties["consume"]?.AsBool(Consume) ?? Consume;
        }

        public virtual bool Supported(ItemSlot toolSlot, IEnumerable<ItemSlot> bitsSlots, EntityAgent byEntity, ILayeredContainer craftingStand)
        {
            return bitsSlots.Where(slot => slot.Itemstack != null).All(ArmorBitIsSupported);
        }

        public virtual void OnUsed(ItemSlot toolSlot, IEnumerable<ItemSlot> bitsSlots, EntityAgent byEntity, ILayeredContainer craftingStand)
        {
            if (toolSlot.Itemstack == null) return;

            if (Consume)
            {
                toolSlot.TakeOut(1);
                return;
            }

            int durabilityDamage = LooseDurabilityOnCombined + bitsSlots.Count(slot => slot.Empty) * LooseDurabilityOnCombinedPerBit;

            if (durabilityDamage <= 0) return;

            toolSlot.Itemstack.Collectible.DamageItem(byEntity.World, byEntity, toolSlot, durabilityDamage, destroyOnZeroDurability: BreakTool);
        }



        protected virtual bool ArmorBitIsSupported(ItemSlot slot)
        {
            if (slot.Itemstack == null) return false;

            if (SupportedArmorBitsByTags != null && slot.Itemstack.Collectible.Tags.Overlaps(SupportedArmorBitsByTags.Value)) return true;

            if (SupportedArmorBitsByWildcard != null && WildcardUtil.Match(SupportedArmorBitsByWildcard, slot.Itemstack.Collectible.Code)) return true;

            return SupportedArmorBitsByWildcard == null || SupportedArmorBitsByWildcard == null;
        }
    }
}
