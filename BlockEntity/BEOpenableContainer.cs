using System.Collections.Generic;
using System.IO;
using System.Text;
using ProtoBuf;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public enum EnumBlockContainerPacketId
    {
        OpenInventory = 5000,
        OpenLidOthers = 5001
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class OpenContainerLidPacket
    {
        [ProtoMember(1)]
        public long EntityId;
        [ProtoMember(2)]
        public bool Opened;

        public OpenContainerLidPacket()
        {
        }

        public OpenContainerLidPacket(long entityId, bool opened)
        {
            EntityId = entityId;
            Opened = opened;
        }
    }

    public class BlockEntityContainerOpen
    {
        public string BlockEntity;
        public string DialogTitle;
        public byte Columns;
        public TreeAttribute Tree;

        public static byte[] ToBytes(string entityName, string dialogTitle, byte columns, InventoryBase inventory)
        {
            using var ms = new MemoryStream();
            var writer = new BinaryWriter(ms);
            writer.Write(entityName);
            writer.Write(dialogTitle);
            writer.Write(columns);
            var tree = new TreeAttribute();
            inventory.ToTreeAttributes(tree);
            tree.ToBytes(writer);

            return ms.ToArray();
        }

        public static BlockEntityContainerOpen FromBytes(byte[] data)
        {
            var inst = new BlockEntityContainerOpen();

            using var ms = new MemoryStream(data);
            var reader = new BinaryReader(ms);
            inst.BlockEntity = reader.ReadString();
            inst.DialogTitle = reader.ReadString();
            inst.Columns = reader.ReadByte();
            inst.Tree = new TreeAttribute();
            inst.Tree.FromBytes(reader);

            return inst;
        }
    }

    public delegate GuiDialogBlockEntity CreateDialogDelegate();


    public abstract class BlockEntityOpenableContainer : BlockEntityContainer
    {
        protected GuiDialogBlockEntity invDialog;

        public virtual AssetLocation OpenSound { get; set; } = new AssetLocation("sounds/block/chestopen");
        public virtual AssetLocation CloseSound { get; set; } = new AssetLocation("sounds/block/chestclose");

        public abstract bool OnPlayerRightClick(IPlayer byPlayer, BlockSelection blockSel);

        /// <summary>
        /// The Entity's that keep this containers lid open.
        /// </summary>
        public HashSet<long> LidOpenEntityId;

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            LidOpenEntityId = new HashSet<long>();
            Inventory.LateInitialize(InventoryClassName + "-" + Pos, api);
            Inventory.ResolveBlocksOrItems();
            Inventory.OnInventoryOpened += OnInventoryOpened;  
            Inventory.OnInventoryClosed += OnInventoryClosed; 

            string os = Block.Attributes?["openSound"]?.AsString();
            string cs = Block.Attributes?["closeSound"]?.AsString();
            AssetLocation opensound = os == null ? null : AssetLocation.Create(os, Block.Code.Domain);
            AssetLocation closesound = cs == null ? null : AssetLocation.Create(cs, Block.Code.Domain);

            OpenSound = opensound ?? this.OpenSound;
            CloseSound = closesound ?? this.CloseSound;
        }

        private void OnInventoryOpened(IPlayer player)
        {
            LidOpenEntityId.Add(player.Entity.EntityId);
        }

        private void OnInventoryClosed(IPlayer player)
        {
            LidOpenEntityId.Remove(player.Entity.EntityId);
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
                    capi.Network.SendBlockEntityPacket(Pos, (int)EnumBlockEntityPacketId.Close, null);
                    capi.Network.SendPacketClient(Inventory.Close(byPlayer));
                };

                invDialog.TryOpen();
                capi.Network.SendPacketClient(Inventory.Open(byPlayer));    // radfast 3.3.2025: I'm not sure this packet has any effect, as the inventory is still closed on the server side at this point, meaning its Id won't be found in PlayerInventoryManager.Inventories. It's the following line's packet which has the effect of causing the inventory to be opened on the server side
                capi.Network.SendBlockEntityPacket(Pos, (int)EnumBlockEntityPacketId.Open, null);
            }
            else
            {
                invDialog.TryClose();
            }
        }

        public override void OnReceivedClientPacket(IPlayer player, int packetid, byte[] data)
        {
            if (packetid == (int)EnumBlockEntityPacketId.Close)
            {
                player.InventoryManager?.CloseInventory(Inventory);
                data = SerializerUtil.Serialize(new OpenContainerLidPacket(player.Entity.EntityId, false));
                ((ICoreServerAPI)Api).Network.BroadcastBlockEntityPacket(
                    Pos,
                    (int)EnumBlockContainerPacketId.OpenLidOthers,
                    data,
                    (IServerPlayer)player
                );
            }


            if (!Api.World.Claims.TryAccess(player, Pos, EnumBlockAccessFlags.Use))
            {
                Api.World.Logger.Audit("Player {0} sent an inventory packet to openable container at {1} but has no claim access. Rejected.", player.PlayerName, Pos);
                return;
            }

            if (packetid < 1000)
            {
                Inventory.InvNetworkUtil.HandleClientPacket(player, packetid, data);

                // Tell server to save this chunk to disk again
                Api.World.BlockAccessor.GetChunkAtBlockPos(Pos).MarkModified();

                return;
            }

            if (packetid == (int)EnumBlockEntityPacketId.Open)
            {
                player.InventoryManager?.OpenInventory(Inventory);
                data = SerializerUtil.Serialize(new OpenContainerLidPacket(player.Entity.EntityId, true));
                ((ICoreServerAPI)Api).Network.BroadcastBlockEntityPacket(
                    Pos,
                    (int)EnumBlockContainerPacketId.OpenLidOthers,
                    data,
                    (IServerPlayer)player
                );
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

                var blockContainer = BlockEntityContainerOpen.FromBytes(data);
                Inventory.FromTreeAttributes(blockContainer.Tree);
                Inventory.ResolveBlocksOrItems();

                invDialog = new GuiDialogBlockEntityInventory(blockContainer.DialogTitle, Inventory, Pos, blockContainer.Columns, Api as ICoreClientAPI);

                Block block = Api.World.BlockAccessor.GetBlock(Pos);
                string os = block.Attributes?["openSound"]?.AsString();
                string cs = block.Attributes?["closeSound"]?.AsString();
                AssetLocation opensound = os == null ? null : AssetLocation.Create(os, block.Code.Domain);
                AssetLocation closesound = cs == null ? null : AssetLocation.Create(cs, block.Code.Domain);

                invDialog.OpenSound = opensound ?? this.OpenSound;
                invDialog.CloseSound = closesound ?? this.CloseSound;

                invDialog.TryOpen();
            }

            if (packetid == (int)EnumBlockContainerPacketId.OpenLidOthers)
            {
                var containerPacket = SerializerUtil.Deserialize<OpenContainerLidPacket>(data);

                if(this is BlockEntityGenericTypedContainer genericContainer)
                {
                    if (containerPacket.Opened)
                    {
                        LidOpenEntityId.Add(containerPacket.EntityId);
                        genericContainer.OpenLid();
                    }
                    else
                    {
                        LidOpenEntityId.Remove(containerPacket.EntityId);
                        if (LidOpenEntityId.Count == 0)
                        {
                            genericContainer.CloseLid();
                        }
                    }
                }
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
            Dispose();
        }

        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();
            Dispose();
        }

        public virtual void Dispose()
        {
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
