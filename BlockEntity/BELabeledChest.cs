using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent
{
    public class BlockEntityLabeledChest : BlockEntityGenericTypedContainer
    {
        // Rennorb 2026.07.09 ux: This should really be calculated dynamically based on the space on the sign,
        //  but a rough limit of 255 should at least be better than no limit at all.
        const int MAX_TEXT_LENGTH = 255;

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
                if (text == null || text.Length == 0) return base.DialogTitle;
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
                            Pos,
                            (int)EnumSignPacketId.OpenDialog
                        );
                    }

                    return true;
                }
                else if (hotbarSlot?.Itemstack?.Collectible is ItemPadlock or ItemPlumbAndSquare)
                {
                    return false;
                }
                bool canPlaceBlock = hotbarSlot?.Itemstack?.Class == EnumItemClass.Block &&
                    (byPlayer.WorldData.CurrentGameMode == EnumGameMode.Survival || byPlayer.WorldData.CurrentGameMode == EnumGameMode.Creative);
                if (Block.PlacedPriorityInteract && canPlaceBlock) return false;
            }

            return base.OnPlayerRightClick(byPlayer, blockSel);
        }






        public override void OnReceivedClientPacket(IServerPlayer player, int packetId, SerializedData packetData, ref CachedAccessPerms perms)
        {
            switch (packetId)
            {
                case (int)EnumSignPacketId.SaveText:
                    void resetSignForThatPlayer()
                    {
                        // Revert the text change for that client:
                        ((ICoreServerAPI)Api).Network.SendBlockEntityPacket<EditSignPacket>((IServerPlayer)player, Pos, (int)EnumSignPacketId.NowText, new() {
                            FontSize = this.fontSize,
                            Text     = this.text,
                        });

                        // Don't forget to give the chalk item back:
                        player.InventoryManager.TryGiveItemstack(tempStack);
                        tempStack = null;
                    }

                    if (!perms.TryAccessingBlockAtPos(EnumBlockAccessFlags.Use, true, "labeled chest"))
                    {
                        resetSignForThatPlayer();
                        break;
                    }

                    var packet = packetData.Deserialize<EditSignPacket>();
                    if (packet.Text.Length > MAX_TEXT_LENGTH)
                    {
                        resetSignForThatPlayer();
                        Api.Logger.Audit("{0} tried to set excessive text (len = {1}) to a sign at {2}", player.PlayerName, packet.Text.Length, Pos);
                        break;
                    }

                    Api.Logger.Audit("{0} changed labeled chest text from '{1}' to '{2}' ({3}) at {4}", player.PlayerName, text.Replace("\n","\\n"), packet.Text.Replace("\n","\\n"), packet.FontSize, Pos);
                    text = packet.Text;

                    fontSize = packet.FontSize;
                    color = tempColor;

                    MarkDirty(true);

                    // Tell server to save this chunk to disk again
                    Api.World.BlockAccessor.GetChunkAtBlockPos(Pos).MarkModified();

                    // 85% chance to get back the item
                    if (Api.World.Rand.NextDouble() < 0.85)
                    {
                        player.InventoryManager.TryGiveItemstack(tempStack);
                    }

                    break;

                case (int)EnumSignPacketId.CancelEdit:
                    if (tempStack == null)
                    {
                        break;
                    }

                    if (!perms.TryAccessingBlockAtPos(EnumBlockAccessFlags.Use, false, "labeled chest")) // Do not validate picking range in case we got transported out of range.
                    {
                        // No need to revert here, this cannot happen without packet manipulation.
                        break;
                    }

                    player.InventoryManager.TryGiveItemstack(tempStack);
                    tempStack = null;

                    break;
            }


            base.OnReceivedClientPacket(player, packetId, packetData, ref perms);
        }


        public override void OnReceivedServerPacket(int packetId, SerializedData packetData)
        {
            if (packetId == (int)EnumSignPacketId.OpenDialog)
            {
                if (editDialog != null && editDialog.IsOpened()) return;

                editDialog = new GuiDialogBlockEntityTextInput(Lang.Get("Edit Label text"), Pos, text, Api as ICoreClientAPI, new TextAreaConfig() { MaxWidth = 200, MaxHeight = 96, MaxLength = MAX_TEXT_LENGTH }.CopyWithFontSize(this.fontSize));
                editDialog.OnTextChanged = DidChangeTextClientSide;
                editDialog.OnCloseCancel = () =>
                {
                    labelrenderer?.SetNewText(text, color);
                    (Api as ICoreClientAPI).Network.SendBlockEntityPacket(Pos, (int)EnumSignPacketId.CancelEdit, null);
                };
                editDialog.TryOpen();
            }


            if (packetId == (int)EnumSignPacketId.NowText)
            {
                var packet = packetData.Deserialize<EditSignPacket>();
                if (labelrenderer != null)
                {
                    labelrenderer.fontSize = packet.FontSize;
                    labelrenderer.SetNewText(packet.Text, color);
                }
            }

            base.OnReceivedServerPacket(packetId, packetData);
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

            if (labelrenderer == null) return;
            labelrenderer.fontSize = fontSize;
            labelrenderer.SetNewText(text, color);
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
