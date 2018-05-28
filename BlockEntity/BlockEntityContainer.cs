using System.Collections.Generic;
using System.IO;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

namespace Vintagestory.GameContent
{
    public enum EnumBlockContainerPacketId
    {
        OpenInventory = 1000,
        CloseInventory = 1001
    }


    public abstract class BlockEntityContainer : BlockEntity, IBlockEntityContainer
    {
        public abstract InventoryBase Inventory { get; }
        public abstract string InventoryClassName { get; }

        IInventory IBlockEntityContainer.Inventory { get { return Inventory; } }
        protected IGuiDialog invDialog;

        public abstract bool OnPlayerRightClick(IPlayer byPlayer, BlockSelection blockSel);

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            Inventory.LateInitialize(InventoryClassName + "-" + pos.X + "/" + pos.Y + "/" + pos.Z, api);
            Inventory.ResolveBlocksOrItems();
        }

        public override void OnBlockRemoved()
        {
            if (api.World is IServerWorldAccessor)
            {
                Inventory.DropAll(pos.ToVec3d().Add(0.5, 0.5, 0.5));
            }
        }

        public override void OnReceivedClientPacket(IPlayer player, int packetid, byte[] data)
        {
            if (packetid < 1000)
            {
                Inventory.InvNetworkUtil.HandleClientPacket(player, packetid, data);

                // Tell server to save this chunk to disk again
                api.World.BlockAccessor.GetChunkAtBlockPos(pos.X, pos.Y, pos.Z).MarkModified();

                return;
            }

            if (packetid == (int)EnumBlockContainerPacketId.CloseInventory)
            {
                if (player.InventoryManager != null)
                {
                    player.InventoryManager.CloseInventory(Inventory);
                }
            }
        }

        public override void OnReceivedServerPacket(int packetid, byte[] data)
        {
            IClientWorldAccessor clientWorld = (IClientWorldAccessor)api.World;

            if (packetid == (int)EnumBlockContainerPacketId.OpenInventory)
            {
                if (invDialog != null)
                {
                    invDialog.TryClose();
                    invDialog = null;
                    return;
                }

                using (MemoryStream ms = new MemoryStream(data))
                {
                    BinaryReader reader = new BinaryReader(ms);

                    string dialogClassName = reader.ReadString();
                    string dialogTitle = reader.ReadString();
                    int cols = reader.ReadByte();

                    TreeAttribute tree = new TreeAttribute();
                    tree.FromBytes(reader);
                    Inventory.FromTreeAttributes(tree);
                    Inventory.ResolveBlocksOrItems();
                    
                    invDialog = clientWorld.OpenDialog(dialogClassName, dialogTitle, Inventory, pos, cols);
                }
            }

            if (packetid == (int)EnumBlockContainerPacketId.CloseInventory)
            {
                
                clientWorld.Player.InventoryManager.CloseInventory(Inventory);

                invDialog?.TryClose();
                invDialog = null;
            }
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
            int q = Inventory.QuantitySlots;
            for (int i = 0; i < q; i++)
            {
                ItemSlot slot = Inventory.GetSlot(i);
                if (slot.Itemstack == null) continue;

                slot.Itemstack.Collectible.OnStoreCollectibleMappings(api.World, slot, blockIdMapping, itemIdMapping);
            }
        }

        public override void OnLoadCollectibleMappings(IWorldAccessor worldForResolve, Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping)
        {
            int q = Inventory.QuantitySlots;
            for (int i = 0; i < q; i++)
            {
                ItemSlot slot = Inventory.GetSlot(i);
                if (slot.Itemstack == null) continue;
                slot.Itemstack.FixMapping(oldBlockIdMapping, oldItemIdMapping, worldForResolve);

                if (slot.Itemstack?.Collectible is ItemLootRandomizer)
                {
                    (slot.Itemstack.Collectible as ItemLootRandomizer).ResolveLoot(slot, Inventory, worldForResolve);

                    slot.Itemstack?.FixMapping(oldBlockIdMapping, oldItemIdMapping, worldForResolve);
                }

                if (slot.Itemstack?.Collectible is ItemStackRandomizer)
                {
                    (slot.Itemstack.Collectible as ItemStackRandomizer).Resolve(slot, worldForResolve);
                }
            }
        }

    }
}
