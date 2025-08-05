using ProtoBuf;
using System.Numerics;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using static OpenTK.Graphics.OpenGL.GL;
using static System.Net.Mime.MediaTypeNames;

#nullable disable

namespace Vintagestory.GameContent
{

    [ProtoContract]
    public class TextDataPacket
    {
        [ProtoMember(1)]
        public string Text;
        [ProtoMember(2)]
        public float FontSize;
    }

    public enum WritingSurfacePackets
    {
        Open = 12311,
        Save = 12312,
        Cancel = 12313
    }


    public class EntityBehaviorWritingSurface : EntityBehavior, ITexPositionSource
    {
        protected MultiTextureMeshRef meshref;
        protected ICoreClientAPI capi;
        protected LoadedTexture loadedTexture;

        protected TextAreaConfig signTextConfig;
        protected CairoFont font;
        
        protected EnumVerticalAlign verticalAlign;
        protected int TextWidth = 208;
        protected int TextHeight = 96;
        protected float DefaultFontSize;
        protected string SurfaceName = "leftplaque";

        int tempColor;
        ItemStack tempStack;


        public float FontSize
        {
            get { return entity.WatchedAttributes.GetFloat(SurfaceName + "_fontSize", DefaultFontSize); }
            set { entity.WatchedAttributes.SetFloat(SurfaceName + "_fontSize", value); }
        }
        public string Text
        {
            get { return entity.WatchedAttributes.GetString(SurfaceName + "_writingSurfaceText"); }
            set { entity.WatchedAttributes.SetString(SurfaceName + "_writingSurfaceText", value); }
        }

        public int Color
        {
            get { return entity.WatchedAttributes.GetInt(SurfaceName + "_textColor", 255 << 24); }
            set { entity.WatchedAttributes.SetInt(SurfaceName + "_textColor", value); }
        }


        public EntityBehaviorWritingSurface(Entity entity) : base(entity) { }

        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            capi = entity.World.Api as ICoreClientAPI;
            if (capi != null)
            {
                capi.Event.ReloadTextures += Event_ReloadTextures;
                entity.WatchedAttributes.RegisterModifiedListener(SurfaceName + "_writingSurfaceText", entity.MarkShapeModified);
                signTextConfig = attributes["fontConfig"].AsObject<TextAreaConfig>();
                font = new CairoFont(signTextConfig.FontSize, signTextConfig.FontName, new double[] { 0, 0, 0, 0.8 });
                if (signTextConfig.BoldFont) font.WithWeight(Cairo.FontWeight.Bold);
                font.LineHeightMultiplier = 0.9f;
                verticalAlign = signTextConfig.VerticalAlign;
                TextWidth = signTextConfig.MaxWidth;
                TextHeight = signTextConfig.MaxHeight;
                DefaultFontSize = signTextConfig.FontSize;
            }
        }

        private void Event_ReloadTextures()
        {
            texPos = null;
            previousText = null;
        }

        GuiDialogTextInput editDialog;
        public override void OnInteract(EntityAgent byEntity, ItemSlot itemslot, Vec3d hitPosition, EnumInteractMode mode, ref EnumHandling handled)
        {
            if (entity.World.Side == EnumAppSide.Server) return;
            if (entity.GetBehavior<EntityBehaviorSelectionBoxes>()?.IsAPCode((byEntity as EntityPlayer).EntitySelection, "LPlaqueAP") != true) return;

            if (editDialog != null && editDialog.IsOpened()) return;

            var capi = entity.Api as ICoreClientAPI;

            if (!loadPigment(byEntity)) return;

            editDialog = new GuiDialogTextInput(Lang.Get("Edit Sign text"), Text, capi, signTextConfig.CopyWithFontSize(this.FontSize));
            editDialog.OnSave = (text) =>
            {
                FontSize = editDialog.FontSize;
                Text = text;

                capi.Network.SendEntityPacket(
                    entity.EntityId, 
                    (int)WritingSurfacePackets.Save, 
                    SerializerUtil.Serialize(new TextDataPacket() { Text = text, FontSize = FontSize })
                );
                Color = tempColor;
            };
            editDialog.OnCloseCancel = () =>
            {
                capi.Network.SendEntityPacket(entity.EntityId, (int)WritingSurfacePackets.Cancel);
            };

            editDialog.TryOpen();
            capi.Network.SendEntityPacket(entity.EntityId, (int)WritingSurfacePackets.Open);
        }

        public override void OnReceivedClientPacket(IServerPlayer player, int packetid, byte[] data, ref EnumHandling handled)
        {
            base.OnReceivedClientPacket(player, packetid, data, ref handled);
            
            if (packetid == (int)WritingSurfacePackets.Open)
            {
                loadPigment(player.Entity);
            }

            if (packetid == (int)WritingSurfacePackets.Cancel)
            {
                player.Entity.TryGiveItemStack(tempStack);
                tempStack = null;
            }

            if (packetid == (int)WritingSurfacePackets.Save)
            {
                // 85% chance to get back the item
                if (entity.World.Rand.NextDouble() < 0.85)
                {
                    player.Entity.TryGiveItemStack(tempStack);
                }
                tempStack = null;

                var pkg = SerializerUtil.Deserialize<TextDataPacket>(data);
                this.Text = pkg.Text;
                this.FontSize = pkg.FontSize;

                entity.MarkShapeModified();
                Color = tempColor;
            }
        }

        private bool loadPigment(EntityAgent eagent)
        {
            ItemSlot hotbarSlot = eagent.RightHandItemSlot;
            if (hotbarSlot?.Itemstack?.ItemAttributes?["pigment"]?["color"].Exists == true)
            {
                JsonObject jobj = hotbarSlot.Itemstack.ItemAttributes["pigment"]["color"];
                int r = jobj["red"].AsInt();
                int g = jobj["green"].AsInt();
                int b = jobj["blue"].AsInt();

                tempColor = ColorUtil.ToRgba(255, r, g, b);
                if (eagent.World.Side == EnumAppSide.Server)
                {
                    tempStack = hotbarSlot.TakeOut(1);
                    hotbarSlot.MarkDirty();
                }
                return true;
            }

            return false;
        }

        string previousText = null;
        int previousColor = 0;
        float previousFontSize = -1;
        TextureAtlasPosition texPos = null;
        int textureSubId=0;

        public override void OnTesselation(ref Shape entityShape, string shapePathForLogging, ref bool shapeIsCloned, ref string[] willDeleteElements)
        {
            if (entity.World.Side == EnumAppSide.Server) return;

            if (previousText == Text && previousColor == Color && previousFontSize == FontSize)
            {
                if (Text == null || Text.Length == 0)
                {
                    if (!shapeIsCloned) entityShape = entityShape.Clone();

                    var ele = entityShape.GetElementByName("PlaqueLeftFrontText");
                    if (ele == null) return;
                    foreach (var face in ele.FacesResolved)
                    {
                        if (face == null) continue;
                        face.Texture = "transparent";
                    }

                }
                return;
            }
            
            previousText = Text;
            previousColor = Color;
            previousFontSize = FontSize;

            var api = entity.Api as ICoreClientAPI;

            // 1. Upload a text texture
            // 2. Plonk it into entity.Properties.Client.Textures
            // 3. Make sure its code matches the element face in the shape file

            font.WithColor(ColorUtil.ToRGBADoubles(Color));
            loadedTexture?.Dispose();
            loadedTexture = null;


            font.UnscaledFontsize = FontSize / RuntimeEnv.GUIScale;

            double verPadding = verticalAlign == EnumVerticalAlign.Middle ? (TextHeight - api.Gui.Text.GetMultilineTextHeight(font, Text, TextWidth)) : 0;
            var bg = new TextBackground()
            {
                VerPadding = (int)verPadding / 2
            };

            loadedTexture = api.Gui.TextTexture.GenTextTexture(Text, font, TextWidth, TextHeight, bg, EnumTextOrientation.Center, false);
                
            string textureCode = "writingsurface-" + SurfaceName + "-" + entity.EntityId;
            
            if (texPos == null)
            {
                api.EntityTextureAtlas.AllocateTextureSpace(TextWidth, TextHeight, out textureSubId, out texPos, new AssetLocationAndSource(textureCode));
            }

            var ctex = new CompositeTexture(textureCode);
            entity.Properties.Client.Textures[textureCode] = ctex;
            ctex.Bake(entity.Api.Assets);
            ctex.Baked.TextureSubId = textureSubId;

            var atlas = api.EntityTextureAtlas.AtlasTextures[texPos.atlasNumber];
            api.Render.RenderTextureIntoTexture(loadedTexture, 0, 0, TextWidth, TextHeight, atlas, texPos.x1 * atlas.Width, texPos.y1 * atlas.Height, -1);
            api.EntityTextureAtlas.RegenMipMaps(texPos.atlasNumber);

            if (!shapeIsCloned) entityShape = entityShape.Clone();

            var elem = entityShape.GetElementByName("PlaqueLeftFrontText");
            if (elem == null) return;
            foreach (var face in elem.FacesResolved)
            {
                if (face == null) continue;
                face.Texture = textureCode;
            }

            shapeIsCloned = true;
        }

        public double RenderOrder => 0.36; // Liquid render is at 0.37
        public int RenderRange => 99;

        Size2i dummysize = new Size2i(2048,2048);
        TextureAtlasPosition dummyPos = new TextureAtlasPosition() { x1 = 0, y1 = 0, x2 = 1, y2 = 1 };
        public Size2i AtlasSize => dummysize;
        public TextureAtlasPosition this[string textureCode] => dummyPos;

        public override string PropertyName() => "writingsurface";
    }

}
