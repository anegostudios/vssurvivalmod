using System.IO;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

namespace Vintagestory.GameContent
{
    public class BlockEntitySign : BlockEntity
    {
        public string text = "";
        BlockEntitySignRenderer signRenderer;

        public override void Initialize(ICoreAPI coreapi)
        {
            base.Initialize(coreapi);

            if (coreapi is ICoreClientAPI)
            {
                signRenderer = new BlockEntitySignRenderer(pos, (ICoreClientAPI)coreapi);
                
                if (text.Length > 0) signRenderer.SetNewText(text);
            }
        }

        public override void OnBlockRemoved()
        {
            if (signRenderer != null)
            {
                signRenderer.Unregister();
                signRenderer = null;
            }
        }

        public override void FromTreeAtributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAtributes(tree, worldForResolving);
            text = tree.GetString("text", "");
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetString("text", text);
        }

        public override void OnReceivedClientPacket(IPlayer player, int packetid, byte[] data)
        {
            if (packetid == (int)EnumSignPacketId.ReceivedText)
            {
                using (MemoryStream ms = new MemoryStream(data))
                {
                    BinaryReader reader = new BinaryReader(ms);
                    text = reader.ReadString();
                    if (text == null) text = "";
                }

                ((ICoreServerAPI)api).Network.BroadcastBlockEntityPacket(
                    pos.X, pos.Y, pos.Z,
                    (int)EnumSignPacketId.ReceivedText,
                    data
                );

                // Tell server to save this chunk to disk again
                api.World.BlockAccessor.GetChunkAtBlockPos(pos.X, pos.Y, pos.Z).MarkModified();
            }
        }

        public override void OnReceivedServerPacket(int packetid, byte[] data)
        {
            if (packetid == (int)EnumSignPacketId.OpenDialog)
            {
                using (MemoryStream ms = new MemoryStream(data))
                {
                    BinaryReader reader = new BinaryReader(ms);

                    string dialogClassName = reader.ReadString();
                    string dialogTitle = reader.ReadString();
                    text = reader.ReadString();
                    if (text == null) text = "";

                    IClientWorldAccessor clientWorld = (IClientWorldAccessor)api.World;

                    GuiDialog dlg = new GuiDialogBlockEntityTextInput(dialogTitle, pos, text, api as ICoreClientAPI);
                    dlg.TryOpen();
                }
            }

            if (packetid == (int)EnumSignPacketId.ReceivedText)
            {
                using (MemoryStream ms = new MemoryStream(data))
                {
                    BinaryReader reader = new BinaryReader(ms);
                    text = reader.ReadString();
                    if (text == null) text = "";

                    if(signRenderer != null) signRenderer.SetNewText(text);
                }
            }
        }



        internal void OpenDialog(IPlayer byPlayer)
        {
            if (api.World is IServerWorldAccessor)
            {
                byte[] data;

                using (MemoryStream ms = new MemoryStream())
                {
                    BinaryWriter writer = new BinaryWriter(ms);
                    writer.Write("BlockEntityTextInput");
                    writer.Write("Sign Text");
                    writer.Write(text);
                    data = ms.ToArray();
                }

                ((ICoreServerAPI)api).Network.SendBlockEntityPacket(
                    (IServerPlayer)byPlayer,
                    pos.X, pos.Y, pos.Z,
                    (int)EnumSignPacketId.OpenDialog,
                    data
                );
            }
        }

        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();

            signRenderer?.Unregister();
        }

    }

    public enum EnumSignPacketId
    {
        ReceivedText = 1000,
        OpenDialog = 1001
    }
}
