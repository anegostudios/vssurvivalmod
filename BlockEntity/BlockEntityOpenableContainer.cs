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


    public abstract class BlockEntityOpenableContainer : BlockEntityContainer, IBlockEntityContainer
    {
        protected GuiDialogBlockEntityInventory invDialog;

        public abstract bool OnPlayerRightClick(IPlayer byPlayer, BlockSelection blockSel);

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            Inventory.LateInitialize(InventoryClassName + "-" + pos.X + "/" + pos.Y + "/" + pos.Z, api);
            Inventory.ResolveBlocksOrItems();
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
                    invDialog?.Dispose();
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
                    
                    invDialog = new GuiDialogBlockEntityInventory(dialogTitle, Inventory, pos, cols, api as ICoreClientAPI);
                    invDialog.TryOpen();
                }
            }

            if (packetid == (int)EnumBlockContainerPacketId.CloseInventory)
            {
                
                clientWorld.Player.InventoryManager.CloseInventory(Inventory);

                invDialog?.TryClose();
                invDialog?.Dispose();
                invDialog = null;
            }
        }


        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();

            invDialog?.Dispose();
        }


    }
}
