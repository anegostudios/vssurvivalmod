using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Vintagestory.GameContent
{
    public class BlockEntityLabeledChest : BlockEntityGenericTypedContainer
    {
        string text = "";
        ChestLabelRenderer labelrenderer;
        int color;
        int tempColor;
        ItemStack tempStack;

        public override float MeshAngle { 
            get => base.MeshAngle; 
            set {
                labelrenderer?.SetRotation(value);
                base.MeshAngle = value;
            }
        }

        public override string DialogTitle {
            get
            {
                if (text == null || text.Length == 0) return Lang.Get("Chest Contents");
                else return text.Replace("\r", "").Replace("\n", " ").Substring(0, Math.Min(text.Length, 15));
            }
        }

        public BlockEntityLabeledChest()
        {
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            if (api is ICoreClientAPI)
            {
                labelrenderer = new ChestLabelRenderer(Pos, api as ICoreClientAPI);
                labelrenderer.SetRotation(MeshAngle);
                labelrenderer.SetNewText(text, color);
            }
        }


        public override bool OnPlayerRightClick(IPlayer byPlayer, BlockSelection blockSel)
        {
            if (byPlayer?.Entity?.Controls?.Sneak == true)
            {
                ItemSlot hotbarSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
                if (hotbarSlot?.Itemstack?.ItemAttributes?["pigment"]?["color"].Exists == true)
                {
                    JsonObject jobj = hotbarSlot.Itemstack.ItemAttributes["pigment"]["color"];
                    int r = jobj["red"].AsInt();
                    int g = jobj["green"].AsInt();
                    int b = jobj["blue"].AsInt();

                    tempColor = ColorUtil.ToRgba(255, r, g, b);
                    tempStack = hotbarSlot.TakeOut(1);


                    if (Api.World is IServerWorldAccessor)
                    {
                        byte[] data;

                        using (MemoryStream ms = new MemoryStream())
                        {
                            BinaryWriter writer = new BinaryWriter(ms);
                            writer.Write("BlockEntityTextInput");
                            writer.Write("Edit chest label text");
                            writer.Write(text);
                            data = ms.ToArray();
                        }

                        ((ICoreServerAPI)Api).Network.SendBlockEntityPacket(
                            (IServerPlayer)byPlayer,
                            Pos.X, Pos.Y, Pos.Z,
                            (int)EnumSignPacketId.OpenDialog,
                            data
                        );
                    }

                    return true;
                }
            }

            return base.OnPlayerRightClick(byPlayer, blockSel);
        }






        public override void OnReceivedClientPacket(IPlayer player, int packetid, byte[] data)
        {
            if (packetid == (int)EnumSignPacketId.SaveText)
            {
                using (MemoryStream ms = new MemoryStream(data))
                {
                    BinaryReader reader = new BinaryReader(ms);
                    text = reader.ReadString();
                    if (text == null) text = "";
                }

                color = tempColor;
                
                MarkDirty(true);

                // Tell server to save this chunk to disk again
                Api.World.BlockAccessor.GetChunkAtBlockPos(Pos.X, Pos.Y, Pos.Z).MarkModified();

                // 85% chance to get back the item
                if (Api.World.Rand.NextDouble() < 0.85)
                {
                    player.InventoryManager.TryGiveItemstack(tempStack);
                }
            }

            if (packetid == (int)EnumSignPacketId.CancelEdit && tempStack != null)
            {
                player.InventoryManager.TryGiveItemstack(tempStack);
                tempStack = null;
            }

            base.OnReceivedClientPacket(player, packetid, data);
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

                    IClientWorldAccessor clientWorld = (IClientWorldAccessor)Api.World;

                    GuiDialogBlockEntityTextInput dlg = new GuiDialogBlockEntityTextInput(dialogTitle, Pos, text, Api as ICoreClientAPI, 132, 4);
                    dlg.OnTextChanged = DidChangeTextClientSide;
                    dlg.OnCloseCancel = () =>
                    {
                        labelrenderer.SetNewText(text, color);
                        (Api as ICoreClientAPI).Network.SendBlockEntityPacket(Pos.X, Pos.Y, Pos.Z, (int)EnumSignPacketId.CancelEdit, null);
                    };
                    dlg.TryOpen();
                }
            }


            if (packetid == (int)EnumSignPacketId.NowText)
            {
                using (MemoryStream ms = new MemoryStream(data))
                {
                    BinaryReader reader = new BinaryReader(ms);
                    text = reader.ReadString();
                    if (text == null) text = "";

                    if (labelrenderer != null)
                    {
                        labelrenderer.SetNewText(text, color);
                    }
                }
            }

            base.OnReceivedServerPacket(packetid, data);
        }


        private void DidChangeTextClientSide(string text)
        {
            labelrenderer?.SetNewText(text, tempColor);
        }



        public override void FromTreeAtributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAtributes(tree, worldForResolving);

            color = tree.GetInt("color");
            text = tree.GetString("text");

            labelrenderer?.SetNewText(text, color);
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetInt("color", color);
            tree.SetString("text", text);
        }

        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();

            if (labelrenderer != null)
            {
                labelrenderer.Unregister();
                labelrenderer = null;
            }
        }

        public override void OnBlockBroken()
        {
            base.OnBlockBroken();
            labelrenderer?.Unregister();
        }

        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();

            labelrenderer?.Unregister();
        }



    }
}
