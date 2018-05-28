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
        ICoreAPI api;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            this.api = api;
        }


        public override bool OnHeldInteractStart(IItemSlot slot, IEntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            IPlayer byPlayer = null;
            if (byEntity is IEntityPlayer) byPlayer = byEntity.World.PlayerByUid(((IEntityPlayer)byEntity).PlayerUID);
            if (byPlayer == null) return false;


            TreeAttribute tree = new TreeAttribute();
            tree.SetString("inventoryId", slot.Inventory.InventoryID);
            tree.SetInt("slotId", slot.Inventory.GetSlotId(slot));
            api.Event.PushEvent("OpenLootRandomizerDialog", tree);

            return false;
        }

        public override void GetHeldItemInfo(ItemStack stack, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(stack, dsc, world, withDebugInfo);

            float[] chances = new float[10];
            ItemStack[] stacks = new ItemStack[10];
            int i = 0;

            foreach (var val in stack.Attributes)
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

        internal void ResolveLoot(ItemSlot slot, InventoryBase inventory, IWorldAccessor worldForResolve)
        {
            double diceRoll = worldForResolve.Rand.NextDouble();

            ItemStack ownstack = slot.Itemstack;
            slot.Itemstack = null;

            IAttribute[] vals = ownstack.Attributes.Values;
            vals.Shuffle(worldForResolve.Rand);

            foreach (var val in vals)
            {
                if (!(val is TreeAttribute)) continue;

                TreeAttribute subtree = val as TreeAttribute;
                float chance = subtree.GetFloat("chance") / 100f;

                if (chance > diceRoll)
                {
                    ItemStack cstack = subtree.GetItemstack("stack");
                    cstack.ResolveBlockOrItem(worldForResolve);
                    slot.Itemstack = cstack;
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