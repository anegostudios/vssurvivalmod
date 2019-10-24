using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace Vintagestory.GameContent
{
    // The 4 trader slots for selling stuff to the trader
    public class ItemSlotTrough : ItemSlotSurvival
    {
        BlockEntityTrough be;

        public ItemSlotTrough(BlockEntityTrough be, InventoryGeneric inventory) : base(inventory)
        {
            this.be = be;
        }

        public override bool CanTakeFrom(ItemSlot sourceSlot)
        {
            return base.CanTakeFrom(sourceSlot) && troughable(sourceSlot);
        }

        public override bool CanHold(ItemSlot itemstackFromSourceSlot)
        {
            return base.CanHold(itemstackFromSourceSlot) && troughable(itemstackFromSourceSlot);
        }

        public bool troughable(ItemSlot sourceSlot)
        {
            if (!Empty && !sourceSlot.Itemstack.Equals(be.Api.World, itemstack, GlobalConstants.IgnoredStackAttributes)) return false;

            ContentConfig[] contentConfigs = be.ContentConfig;
            ContentConfig config = getContentConfig(be.Api.World, contentConfigs, sourceSlot);

            return config != null && config.MaxFillLevels * config.QuantityPerFillLevel > StackSize;
        }


        public static ContentConfig getContentConfig(IWorldAccessor world, ContentConfig[] contentConfigs, ItemSlot sourceSlot)
        {
            if (sourceSlot.Empty) return null;
            
            for (int i = 0; i < contentConfigs.Length; i++)
            {
                if (sourceSlot.Itemstack.Equals(world, contentConfigs[i].Content.ResolvedItemstack, GlobalConstants.IgnoredStackAttributes))
                {
                    return contentConfigs[i];
                }
            }

            return null;
        }
    }
}
