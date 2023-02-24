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
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class BlockEntityLabeledChest : BlockEntityGenericTypedContainer
    {
        string text = "";
        ChestLabelRenderer labelrenderer;
        int color;
        int tempColor;
        ItemStack tempStack;
        float fontSize = 20;

        GuiDialogBlockEntityTextInput editDialog;

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
            if (byPlayer?.Entity?.Controls?.ShiftKey == true)
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
                    hotbarSlot.MarkDirty();

                    if (Api is ICoreServerAPI sapi)
                    {
                        sapi.Network.SendBlockEntityPacket(
                            (IServerPlayer)byPlayer,
                            Pos.X, Pos.Y, Pos.Z,
                            (int)EnumSignPacketId.OpenDialog
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
                var packet = SerializerUtil.Deserialize<EditSignPacket>(data);
                this.text = packet.Text;

                this.fontSize = packet.FontSize;
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
                if (editDialog != null && editDialog.IsOpened()) return;

                editDialog = new GuiDialogBlockEntityTextInput("Edit Label text", Pos, text, Api as ICoreClientAPI, new TextAreaConfig() { MaxWidth = 130, MaxHeight = 160 }.CopyWithFontSize(this.fontSize));
                editDialog.OnTextChanged = DidChangeTextClientSide;
                editDialog.OnCloseCancel = () =>
                {
                    labelrenderer?.SetNewText(text, color);
                    (Api as ICoreClientAPI).Network.SendBlockEntityPacket(Pos.X, Pos.Y, Pos.Z, (int)EnumSignPacketId.CancelEdit, null);
                };
                editDialog.TryOpen();
            }


            if (packetid == (int)EnumSignPacketId.NowText)
            {
                var packet = SerializerUtil.Deserialize<EditSignPacket>(data);
                if (labelrenderer != null)
                {
                    labelrenderer.fontSize = packet.FontSize;
                    labelrenderer.SetNewText(packet.Text, color);
                }
            }

            base.OnReceivedServerPacket(packetid, data);
        }


        private void DidChangeTextClientSide(string text)
        {
            if (editDialog == null) return;
            this.fontSize = editDialog.FontSize;
            labelrenderer.fontSize = this.fontSize;
            labelrenderer?.SetNewText(text, tempColor);
        }



        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);

            color = tree.GetInt("color");
            text = tree.GetString("text");
            fontSize = tree.GetFloat("fontSize", 20);

            labelrenderer?.SetNewText(text, color);
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetInt("color", color);
            tree.SetString("text", text);
            tree.SetFloat("fontSize", fontSize);
        }

        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();

            if (labelrenderer != null)
            {
                labelrenderer.Dispose();
                labelrenderer = null;
            }

            editDialog?.TryClose();
            editDialog?.Dispose();
        }

        public override void OnBlockBroken(IPlayer byPlayer = null)
        {
            base.OnBlockBroken(byPlayer);
            labelrenderer?.Dispose();
        }

        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();

            labelrenderer?.Dispose();

            editDialog?.TryClose();
            editDialog?.Dispose();
        }



    }
}
