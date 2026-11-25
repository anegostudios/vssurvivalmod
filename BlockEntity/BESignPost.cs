using System;
using System.IO;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.Datastructures;

#nullable disable

namespace Vintagestory.GameContent
{
    public class BlockEntitySignPost : BlockEntity
    {
        public const int MaxTextLengthPerSign = 64;

        protected string[] textByCardinalDirection = new string[8].Fill("");
        protected BlockEntitySignPostRenderer signRenderer;
        protected int color;
        protected int tempColor;
        protected ItemStack tempStack;
        protected MeshData signMesh;


        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            if (api is ICoreClientAPI)
            {
                CairoFont font = new CairoFont(20, GuiStyle.StandardFontName, new double[] { 0, 0, 0, 0.8 });

                signRenderer = new BlockEntitySignPostRenderer(Pos, (ICoreClientAPI)api, font);

                if (textByCardinalDirection.Length > 0) signRenderer.SetNewText(textByCardinalDirection, color);

                Shape shape = Shape.TryGet(api, AssetLocation.Create("shapes/block/wood/signpost/sign.json"));
                if (shape != null)
                {
                    (api as ICoreClientAPI).Tesselator.TesselateShape(Block, shape, out signMesh);
                }
            }
        }

        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();
            signRenderer?.Dispose();
            signRenderer = null;
        }


        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);
            color = tree.GetInt("color");
            if (color == 0) color = ColorUtil.BlackArgb;

            for (int dir = 0; dir < 8; dir++)
            {
                textByCardinalDirection[dir] = tree.GetString("text" + dir, "");
            }

            signRenderer?.SetNewText(textByCardinalDirection, color);
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetInt("color", color);

            for (int dir = 0; dir < 8; dir++)
            {
                tree.SetString("text" + dir, textByCardinalDirection[dir]);
            }
        }

        public override void OnReceivedClientPacket(IServerPlayer player, int packetId, SerializedData packetData, ref CachedAccessPerms perms)
        {
            switch(packetId)
            {
                case (int)EnumSignPacketId.SaveText:
                    void resetSignForThatPlayer()
                    {
                        // Revert the text change for that client:
                        var ms = new MemoryStream(512);
                        var writer = new BinaryWriter(ms);
                        for (int dir = 0; dir < 8; dir++)
                        {
                            writer.Write(textByCardinalDirection[dir]);
                        }
                        ((ICoreServerAPI)Api).Network.SendBlockEntityPacket((IServerPlayer)player, Pos, (int)EnumSignPacketId.NowText, ms.ToArray());

                        // Don't forget to give the calk item back:
                        player.InventoryManager.TryGiveItemstack(tempStack);
                        tempStack = null;
                    }

                    if (!perms.TryAccessingBlockAtPos(EnumBlockAccessFlags.Use, true, "signpost"))
                    {
                        resetSignForThatPlayer();
                        return;
                    }

                    var prevTexts = string.Join(',', textByCardinalDirection);
                    {
                        var reader = packetData.AsBinaryReader();
                        for (int dir = 0; dir < 8; dir++)
                        {
                            var str = reader.ReadString() ?? string.Empty;
                            if (str.Length > MaxTextLengthPerSign)
                            {
                                resetSignForThatPlayer();
                                Api.Logger.Audit("{0} tried to set excessive text (len = {1}) on a signpost block at {2}", player.PlayerName, str.Length, Pos);
                                return;
                            }
                            textByCardinalDirection[dir] = str;
                        }
                    }
                    var nowTexts = string.Join(',', textByCardinalDirection);
                    Api.Logger.Audit("{0} changed sign post text from '{1}' to '{2}' at {3}", player.PlayerName, prevTexts, nowTexts, Pos);

                    color = tempColor;
                    MarkDirty(true);

                    // Tell server to save this chunk to disk again
                    Api.World.BlockAccessor.GetChunkAtBlockPos(Pos).MarkModified();

                    // 85% chance to get back the item
                    if (Api.World.Rand.NextDouble() < 0.85)
                    {
                        player.InventoryManager.TryGiveItemstack(tempStack);
                    }

                    return;


                case (int)EnumSignPacketId.CancelEdit:
                    if (tempStack == null) return;

                    if (!perms.TryAccessingBlockAtPos(EnumBlockAccessFlags.Use, false, "signpost")) // Do not validate picking range in case we got transported out of range.
                    {
                        // No need to revert here, this cannot happen without packet manipulation.
                        return;
                    }

                    player.InventoryManager.TryGiveItemstack(tempStack);
                    tempStack = null;
                    return;
            }

            base.OnReceivedClientPacket(player, packetId, packetData, ref perms);
        }

        GuiDialogSignPost dlg;
        public override void OnReceivedServerPacket(int packetId, SerializedData packetData)
        {
            if (packetId == (int)EnumSignPacketId.OpenDialog)
            {
                openDialog(packetData);
            }


            if (packetId == (int)EnumSignPacketId.NowText)
            {
                var reader = packetData.AsBinaryReader();
                for (int i = 0; i < 8; i++)
                {
                    textByCardinalDirection[i] = reader.ReadString() ?? "";
                }

                signRenderer?.SetNewText(textByCardinalDirection, color);
                MarkDirty(true);
            }
        }

        private void openDialog(SerializedData openPacketData)
        {
            if (dlg != null && dlg.IsOpened())
            {
                return;
            }

            var reader = openPacketData.AsBinaryReader();
            for (int i = 0; i < 8; i++)
            {
                textByCardinalDirection[i] = reader.ReadString() ?? "";
            }

            var clientWorld = (IClientWorldAccessor)Api.World;

            var font = new CairoFont(20, GuiStyle.StandardFontName, [0, 0, 0, 0.8]);

            dlg = new GuiDialogSignPost(Lang.Get("Edit Sign Text"), Pos, textByCardinalDirection, Api as ICoreClientAPI, font);
            dlg.OnTextChanged = (string[] textByCardinalDirection) =>
            {
                signRenderer?.SetNewText(textByCardinalDirection, tempColor);
                this.textByCardinalDirection = textByCardinalDirection;
                MarkDirty(true);
            };
            dlg.OnCloseCancel = () =>
            {
                signRenderer.SetNewText(textByCardinalDirection, color);
                (Api as ICoreClientAPI).Network.SendBlockEntityPacket(Pos, (int)EnumSignPacketId.CancelEdit, null);
            };
            dlg.OnClosed += () =>
            {
                dlg.Dispose();
                dlg = null;
            };

            dlg.TryOpen();
        }

        public void OnRightClick(IPlayer byPlayer)
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
                        byte[] data = null;
                        using (MemoryStream ms = new MemoryStream())
                        {
                            BinaryWriter writer = new BinaryWriter(ms);
                            for (int dir = 0; dir < 8; dir++)
                            {
                                writer.Write(textByCardinalDirection[dir]);
                            }

                            data = ms.ToArray();
                        }

                        sapi.Network.SendBlockEntityPacket(
                            (IServerPlayer)byPlayer,
                            Pos,
                            (int)EnumSignPacketId.OpenDialog,
                            data
                        );
                    }
                }
            }
        }

        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();
            signRenderer?.Dispose();
        }


        //                             N,   NE,  E,  SE, S, SW,  W,   NW
        public float[] signMeshRotY = [180, 135, 90, 45, 0, 315, 270, 225];

        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
        {
            for (int dir = 0; dir < 8; dir++)
            {
                if (textByCardinalDirection[dir].Length == 0) continue;

                float rotY = this.signMeshRotY[dir];
                mesher.AddMeshData(signMesh.Clone().Rotate(0, rotY * GameMath.DEG2RAD, 0));
            }


            return false;
        }
    }
}
