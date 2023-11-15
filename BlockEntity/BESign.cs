using ProtoBuf;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    [ProtoContract]
    public class EditSignPacket
    {
        [ProtoMember(1)]
        public string Text;
        [ProtoMember(2)]
        public float FontSize;
    }

    public class BlockEntitySign : BlockEntity
    {
        public string text = "";
        BlockEntitySignRenderer signRenderer;
        int color;
        int tempColor;
        ItemStack tempStack;
        float angleRad;
        float fontSize = 20;

        public Cuboidf[] colSelBox;

        BlockSign blockSign;

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
                    signRenderer.SetFreestanding(angleRad);
                }

                if (changed) MarkDirty(true);
            }
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            blockSign = Block as BlockSign;

            if (api is ICoreClientAPI)
            {
                signRenderer = new BlockEntitySignRenderer(Pos, (ICoreClientAPI)api, blockSign?.signConfig.CopyWithFontSize(this.fontSize));
                signRenderer.fontSize = this.fontSize;

                if (text.Length > 0) signRenderer.SetNewText(text, color);

                if (Block.Variant["attachment"] != "wall")
                {
                    signRenderer.SetFreestanding(angleRad);
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

            fontSize = tree.GetFloat("fontSize", blockSign?.signConfig?.FontSize ?? 20);
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetInt("color", color);
            tree.SetString("text", text);
            tree.SetFloat("meshAngle", MeshAngleRad);
            tree.SetFloat("fontSize", fontSize);
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
                var packet = SerializerUtil.Deserialize<EditSignPacket>(data);
                this.text = packet.Text;
                this.fontSize = packet.FontSize;

                color = tempColor;

                MarkDirty(true);

                // Tell server to save this chunk to disk again
                Api.World.BlockAccessor.GetChunkAtBlockPos(Pos).MarkModified();

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


        GuiDialogBlockEntityTextInput editDialog;
        public override void OnReceivedServerPacket(int packetid, byte[] data)
        {
            if (packetid == (int)EnumSignPacketId.OpenDialog)
            {
                if (editDialog != null && editDialog.IsOpened()) return;

                editDialog = new GuiDialogBlockEntityTextInput(Lang.Get("Edit Sign text"), Pos, text, Api as ICoreClientAPI, blockSign?.signConfig.CopyWithFontSize(this.fontSize));
                editDialog.OnTextChanged = DidChangeTextClientSide;
                editDialog.OnCloseCancel = () =>
                {
                    signRenderer.SetNewText(text, color);
                    (Api as ICoreClientAPI).Network.SendBlockEntityPacket(Pos.X, Pos.Y, Pos.Z, (int)EnumSignPacketId.CancelEdit, null);
                };
                editDialog.TryOpen();
            }


            if (packetid == (int)EnumSignPacketId.NowText)
            {
                var packet = SerializerUtil.Deserialize<EditSignPacket>(data);
                if (signRenderer != null)
                {
                    signRenderer.fontSize = packet.FontSize;
                    signRenderer.SetNewText(packet.Text, color);
                }
            }
        }


        private void DidChangeTextClientSide(string text)
        {
            if (editDialog == null) return;
            this.fontSize = editDialog.FontSize;
            signRenderer.fontSize = this.fontSize;
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

                    if (Api is ICoreServerAPI sapi)
                    {
                        sapi.Network.SendBlockEntityPacket(
                            (IServerPlayer)byPlayer,
                            Pos.X, Pos.Y, Pos.Z,
                            (int)EnumSignPacketId.OpenDialog
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
            mesh = ObjectCacheUtil.GetOrCreate(Api, "signmesh" + base.Block.Code.ToString() + "/" + base.Block.Shape.Base?.ToString() + "/" + this.MeshAngleRad, () =>
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
