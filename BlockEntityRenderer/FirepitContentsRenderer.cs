using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent
{
    public class FirepitContentsRenderer : IRenderer
    {
        MultiTextureMeshRef meshref;
        ICoreClientAPI api;
        BlockPos pos;
        public ItemStack ContentStack;
        int textureId;
        Matrixf ModelMat = new Matrixf();

        public ModelTransform Transform;
        ModelTransform defaultTransform;

        public IInFirepitRenderer contentStackRenderer;
        public bool RequireSpit
        {
            get
            {
                return contentStackRenderer == null && ContentStack?.Item != null;
            }
        }

        public double RenderOrder
        {
            get { return 0.5; }
        }

        public int RenderRange
        {
            get { return 48; }
        }

        public FirepitContentsRenderer(ICoreClientAPI api, BlockPos pos)
        {
            this.api = api;
            this.pos = pos;
            Transform = new ModelTransform().EnsureDefaultValues();
            Transform.Origin.X = 8 / 16f;
            Transform.Origin.Y = 1 / 16f;
            Transform.Origin.Z = 8 / 16f;
            Transform.Rotation.X = 90;
            Transform.Rotation.Y = 90;
            Transform.Rotation.Z = 0;
            Transform.Translation.X = 0 / 32f;
            Transform.Translation.Y = 4f / 16f;
            Transform.Translation.Z = 0 / 32f;
            Transform.ScaleXYZ.X = 0.25f;
            Transform.ScaleXYZ.Y = 0.25f;
            Transform.ScaleXYZ.Z = 0.25f;

            defaultTransform = Transform;

        }


        internal void SetChildRenderer(ItemStack contentStack, IInFirepitRenderer renderer)
        {
            this.ContentStack = contentStack;
            meshref?.Dispose();
            meshref = null;
            
            contentStackRenderer = renderer;
        }

        public void SetContents(ItemStack newContentStack, ModelTransform transform)
        {
            contentStackRenderer?.Dispose();
            contentStackRenderer = null;

            this.Transform = transform;
            if (transform == null) this.Transform = defaultTransform;
            this.Transform.EnsureDefaultValues();

            meshref?.Dispose();
            meshref = null;
            
            if (newContentStack == null || newContentStack.Class == EnumItemClass.Block)
            {
                this.ContentStack = null;
                return;
            }

            MeshData ingredientMesh;
            if (newContentStack.Class == EnumItemClass.Item)
            {
                api.Tesselator.TesselateItem(newContentStack.Item, out ingredientMesh);
                textureId = api.ItemTextureAtlas.Positions[newContentStack.Item.FirstTexture.Baked.TextureSubId].atlasTextureId;
            }
            else
            {
                api.Tesselator.TesselateBlock(newContentStack.Block, out ingredientMesh);
                textureId = api.ItemTextureAtlas.Positions[newContentStack.Block.Textures.FirstOrDefault().Value.Baked.TextureSubId].atlasTextureId;
            }

            meshref = api.Render.UploadMultiTextureMesh(ingredientMesh);
            this.ContentStack = newContentStack;
        }

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            if (contentStackRenderer != null)
            {
                contentStackRenderer.OnRenderFrame(deltaTime, stage);
                return;
            }

            if (meshref == null) return;
            
            IRenderAPI rpi = api.Render;
            Vec3d camPos = api.World.Player.Entity.CameraPos;

            rpi.GlDisableCullFace();
            rpi.GlToggleBlend(true);

            IStandardShaderProgram prog = rpi.StandardShader;
            prog.Use();
            prog.DontWarpVertices = 0;
            prog.AddRenderFlags = 0;
            prog.RgbaAmbientIn = rpi.AmbientColor;
            prog.RgbaFogIn = rpi.FogColor;
            prog.FogMinIn = rpi.FogMin;
            prog.FogDensityIn = rpi.FogDensity;
            prog.RgbaTint = ColorUtil.WhiteArgbVec;
            prog.NormalShaded = 1;
            prog.ExtraGodray = 0;
            prog.SsaoAttn = 0;
            prog.AlphaTest = 0.05f;
            prog.OverlayOpacity = 0;

            int temp = (int)ContentStack.Collectible.GetTemperature(api.World, ContentStack);
            Vec4f lightrgbs = api.World.BlockAccessor.GetLightRGBs(pos.X, pos.Y, pos.Z);
            float[] glowColor = ColorUtil.GetIncandescenceColorAsColor4f(temp);
            lightrgbs[0] += glowColor[0];
            lightrgbs[1] += glowColor[1];
            lightrgbs[2] += glowColor[2];

            prog.RgbaLightIn = lightrgbs;
            
            prog.ExtraGlow = (int)GameMath.Clamp((temp - 500) / 4, 0, 255);
            
            prog.ModelMatrix = ModelMat
                .Identity()
                .Translate(pos.X - camPos.X + Transform.Translation.X, pos.Y - camPos.Y + Transform.Translation.Y, pos.Z - camPos.Z + Transform.Translation.Z)
                .Translate(Transform.Origin.X, 0.6f + Transform.Origin.Y, Transform.Origin.Z)
                .RotateX(Transform.Rotation.X * GameMath.DEG2RAD)
                .RotateY(Transform.Rotation.Y * GameMath.DEG2RAD)
                .RotateZ(Transform.Rotation.Z * GameMath.DEG2RAD)
                .Scale(Transform.ScaleXYZ.X, Transform.ScaleXYZ.Y, Transform.ScaleXYZ.Z)
                .Translate(-Transform.Origin.X, -Transform.Origin.Y, -Transform.Origin.Z)
                .Values
            ;

            prog.ViewMatrix = rpi.CameraMatrixOriginf;
            prog.ProjectionMatrix = rpi.CurrentProjectionMatrix;

            rpi.RenderMultiTextureMesh(meshref, "tex");

            prog.Stop();
        }

        public void Dispose()
        {
            api.Event.UnregisterRenderer(this, EnumRenderStage.Opaque);

            meshref?.Dispose();
            contentStackRenderer?.Dispose();
        }

    }
}
