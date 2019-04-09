using System;
using System.Collections.Generic;
using System.IO;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

namespace Vintagestory.GameContent
{
    public abstract class BlockEntityContainer : BlockEntity, IBlockEntityContainer
    {
        public abstract InventoryBase Inventory { get; }
        public abstract string InventoryClassName { get; }

        IInventory IBlockEntityContainer.Inventory { get { return Inventory; } }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            Inventory.LateInitialize(InventoryClassName + "-" + pos.X + "/" + pos.Y + "/" + pos.Z, api);
            Inventory.ResolveBlocksOrItems();
        }

        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            BlockContainer container = byItemStack?.Block as BlockContainer;
            if (container != null)
            {
                ItemStack[] stacks = container.GetContents(api.World, byItemStack);
                for (int i = 0; stacks != null && i < stacks.Length; i++)
                {
                    Inventory[i].Itemstack = stacks[i].Clone();
                }

            }
        }

        public override void OnBlockBroken()
        {
            if (api.World is IServerWorldAccessor)
            {
                Inventory.DropAll(pos.ToVec3d().Add(0.5, 0.5, 0.5));
            }
        }

        public ItemStack[] GetContentStacks(bool cloned = true)
        {
            List<ItemStack> stacklist = new List<ItemStack>();
            foreach (var slot in Inventory)
            {
                if (slot.Empty) continue;
                stacklist.Add(cloned ? slot.Itemstack.Clone() : slot.Itemstack);
            }

            return stacklist.ToArray();
        }

        public override void FromTreeAtributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAtributes(tree, worldForResolving);
            Inventory.FromTreeAttributes(tree.GetTreeAttribute("inventory"));
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            if (Inventory != null)
            {
                ITreeAttribute invtree = new TreeAttribute();
                Inventory.ToTreeAttributes(invtree);
                tree["inventory"] = invtree;
            }
        }


        public override void OnStoreCollectibleMappings(Dictionary<int, AssetLocation> blockIdMapping, Dictionary<int, AssetLocation> itemIdMapping)
        {
            foreach (var slot in Inventory)
            {
                slot.Itemstack?.Collectible.OnStoreCollectibleMappings(api.World, slot, blockIdMapping, itemIdMapping);
            }
        }

        public override void OnLoadCollectibleMappings(IWorldAccessor worldForResolve, Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping)
        {
            foreach (var slot in Inventory)
            {
                if (slot.Itemstack == null) continue;

                if (!slot.Itemstack.FixMapping(oldBlockIdMapping, oldItemIdMapping, worldForResolve))
                {
                    slot.Itemstack = null;
                } else
                {
                    slot.Itemstack.Collectible.OnLoadCollectibleMappings(worldForResolve, slot, oldBlockIdMapping, oldItemIdMapping);
                }

                

                if (slot.Itemstack?.Collectible is ItemLootRandomizer)
                {
                    (slot.Itemstack.Collectible as ItemLootRandomizer).ResolveLoot(slot, Inventory, worldForResolve);

                    if (slot.Itemstack?.FixMapping(oldBlockIdMapping, oldItemIdMapping, worldForResolve) == false)
                    {
                        slot.Itemstack = null;
                    }
                }

                if (slot.Itemstack?.Collectible is ItemStackRandomizer)
                {
                    (slot.Itemstack.Collectible as ItemStackRandomizer).Resolve(slot, worldForResolve);
                }
            }
        }

    }
}
