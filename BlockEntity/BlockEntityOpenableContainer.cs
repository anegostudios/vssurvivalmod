using System.Collections.Generic;
using System.IO;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

namespace Vintagestory.GameContent
{
    public enum EnumBlockContainerPacketId
    {
        OpenInventory = 5000
    }


    public abstract class BlockEntityOpenableContainer : BlockEntityContainer, IBlockEntityContainer
    {
        protected GuiDialogBlockEntityInventory invDialog;

        public virtual AssetLocation OpenSound { get; set; } = new AssetLocation("sounds/block/chestopen");
        public virtual AssetLocation CloseSound { get; set; } = new AssetLocation("sounds/block/chestclose");

        public abstract bool OnPlayerRightClick(IPlayer byPlayer, BlockSelection blockSel);

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            Inventory.LateInitialize(InventoryClassName + "-" + Pos.X + "/" + Pos.Y + "/" + Pos.Z, api);
            Inventory.ResolveBlocksOrItems();

            string os = Block.Attributes?["openSound"]?.AsString();
            string cs = Block.Attributes?["closeSound"]?.AsString();
            AssetLocation opensound = os == null ? null : AssetLocation.Create(os, Block.Code.Domain);
            AssetLocation closesound = cs == null ? null : AssetLocation.Create(cs, Block.Code.Domain);

            OpenSound = opensound ?? this.OpenSound;
            CloseSound = closesound ?? this.CloseSound;
        }


        public override void OnReceivedClientPacket(IPlayer player, int packetid, byte[] data)
        {
            if (packetid < 1000)
            {
                Inventory.InvNetworkUtil.HandleClientPacket(player, packetid, data);

                // Tell server to save this chunk to disk again
                Api.World.BlockAccessor.GetChunkAtBlockPos(Pos.X, Pos.Y, Pos.Z).MarkModified();

                return;
            }

            if (packetid == (int)EnumBlockEntityPacketId.Close)
            {
                if (player.InventoryManager != null)
                {
                    player.InventoryManager.CloseInventory(Inventory);
                }
            }

        }

        public override void OnReceivedServerPacket(int packetid, byte[] data)
        {
            IClientWorldAccessor clientWorld = (IClientWorldAccessor)Api.World;

            if (packetid == (int)EnumBlockContainerPacketId.OpenInventory)
            {
                if (invDialog != null)
                {
                    if (invDialog?.IsOpened() == true) invDialog.TryClose();
                    invDialog?.Dispose();
                    invDialog = null;
                    return;
                }

                string dialogClassName;
                string dialogTitle;
                int cols;
                TreeAttribute tree = new TreeAttribute();

                using (MemoryStream ms = new MemoryStream(data))
                {
                    BinaryReader reader = new BinaryReader(ms);
                    dialogClassName = reader.ReadString();
                    dialogTitle = reader.ReadString();
                    cols = reader.ReadByte();    
                    tree.FromBytes(reader);
                }

                Inventory.FromTreeAttributes(tree);
                Inventory.ResolveBlocksOrItems();
                    
                invDialog = new GuiDialogBlockEntityInventory(dialogTitle, Inventory, Pos, cols, Api as ICoreClientAPI);

                Block block = Api.World.BlockAccessor.GetBlock(Pos);
                string os = block.Attributes?["openSound"]?.AsString();
                string cs = block.Attributes?["closeSound"]?.AsString();
                AssetLocation opensound = os == null ? null : AssetLocation.Create(os, block.Code.Domain);
                AssetLocation closesound = cs == null ? null : AssetLocation.Create(cs, block.Code.Domain);

                invDialog.OpenSound = opensound ?? this.OpenSound;
                invDialog.CloseSound = closesound ?? this.CloseSound;

                invDialog.TryOpen();
            }

            if (packetid == (int)EnumBlockEntityPacketId.Close)
            {
                clientWorld.Player.InventoryManager.CloseInventory(Inventory);
                if (invDialog?.IsOpened() == true) invDialog?.TryClose();
                invDialog?.Dispose();
                invDialog = null;
            }
        }


        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();

            if (invDialog?.IsOpened() == true) invDialog?.TryClose();
            invDialog?.Dispose();
        }

        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();

            if (invDialog?.IsOpened() == true) invDialog?.TryClose();
            invDialog?.Dispose();
        }


        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            base.GetBlockInfo(forPlayer, dsc);
        }



    }
}
