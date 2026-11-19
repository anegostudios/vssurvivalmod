using System;
using System.Collections.Generic;
using ProtoBuf;
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
    [ProtoContract]
    public class EditSignPacket
    {
        [ProtoMember(1)]
        public string Text;
        [ProtoMember(2)]
        public float FontSize;
    }

    public class BlockEntitySign : BlockEntity , IRotatable
    {
        public const int MaxTextLength = 1024;

        public string text = "";
        protected BlockEntitySignRenderer signRenderer;
        protected int color;
        protected int tempColor;
        protected ItemStack tempStack;
        protected float angleRad;
        protected float fontSize = 20;
        protected BlockSign blockSign;
        protected GuiDialogBlockEntityTextInput editDialog;
        protected MeshData mesh;

        public Cuboidf[] colSelBox;
        public bool Translateable { get; set; }

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
                signRenderer.translateable = this.Translateable;

                if (text.Length > 0) signRenderer.SetNewText(text, color);

                if (Block.Variant["attachment"] != "wall")
                {
                    signRenderer.SetFreestanding(angleRad);
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

            text = tree.GetString("text", "");

            if (!tree.HasAttribute("meshAngle"))
            {
                // Pre 1.16 behavior
                MeshAngleRad = Block.Shape.rotateY * GameMath.DEG2RAD;
            } else
            {
                MeshAngleRad = tree.GetFloat("meshAngle", 0);
            }

            fontSize = tree.GetFloat("fontSize", blockSign?.signConfig?.FontSize ?? 20);
            Translateable = tree.GetBool("translateable");

            if (signRenderer != null)
            {
                signRenderer.fontSize = fontSize;
                signRenderer.translateable = Translateable;
                signRenderer.SetNewText(text, color);
            }
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetInt("color", color);
            tree.SetString("text", text);
            tree.SetFloat("meshAngle", MeshAngleRad);
            tree.SetFloat("fontSize", fontSize);
            tree.SetBool("translateable", Translateable);
        }

        public override void OnReceivedClientPacket(IServerPlayer player, int packetId, SerializedData packetData, ref CachedAccessPerms perms)
        {
            switch (packetId)
            {
                case (int)EnumSignPacketId.SaveText:
                    void resetSignForThatPlayer()
                    {
                        player.InventoryManager.ActiveHotbarSlot.MarkDirty();
                        // Revert the text change for that client:
                        ((ICoreServerAPI)Api).Network.SendBlockEntityPacket<EditSignPacket>(player, Pos, (int)EnumSignPacketId.NowText, new() {
                            FontSize = this.fontSize,
                            Text     = this.text,
                        });

                        // Don't forget to give the chalk item back:
                        player.InventoryManager.TryGiveItemstack(tempStack);
                        tempStack = null;
                    }

                    if (!perms.TryAccessingBlockAtPos(EnumBlockAccessFlags.Use, true, "sign"))
                    {
                        resetSignForThatPlayer();
                        return;
                    }

                    var packet = packetData.Deserialize<EditSignPacket>();
                    if (packet.Text.Length > MaxTextLength)
                    {
                        resetSignForThatPlayer();
                        Api.Logger.Audit("{0} tried to set excessive text (len = {1}) on a sign block at {2}", player.PlayerName, packet.Text.Length, Pos);
                        return;
                    }

                    fontSize = packet.FontSize;
                    Api.Logger.Audit("{0} changed sign text from '{1}' to '{2}' ({3}) at {4}", player.PlayerName, text.Replace("\n","\\n"), packet.Text.Replace("\n","\\n"), packet.FontSize, Pos);
                    SetText(packet.Text);

                    // 85% chance to get back the item
                    if (Api.World.Rand.NextDouble() < 0.85)
                    {
                        player.InventoryManager.TryGiveItemstack(tempStack);
                    }

                    return;


                case (int)EnumSignPacketId.CancelEdit:
                    if (tempStack == null) return;

                    if (!perms.TryAccessingBlockAtPos(EnumBlockAccessFlags.Use, false, "sign")) // Do not validate picking range in case we got transported out of range.
                    {
                        player.InventoryManager.ActiveHotbarSlot.MarkDirty();
                        // No need to revert here, this cannot happen without packet manipulation.
                        return;
                    }

                    player.InventoryManager.TryGiveItemstack(tempStack);
                    tempStack = null;
                    return;
            }

            base.OnReceivedClientPacket(player, packetId, packetData, ref perms);
        }

        public void SetText(string text)
        {
            this.text = text;
            color = tempColor;
            MarkDirty(true);
            // Tell server to save this chunk to disk again
            Api.World.BlockAccessor.GetChunkAtBlockPos(Pos).MarkModified();
        }


        public override void OnReceivedServerPacket(int packetId, SerializedData packetData)
        {
            if (packetId == (int)EnumSignPacketId.OpenDialog)
            {
                if (editDialog != null && editDialog.IsOpened()) return;

                editDialog = new GuiDialogBlockEntityTextInput(Lang.Get("Edit Sign text"), Pos, text, Api as ICoreClientAPI, blockSign?.signConfig.CopyWithFontSize(this.fontSize));
                editDialog.OnTextChanged = (string text) =>
                {
                    if (editDialog == null) return;
                    this.fontSize = editDialog.FontSize;
                    signRenderer.fontSize = this.fontSize;
                    signRenderer?.SetNewText(text, tempColor);
                };
                editDialog.OnCloseCancel = () =>
                {
                    signRenderer.SetNewText(text, color);
                    (Api as ICoreClientAPI).Network.SendBlockEntityPacket(Pos, (int)EnumSignPacketId.CancelEdit, null);
                };
                editDialog.TryOpen();
            }


            if (packetId == (int)EnumSignPacketId.NowText)
            {
                var packet = packetData.Deserialize<EditSignPacket>();
                if (signRenderer != null)
                {
                    signRenderer.fontSize = packet.FontSize;
                    signRenderer.SetNewText(packet.Text, color);
                }
            }
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
                            Pos,
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

        public void OnTransformed(IWorldAccessor worldAccessor, ITreeAttribute tree, int degreeRotation, Dictionary<int, AssetLocation> oldBlockIdMapping,
            Dictionary<int, AssetLocation> oldItemIdMapping, EnumAxis? flipAxis)
        {
            MeshAngleRad = tree.GetFloat("meshAngle");
            MeshAngleRad -= degreeRotation * GameMath.DEG2RAD;
            tree.SetFloat("meshAngle", MeshAngleRad);
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
