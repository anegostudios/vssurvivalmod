using System.IO;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public enum EnumBlockContainerPacketId
    {
        OpenInventory = 5000
    }

    public delegate GuiDialogBlockEntity CreateDialogDelegate();


    public abstract class BlockEntityOpenableContainer : BlockEntityContainer, IBlockEntityContainer
    {
        protected GuiDialogBlockEntity invDialog;

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

        protected void toggleInventoryDialogClient(IPlayer byPlayer, CreateDialogDelegate onCreateDialog)
        {
            if (invDialog == null)
            {
                ICoreClientAPI capi = Api as ICoreClientAPI;
                invDialog = onCreateDialog();
                invDialog.OnClosed += () =>
                {
                    invDialog = null;
                    capi.Network.SendBlockEntityPacket(Pos.X, Pos.Y, Pos.Z, (int)EnumBlockEntityPacketId.Close, null);
                    capi.Network.SendPacketClient(Inventory.Close(byPlayer));
                };

                invDialog.TryOpen();
                capi.Network.SendPacketClient(Inventory.Open(byPlayer));
                capi.Network.SendBlockEntityPacket(Pos.X, Pos.Y, Pos.Z, (int)EnumBlockEntityPacketId.Open, null);
            }
            else
            {
                invDialog.TryClose();
            }
        }



        public override void OnReceivedClientPacket(IPlayer player, int packetid, byte[] data)
        {
            if (packetid < 1000)
            {
                Inventory.InvNetworkUtil.HandleClientPacket(player, packetid, data);

                // Tell server to save this chunk to disk again
                Api.World.BlockAccessor.GetChunkAtBlockPos(Pos).MarkModified();

                return;
            }

            if (packetid == (int)EnumBlockEntityPacketId.Close)
            {
                player.InventoryManager?.CloseInventory(Inventory);
            }

            if (packetid == (int)EnumBlockEntityPacketId.Open)
            {
                player.InventoryManager?.OpenInventory(Inventory);
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

        public override void DropContents(Vec3d atPos)
        {
            Inventory.DropAll(atPos);
        }

    }
}
