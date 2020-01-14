using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class ItemLootRandomizer : Item
    {
        Random rand;

        public override void OnLoaded(ICoreAPI api)
        {
            rand = new Random(api.World.Seed);
            base.OnLoaded(api);
        }

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling)
        {
            IPlayer byPlayer = (byEntity as EntityPlayer).Player;
            if (byPlayer == null) return;


            TreeAttribute tree = new TreeAttribute();
            tree.SetString("inventoryId", slot.Inventory.InventoryID);
            tree.SetInt("slotId", slot.Inventory.GetSlotId(slot));
            api.Event.PushEvent("OpenLootRandomizerDialog", tree);
        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

            int i = 0;

            foreach (var val in inSlot.Itemstack.Attributes)
            {
                if (!val.Key.StartsWith("stack") || !(val.Value is TreeAttribute)) continue;

                TreeAttribute subtree = val.Value as TreeAttribute;

                if (i == 0) dsc.AppendLine("Contents: ");
                ItemStack cstack = subtree.GetItemstack("stack");
                cstack.ResolveBlockOrItem(world);

                dsc.AppendLine(cstack.StackSize + "x " + cstack.GetName() + ": " + subtree.GetFloat("chance") + "%");

                i++;
            }

        }

        internal void ResolveLoot(ItemSlot slot, IWorldAccessor worldForResolve)
        {
            object dval;
            worldForResolve.Api.ObjectCache.TryGetValue("donotResolveImports", out dval);
            if (dval is bool && (bool)dval) return;

            double diceRoll = rand.NextDouble();

            ItemStack ownstack = slot.Itemstack;
            slot.Itemstack = null;

            IAttribute[] vals = ownstack.Attributes.Values;
            vals.Shuffle(rand);

            foreach (var val in vals)
            {
                if (!(val is TreeAttribute)) continue;

                TreeAttribute subtree = val as TreeAttribute;
                float chance = subtree.GetFloat("chance") / 100f;

                if (chance > diceRoll)
                {
                    ItemStack cstack = subtree.GetItemstack("stack").Clone();

                    // A items and blocks ItemStackAttributes already has the FixMapping applied to them during Collectible.OnLoadCollectibleMappings(), so we only need to check if the collectible is set or not
                    if (cstack.Collectible != null)
                    {
                        cstack.ResolveBlockOrItem(worldForResolve);
                        slot.Itemstack = cstack;
                    } else
                    {
                        slot.Itemstack = null;
                    }
                    return;
                }

                diceRoll -= chance;

            }
        }

        public override void OnStoreCollectibleMappings(IWorldAccessor world, ItemSlot inSlot, Dictionary<int, AssetLocation> blockIdMapping, Dictionary<int, AssetLocation> itemIdMapping)
        {
            base.OnStoreCollectibleMappings(world, inSlot, blockIdMapping, itemIdMapping);
            
            foreach (var val in inSlot.Itemstack.Attributes)
            {
                if (!val.Key.StartsWith("stack") || !(val.Value is TreeAttribute)) continue;

                TreeAttribute subtree = val.Value as TreeAttribute;

                ItemStack cstack = subtree.GetItemstack("stack");
                cstack.ResolveBlockOrItem(world);

                if (cstack.Class == EnumItemClass.Block)
                {
                    blockIdMapping[cstack.Id] = cstack.Collectible.Code;
                }
                else
                {
                    itemIdMapping[cstack.Id] = cstack.Collectible.Code;
                }
            }
        }

    }
}