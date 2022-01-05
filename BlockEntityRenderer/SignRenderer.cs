using Vintagestory.API;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class BlockEntitySignRenderer : IRenderer
    {
        protected static int TextWidth = 200;
        protected static int TextHeight = 100;

        protected static float QuadWidth = 0.9f;
        protected static float QuadHeight = 0.45f;


        protected CairoFont font;
        protected BlockPos pos;
        protected ICoreClientAPI api;

        protected LoadedTexture loadedTexture;
        protected MeshRef quadModelRef;
        public Matrixf ModelMat = new Matrixf();

        public float rotY = 0;
        public float translateX = 0;
        public float translateY = 0.5625f;
        public float translateZ = 0;

        public float offsetX;
        public float offsetY;
        public float offsetZ;

        float fontSize=20;

        public double RenderOrder
        {
            get { return 1.1; } // 11.11.2020 - this was 0.5 but that causes issues with signs + chest animation
        }

        public int RenderRange
        {
            get { return 24; }
        }

        public BlockEntitySignRenderer(BlockPos pos, ICoreClientAPI api)
        {
            this.api = api;
            this.pos = pos;
            font = new CairoFont(fontSize, GuiStyle.StandardFontName, new double[] { 0, 0, 0, 0.8 });
            font.LineHeightMultiplier = 0.9f;

            api.Event.RegisterRenderer(this, EnumRenderStage.Opaque, "sign");

            MeshData modeldata = QuadMeshUtil.GetQuad();
            modeldata.Uv = new float[]
            {
                1, 1,
                0, 1,
                0, 0,
                1, 0
            };
            modeldata.Rgba = new byte[4 * 4];
            modeldata.Rgba.Fill((byte)255);

            quadModelRef = api.Render.UploadMesh(modeldata);

            Block block = api.World.BlockAccessor.GetBlock(pos);
            BlockFacing facing = BlockFacing.FromCode(block.LastCodePart());
            if (facing == null) return;

            float wallOffset = block.Variant["attachment"] == "wall" ? 0.22f : 0;

            switch (facing.Index)
            {
                case 0: // N
                    translateX = 0.5f;
                    translateZ = 1 - 0.71f - wallOffset;
                    rotY = 180;
                    break;
                case 1: // E
                    translateX = 0.71f + wallOffset;
                    translateZ = 0.5f;
                    rotY = 90;
                    break;
                case 2: // S
                    translateX = 0.5f;
                    translateZ = 0.71f + wallOffset;

                    break;
                case 3: // W
                    translateX = 1 - 0.71f - wallOffset;
                    translateZ = 0.5f;
                    rotY = 270;

                    break;
                    
            }

            
        }

        public virtual void SetNewText(string text, int color)
        {
            font.WithColor(ColorUtil.ToRGBADoubles(color));
            loadedTexture?.Dispose();
            loadedTexture = null;

            if (text.Length > 0)
            {
                font.UnscaledFontsize = fontSize / RuntimeEnv.GUIScale;
                loadedTexture = api.Gui.TextTexture.GenTextTexture(text, font, TextWidth, TextHeight, null, EnumTextOrientation.Center, false);
            }
        }




        public virtual void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            if (loadedTexture == null) return;
            if (!api.Render.DefaultFrustumCuller.SphereInFrustum(pos.X + 0.5, pos.Y + 0.5, pos.Z + 0.5, 1)) return;

            IRenderAPI rpi = api.Render;
            Vec3d camPos = api.World.Player.Entity.CameraPos;

            rpi.GlDisableCullFace();
            rpi.GlToggleBlend(true, EnumBlendMode.PremultipliedAlpha);

            IStandardShaderProgram prog = rpi.PreparedStandardShader(pos.X, pos.Y, pos.Z);

            prog.Tex2D = loadedTexture.TextureId;
            prog.NormalShaded = 0;
            prog.ModelMatrix = ModelMat
                .Identity()
                .Translate(pos.X - camPos.X, pos.Y - camPos.Y, pos.Z - camPos.Z)
                .Translate(translateX, translateY, translateZ)
                .RotateY(rotY * GameMath.DEG2RAD)
                .Translate(offsetX, offsetY, offsetZ)
                .Scale(0.45f * QuadWidth, 0.45f * QuadHeight, 0.45f * QuadWidth)
                .Values
            ;

            prog.ViewMatrix = rpi.CameraMatrixOriginf;
            prog.ProjectionMatrix = rpi.CurrentProjectionMatrix;
            prog.NormalShaded = 0;
            prog.ExtraGodray = 0;
            prog.SsaoAttn = 0;
            prog.AlphaTest = 0.05f;
            prog.OverlayOpacity = 0;

            rpi.RenderMesh(quadModelRef);
            prog.Stop();

            rpi.GlToggleBlend(true, EnumBlendMode.Standard);
        }

        public void Dispose()
        {
            api.Event.UnregisterRenderer(this, EnumRenderStage.Opaque);

            loadedTexture?.Dispose();
            quadModelRef?.Dispose();
        }

    }
}
