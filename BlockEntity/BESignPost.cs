using System.IO;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Vintagestory.GameContent
{
    public class BlockEntitySignPost : BlockEntity
    {
        public string[] textByCardinalDirection = new string[8];

        BlockEntitySignPostRenderer signRenderer;
        int color;
        int tempColor;
        ItemStack tempStack;

        MeshData signMesh;

        public string GetTextForDirection(Cardinal dir)
        {
            return textByCardinalDirection[dir.Index];
        }

        public BlockEntitySignPost()
        {
            for (int i = 0; i < 8; i++)
            {
                textByCardinalDirection[i] = "";
            }
        }

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
            signRenderer?.Dispose();
            signRenderer = null;
        }


        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);
            color = tree.GetInt("color");
            if (color == 0) color = ColorUtil.BlackArgb;

            for (int i = 0; i < 8; i++)
            {
                textByCardinalDirection[i] = tree.GetString("text" + i, "");
            }
            

            signRenderer?.SetNewText(textByCardinalDirection, color);
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetInt("color", color);

            for (int i = 0; i < 8; i++)
            {
                tree.SetString("text" + i, textByCardinalDirection[i]);
            }
        }

        public override void OnReceivedClientPacket(IPlayer player, int packetid, byte[] data)
        {
            if (packetid == (int)EnumSignPacketId.SaveText)
            {
                using (MemoryStream ms = new MemoryStream(data))
                {
                    BinaryReader reader = new BinaryReader(ms);
                    for (int i = 0; i < 8; i++)
                    {
                        textByCardinalDirection[i] = reader.ReadString();
                        if (textByCardinalDirection[i] == null) textByCardinalDirection[i] = "";
                    }
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
        }

        GuiDialogSignPost dlg;
        public override void OnReceivedServerPacket(int packetid, byte[] data)
        {
            if (packetid == (int)EnumSignPacketId.OpenDialog)
            {
                using (MemoryStream ms = new MemoryStream(data))
                {
                    BinaryReader reader = new BinaryReader(ms);

                    string dialogClassName = reader.ReadString();
                    string dialogTitle = reader.ReadString();

                    for (int i = 0; i < 8; i++)
                    {
                        textByCardinalDirection[i] = reader.ReadString();
                        if (textByCardinalDirection[i] == null) textByCardinalDirection[i] = "";
                    }

                    IClientWorldAccessor clientWorld = (IClientWorldAccessor)Api.World;

                    CairoFont font = new CairoFont(20, GuiStyle.StandardFontName, new double[] { 0, 0, 0, 0.8 });

                    if (dlg != null && dlg.IsOpened())
                    {
                        return;
                    }

                    dlg = new GuiDialogSignPost(dialogTitle, Pos, textByCardinalDirection, Api as ICoreClientAPI, font);
                    dlg.OnTextChanged = DidChangeTextClientSide;
                    dlg.OnCloseCancel = () =>
                    {
                        signRenderer.SetNewText(textByCardinalDirection, color);
                        (Api as ICoreClientAPI).Network.SendBlockEntityPacket(Pos.X, Pos.Y, Pos.Z, (int)EnumSignPacketId.CancelEdit, null);
                    };
                    dlg.OnClosed += () => { dlg.Dispose(); dlg = null; };
                    dlg.TryOpen();
                }
            }


            if (packetid == (int)EnumSignPacketId.NowText)
            {
                using (MemoryStream ms = new MemoryStream(data))
                {
                    BinaryReader reader = new BinaryReader(ms);
                    for (int i = 0; i < 8; i++)
                    {
                        textByCardinalDirection[i] = reader.ReadString();
                        if (textByCardinalDirection[i] == null) textByCardinalDirection[i] = "";
                    }

                    if (signRenderer != null)
                    {
                        signRenderer.SetNewText(textByCardinalDirection, color);
                    }
                }
            }
        }


        private void DidChangeTextClientSide(string[] textByCardinalDirection)
        {
            signRenderer?.SetNewText(textByCardinalDirection, tempColor);
            this.textByCardinalDirection = textByCardinalDirection;
            MarkDirty(true);
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


                    if (Api.World is IServerWorldAccessor)
                    {
                        byte[] data;

                        using (MemoryStream ms = new MemoryStream())
                        {
                            BinaryWriter writer = new BinaryWriter(ms);
                            writer.Write("BlockEntityTextInput");
                            writer.Write(Lang.Get("Edit Sign Text"));
                            for (int i = 0; i < 8; i++)
                            {
                                writer.Write(textByCardinalDirection[i]);
                            }
                            
                            data = ms.ToArray();
                        }

                        ((ICoreServerAPI)Api).Network.SendBlockEntityPacket(
                            (IServerPlayer)byPlayer,
                            Pos.X, Pos.Y, Pos.Z,
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

        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
        {
            for (int i = 0; i < 8; i++)
            {
                if (textByCardinalDirection[i].Length == 0) continue;

                Cardinal dir = Cardinal.ALL[i];
                float rotY = 0;

                switch (dir.Index)
                {
                    case 0: // N
                        rotY = 180;
                        break;
                    case 1: // NE
                        rotY = 135;
                        break;
                    case 2: // E
                        rotY = 90;
                        break;
                    case 3: // SE
                        rotY = 45;
                        break;
                    case 4: // S
                        break;
                    case 5: // SW
                        rotY = 315;
                        break;
                    case 6: // W
                        rotY = 270;
                        break;
                    case 7: // NW
                        rotY = 225;
                        break;
                }

                mesher.AddMeshData(signMesh.Clone().Rotate(new Vec3f(0.5f, 0.5f, 0.5f), 0, rotY * GameMath.DEG2RAD, 0));
            }


            return false;
        }
    }
}
