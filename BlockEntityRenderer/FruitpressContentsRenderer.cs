using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class FruitpressContentsRenderer : IRenderer, ITexPositionSource
    {
        
        ICoreClientAPI api;
        BlockPos pos;
        Matrixf ModelMat = new Matrixf();
        MeshRef juiceMeshref;
        MeshRef mashMeshref;

        BlockEntityFruitPress befruitpress;


        public double RenderOrder
        {
            get { return 0.65; }
        }

        public int RenderRange
        {
            get { return 48; }
        }

        AssetLocation textureLocation;
        TextureAtlasPosition texPos;


        public TextureAtlasPosition juiceTexPos;

        public Size2i AtlasSize => api.BlockTextureAtlas.Size;

        public TextureAtlasPosition this[string textureCode]
        {
            get
            {
                AssetLocation texturePath = textureLocation;
                TextureAtlasPosition texpos;
                if (texturePath == null)
                {
                    texpos = this.texPos;
                } else
                {
                    texpos = api.BlockTextureAtlas[texturePath];
                }


                if (texpos == null)
                {
                    IAsset texAsset = api.Assets.TryGet(texturePath.Clone().WithPathPrefixOnce("textures/").WithPathAppendixOnce(".png"));
                    if (texAsset != null)
                    {
                        BitmapRef bmp = texAsset.ToBitmap(api);
                        api.BlockTextureAtlas.GetOrInsertTexture(texturePath, out _, out texpos, () => bmp);
                    }
                    else
                    {
                        texpos = api.BlockTextureAtlas.UnknownTexturePosition;
                    }
                }

                return texpos;
            }
        }

        public FruitpressContentsRenderer(ICoreClientAPI api, BlockPos pos, BlockEntityFruitPress befruitpress)
        {
            this.api = api;
            this.pos = pos;
            this.befruitpress = befruitpress;
        }

        public void reloadMeshes(JuiceableProperties props, bool mustReload)
        {
            if (befruitpress.Inventory.Empty)
            {
                juiceMeshref = null;
                mashMeshref = null;
                return;
            }
            if (!mustReload && juiceMeshref != null) return;

            juiceMeshref?.Dispose();
            mashMeshref?.Dispose();

            ItemStack stack = befruitpress.Inventory[0].Itemstack;

            if (stack == null) return;

            // Mash
            int y;
            if (stack.Collectible.Code.Path == "rot")
            {
                textureLocation = new AssetLocation("block/wood/barrel/rot");
                y = GameMath.Clamp(stack.StackSize / 2, 1, 9);
            }
            else
            {
                var tex = props.PressedStack.ResolvedItemstack.Item.Textures.First();
                textureLocation = tex.Value.Base;

                if (stack.Attributes.HasAttribute("juiceableLitresLeft"))
                {
                    float availableLitres = (float)stack.Attributes.GetDecimal("juiceableLitresLeft") + (float)stack.Attributes.GetDecimal("juiceableLitresTransfered");
                    y = (int)GameMath.Clamp(availableLitres, 1, 9);
                }
                else y = GameMath.Clamp(stack.StackSize, 1, 9);
            }


            Shape mashShape = API.Common.Shape.TryGet(api, "shapes/block/wood/fruitpress/part-mash-"+y+".json");
            api.Tesselator.TesselateShape("fruitpress-mash", mashShape, out MeshData mashMesh, this);

            juiceTexPos = api.BlockTextureAtlas[textureLocation];

            // Juice
            if (stack.Collectible.Code.Path != "rot")
            {
                Shape juiceShape = API.Common.Shape.TryGet(api, "shapes/block/wood/fruitpress/part-juice.json");

                var loc = AssetLocation.Create("juiceportion-" + stack.Collectible.Variant["fruit"], stack.Collectible.Code.Domain);
                Item item = api.World.GetItem(loc);
                textureLocation = null;
                if (item?.FirstTexture.Baked == null)
                {
                    texPos = api.BlockTextureAtlas.UnknownTexturePosition;
                }
                else
                {
                    texPos = api.BlockTextureAtlas.Positions[item.FirstTexture.Baked.TextureSubId];
                }

                api.Tesselator.TesselateShape("fruitpress-juice", juiceShape, out MeshData juiceMesh, this);

                juiceMeshref = api.Render.UploadMesh(juiceMesh);
            }
            
            mashMeshref = api.Render.UploadMesh(mashMesh);
        }


        // Needs to render mash, juice quad and bucket
        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            if (mashMeshref == null || mashMeshref.Disposed) return;
            
            IRenderAPI rpi = api.Render;
            Vec3d camPos = api.World.Player.Entity.CameraPos;

            rpi.GlDisableCullFace();
            rpi.GlToggleBlend(true);

            IStandardShaderProgram prog = rpi.StandardShader;
            prog.Use();
            prog.Tex2D = api.BlockTextureAtlas.AtlasTextures[0].TextureId;
            prog.DontWarpVertices = 0;
            prog.AddRenderFlags = 0;
            prog.RgbaAmbientIn = rpi.AmbientColor;
            prog.RgbaFogIn = rpi.FogColor;
            prog.FogMinIn = rpi.FogMin;
            prog.FogDensityIn = rpi.FogDensity;
            prog.RgbaTint = ColorUtil.WhiteArgbVec;
            prog.NormalShaded = 1;
            prog.ExtraGodray = 0;
            prog.ExtraGlow = 0;
            prog.SsaoAttn = 0;
            prog.AlphaTest = 0.05f;
            prog.OverlayOpacity = 0;

            Vec4f lightrgbs = api.World.BlockAccessor.GetLightRGBs(pos.X, pos.Y, pos.Z);
            prog.RgbaLightIn = lightrgbs;


            double squeezeRel = befruitpress.MashSlot.Itemstack?.Attributes?.GetDouble("squeezeRel", 1) ?? 1;

            prog.ModelMatrix = ModelMat
                .Identity()
                .Translate(pos.X - camPos.X, pos.Y - camPos.Y, pos.Z - camPos.Z)
                .Translate(0, 0.8f, 0)
                .Scale(1, (float)squeezeRel, 1)
                .Values
            ;

            prog.ViewMatrix = rpi.CameraMatrixOriginf;
            prog.ProjectionMatrix = rpi.CurrentProjectionMatrix;

            rpi.RenderMesh(mashMeshref);

            prog.Stop();
        }

        public void Dispose()
        {
            api.Event.UnregisterRenderer(this, EnumRenderStage.Opaque);

            mashMeshref?.Dispose();
            juiceMeshref?.Dispose();
        }

    }
}
