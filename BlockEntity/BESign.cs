using System;
using System.IO;
using Vintagestory.API;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class BlockEntitySign : BlockEntity
    {
        public string text = "";
        BlockEntitySignRenderer signRenderer;
        int color;
        int tempColor;
        ItemStack tempStack;
        float angleRad;

        public Cuboidf[] colSelBox;

        public virtual float MeshAngleRad
        {
            get { return angleRad; }
            set
            {
                bool changed = angleRad != value;
                angleRad = value;
                if (Block?.CollisionBoxes != null)
                {
                    colSelBox = new Cuboidf[] { Block.CollisionBoxes[0].RotatedCopy(0, value * GameMath.RAD2DEG, 0, new Vec3d(0.5, 0.5, 0.5)) };
                }

                if (signRenderer != null && Block?.Variant["attachment"] != "wall")
                {
                    signRenderer.rotY = 180 + angleRad * GameMath.RAD2DEG;
                    signRenderer.translateX = 8f / 16f;
                    signRenderer.translateZ = 8f / 16f;
                    signRenderer.offsetZ = -1.51f / 16f;
                }

                if (changed) MarkDirty(true);
            }
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            if (api is ICoreClientAPI)
            {
                signRenderer = new BlockEntitySignRenderer(Pos, (ICoreClientAPI)api);
                
                if (text.Length > 0) signRenderer.SetNewText(text, color);

                if (Block.Variant["attachment"] != "wall")
                {
                    signRenderer.rotY = 180 + angleRad * GameMath.RAD2DEG;
                    signRenderer.translateX = 8f / 16f;
                    signRenderer.translateZ = 8f / 16f;
                    signRenderer.offsetZ = -1.51f / 16f;
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

            text = tree.GetString("text", "");

            if (!tree.HasAttribute("meshAngle"))
            {
                // Pre 1.16 behavior
                MeshAngleRad = Block.Shape.rotateY * GameMath.DEG2RAD;
            } else
            {
                MeshAngleRad = tree.GetFloat("meshAngle", 0);
            }

            signRenderer?.SetNewText(text, color);
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetInt("color", color);
            tree.SetString("text", text);
            tree.SetFloat("meshAngle", MeshAngleRad);
        }

        public override void OnReceivedClientPacket(IPlayer player, int packetid, byte[] data)
        {
            if (!Api.World.Claims.TryAccess(player, Pos, EnumBlockAccessFlags.BuildOrBreak))
            {
                player.InventoryManager.ActiveHotbarSlot.MarkDirty();
                return;
            }

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

                    GuiDialogBlockEntityTextInput dlg = new GuiDialogBlockEntityTextInput(dialogTitle, Pos, text, Api as ICoreClientAPI, 160);
                    dlg.OnTextChanged = DidChangeTextClientSide;
                    dlg.OnCloseCancel = () =>
                    {
                        signRenderer.SetNewText(text, color);
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
                    
                    if (signRenderer != null)
                    {
                        signRenderer.SetNewText(text, color);
                    }
                }
            }
        }


        private void DidChangeTextClientSide(string text)
        {
            signRenderer?.SetNewText(text, tempColor);
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
                            writer.Write("Sign Text");
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
                }
            }
        }

        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();

            signRenderer?.Dispose();
        }

        MeshData mesh;

        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
        {
            if (Block.Variant["attachment"] != "ground")
            {
                return base.OnTesselation(mesher, tessThreadTesselator);
            }

            ensureMeshExists();
            mesher.AddMeshData(mesh);

            return true;
        }


        private void ensureMeshExists()
        {
            mesh = ObjectCacheUtil.GetOrCreate(Api, "signmesh" + Block.Shape.Base + "/" + MeshAngleRad, () =>
            {
                ICoreClientAPI capi = Api as ICoreClientAPI;
                var shape = capi.TesselatorManager.GetCachedShape(Block.Shape.Base);
                capi.Tesselator.TesselateShape(Block, shape, out mesh, new Vec3f(0, MeshAngleRad * GameMath.RAD2DEG, 0));
                return mesh;
            });
        }
    }

    public enum EnumSignPacketId
    {
        NowText = 1000,
        OpenDialog = 1001,
        SaveText = 1002,
        CancelEdit = 1003
            
    }
}
