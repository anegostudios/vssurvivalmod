using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class FirepitContentsRenderer : IRenderer
    {
        MeshRef meshref;
        ICoreClientAPI api;
        BlockPos pos;
        public ItemStack ContentStack;
        int textureId;
        Matrixf ModelMat = new Matrixf();

        ModelTransform transform;
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
            transform = new ModelTransform().EnsureDefaultValues();
            transform.Origin.X = 8 / 16f;
            transform.Origin.Y = 1 / 16f;
            transform.Origin.Z = 8 / 16f;
            transform.Rotation.X = 90;
            transform.Rotation.Y = 90;
            transform.Rotation.Z = 0;
            transform.Translation.X = 0 / 32f;
            transform.Translation.Y = 4f / 16f;
            transform.Translation.Z = 0 / 32f;
            transform.ScaleXYZ.X = 0.25f;
            transform.ScaleXYZ.Y = 0.25f;
            transform.ScaleXYZ.Z = 0.25f;

            defaultTransform = transform;

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

            this.transform = transform;
            if (transform == null) this.transform = defaultTransform;
            this.transform.EnsureDefaultValues();

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

            meshref = api.Render.UploadMesh(ingredientMesh);
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
            prog.ExtraGodray = 0;

            api.Render.BindTexture2d(api.ItemTextureAtlas.AtlasTextureIds[0]);

            int temp = (int)ContentStack.Collectible.GetTemperature(api.World, ContentStack);
            Vec4f lightrgbs = api.World.BlockAccessor.GetLightRGBs(pos.X, pos.Y, pos.Z);
            float[] glowColor = ColorUtil.GetIncandescenceColorAsColor4f(temp);
            lightrgbs[0] += glowColor[0];
            lightrgbs[1] += glowColor[1];
            lightrgbs[2] += glowColor[2];

            prog.RgbaLightIn = lightrgbs;
            prog.RgbaBlockIn = ColorUtil.WhiteArgbVec;
            prog.ExtraGlow = (int)GameMath.Clamp((temp - 500) / 4, 0, 255);

            
            prog.ModelMatrix = ModelMat
                .Identity()
                .Translate(pos.X - camPos.X + transform.Translation.X, pos.Y - camPos.Y + transform.Translation.Y, pos.Z - camPos.Z + transform.Translation.Z)
                .Translate(transform.Origin.X, 0.6f + transform.Origin.Y, transform.Origin.Z)
                .RotateX((90 + transform.Rotation.X) * GameMath.DEG2RAD)
                .RotateY(transform.Rotation.Y * GameMath.DEG2RAD)
                .RotateZ(transform.Rotation.Z * GameMath.DEG2RAD)
                .Scale(transform.ScaleXYZ.X, transform.ScaleXYZ.Y, transform.ScaleXYZ.Z)
                .Translate(-transform.Origin.X, -transform.Origin.Y, -transform.Origin.Z)
                .Values
            ;

            prog.ViewMatrix = rpi.CameraMatrixOriginf;
            prog.ProjectionMatrix = rpi.CurrentProjectionMatrix;

            rpi.RenderMesh(meshref);

            prog.Stop();
        }

        public void Unregister()
        {
            api.Event.UnregisterRenderer(this, EnumRenderStage.Opaque);
        }

        // Called by UnregisterRenderer
        public void Dispose()
        {
            meshref?.Dispose();
            contentStackRenderer?.Dispose();
        }

    }
}
