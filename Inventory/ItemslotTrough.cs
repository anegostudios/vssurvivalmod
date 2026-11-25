using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent
{
    public class ItemSlotTrough : ItemSlotSurvival
    {
        BlockEntityTrough be;

        public ItemSlotTrough(BlockEntityTrough be, InventoryGeneric inventory) : base(inventory)
        {
            this.be = be;
        }

        public override bool CanTakeFrom(ItemSlot sourceSlot, EnumMergePriority priority = EnumMergePriority.AutoMerge)
        {
            return base.CanTakeFrom(sourceSlot, priority) && troughable(sourceSlot);
        }

        public override bool CanHold(ItemSlot itemstackFromSourceSlot)
        {
            return base.CanHold(itemstackFromSourceSlot) && troughable(itemstackFromSourceSlot);
        }

        public bool troughable(ItemSlot sourceSlot)
        {
            if (!Empty && !sourceSlot.Itemstack.Equals(be.Api.World, itemstack, GlobalConstants.IgnoredStackAttributes)) return false;

            ContentConfig[] contentConfigs = be.contentConfigs;
            ContentConfig config = getContentConfig(be.Api.World, contentConfigs, sourceSlot);

            return config != null && config.MaxFillLevels * config.QuantityPerFillLevel > StackSize;
        }


        public static ContentConfig getContentConfig(IWorldAccessor world, ContentConfig[] contentConfigs, ItemSlot sourceSlot)
        {
            if (sourceSlot.Empty || contentConfigs == null) return null;
            
            for (int i = 0; i < contentConfigs.Length; i++)
            {
                var cfg = contentConfigs[i];

                if (cfg.Content.Code.Path.Contains('*'))
                {
                    if (WildcardUtil.Match(cfg.Content.Code, sourceSlot.Itemstack.Collectible.Code)) return cfg;
                    continue;
                }

                if (sourceSlot.Itemstack.Equals(world, cfg.Content.ResolvedItemstack, GlobalConstants.IgnoredStackAttributes))
                {
                    return cfg;
                }
            }

            return null;
        }
    }
}
