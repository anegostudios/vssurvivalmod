using System;
using System.Drawing.Imaging;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent
{
    public class AfterSignRenderer : IRenderer
    {
        public static bool Registered = false;

        public double RenderOrder => BlockEntitySignRenderer.AfterSignRendererOrder;

        public int RenderRange => 24;

        public AfterSignRenderer()
        {
            Registered = true;
        }

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            BlockEntitySignRenderer.AfterRenderFrameOnce();
        }

        public void Dispose()
        {
        }

        public static void OnGameDisposed()
        {
            Registered = false;
            BlockEntitySignRenderer.AfterRenderFrameOnce();   // Dispose of any shader, in case we exit the game at a strange time between the two renderers (e.g. due to an exception). Doesn't have to be 100% perfect, any code might throw an exception between shader.Use() and shader.Stop() - this is definitely "good enough"
        }
    }

    public class BlockEntitySignRenderer : IRenderer
    {
        /// <summary>
        /// Special value, intended to be immediately followed by AfterSignRendererOrder with no other renderers in between nor with the same values
        /// </summary>
        public static double SignRendererOrder = 1.101;
        /// <summary>
        /// Special value, intended to be immediately preceded by SignRendererOrder with no other renderers in between nor with the same values
        /// </summary>
        public static double AfterSignRendererOrder = 1.102;

        private static readonly double[] blackColor = { 0, 0, 0, 0.8 };
        protected int TextWidth = 200;
        protected int TextHeight = 100;

        protected float QuadWidth = 14/16f;
        protected float QuadHeight = 6.5f/16f;

        protected string text;
        protected CairoFont font;
        protected BlockPos pos;
        protected ICoreClientAPI api;

        protected LoadedTexture loadedTexture;
        protected MeshRef quadModelRef;
        public Matrixf ModelMat = new Matrixf();

        public float rotY = 0;
        protected float translateX = 0;
        protected float translateY = 0;
        protected float translateZ = 0;

        protected float offsetX;
        protected float offsetY;
        protected float offsetZ;

        public float fontSize =20;
        EnumVerticalAlign verticalAlign;
        internal bool translateable;

        // Some static fields used for Performance control in OnRenderFrame()
        private static float lastDt = -1;
        private static int renderTextMaxCount;
        public static IStandardShaderProgram progCached;


        public double RenderOrder
        {
            get { return SignRendererOrder; } // 11.11.2020 - this was 0.5 but that causes issues with signs + chest animation
            // radfast 08.09.2025 - we use a unique RenderOrder, so that all signs render together, for efficiency in using the same shader
        }

        public int RenderRange
        {
            get { return 24; }
        }

        public BlockEntitySignRenderer(BlockPos pos, ICoreClientAPI api, TextAreaConfig config)
        {
            this.api = api;
            this.pos = pos;
            if (config == null) config = new TextAreaConfig();

            this.fontSize = config.FontSize;
            this.QuadWidth = config.textVoxelWidth / 16f;
            this.QuadHeight = config.textVoxelHeight / 16f;
            this.verticalAlign = config.VerticalAlign;
            this.TextWidth = config.MaxWidth;

            font = new CairoFont(this.fontSize, config.FontName, blackColor);
            if (config.BoldFont) font.WithWeight(Cairo.FontWeight.Bold);
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

            translateY = 9 / 16f;

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

            this.offsetX += config.textVoxelOffsetX / 16f;
            this.offsetY += config.textVoxelOffsetY / 16f;
            this.offsetZ += config.textVoxelOffsetZ / 16f;
        }

        public void SetFreestanding(float angleRad)
        {
            rotY = 180 + angleRad * GameMath.RAD2DEG;
            translateX = 8f / 16f;
            translateZ = 8f / 16f;
            offsetZ = -1.51f / 16f;
        }

        public virtual void SetNewText(string text, int color)
        {
            if (translateable)
            {
                string translatedText = Lang.Get(text);
                if (Lang.UsesNonLatinCharacters(Lang.CurrentLocale))
                {
                    string englishText = Lang.GetL(Lang.DefaultLocale, text);
                    if (translatedText != englishText)   // If it has been translated, and it's a non-Latin font, use the configured standard font not the font stored in the sign/plaque
                    {
                        font.Fontname = GuiStyle.StandardFontName; // Asian languages probably can't use any fancy font such as Almendra; Github #4748
                    }
                }

                text = translatedText;
            }

            font.Color = ColorUtil.ToRGBADoubles(color);
            loadedTexture?.Dispose();
            loadedTexture = null;
            this.text = text;
        }

        protected LoadedTexture RenderText()
        {
            font.UnscaledFontsize = fontSize / RuntimeEnv.GUIScale;

            double verPadding = verticalAlign == EnumVerticalAlign.Middle ? (TextHeight - api.Gui.Text.GetMultilineTextHeight(font, text, TextWidth)) : 0;
            var bg = new TextBackground()
            {
                VerPadding = (int)verPadding / 2
            };

            return api.Gui.TextTexture.GenTextTexture(
                text,
                font,
                TextWidth,
                TextHeight,
                bg,
                EnumTextOrientation.Center,
                false
            );
        }

        public virtual void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            if (loadedTexture == null)
            {
                if (text != null && text.Length > 0)
                {
                    if (lastDt != deltaTime)
                    {
                        lastDt = deltaTime;
                        renderTextMaxCount = 32;
                    }
                    if (renderTextMaxCount-- <= 0) return;    // For performance, limit the number of new sign texts rendered each frame

                    loadedTexture = RenderText();
                }
                else return;
            }
            if (!api.Render.DefaultFrustumCuller.SphereInFrustum(pos.X + 0.5, pos.InternalY + 0.5, pos.Z + 0.5, 1)) return;

            IRenderAPI rpi = api.Render;
            Vec3d camPos = api.World.Player.Entity.CameraPos;

            rpi.GlToggleBlend(true, EnumBlendMode.PremultipliedAlpha);
            var prog = progCached;
            if (prog == null)
            {
                rpi.GlDisableCullFace();

                progCached = prog = rpi.PreparedStandardShader(pos.X, pos.InternalY, pos.Z);

                prog.ViewMatrix = rpi.CameraMatrixOriginf;
                prog.ProjectionMatrix = rpi.CurrentProjectionMatrix;
                prog.NormalShaded = 0;
                prog.ExtraGodray = 0;
                prog.SsaoAttn = 0;
                prog.AlphaTest = 0.05f;
                prog.OverlayOpacity = 0;
            }
            else
            {
                prog.RgbaLightIn = api.World.BlockAccessor.GetLightRGBs(pos);
            }

            prog.Tex2D = loadedTexture.TextureId;
            prog.ModelMatrix = ModelMat
                .Identity()
                .Translate(pos.X - camPos.X + translateX, pos.InternalY - camPos.Y + translateY, pos.Z - camPos.Z + translateZ)
                .RotateY(rotY * GameMath.DEG2RAD)
                .Translate(offsetX, offsetY, offsetZ)
                .Scale(0.5f * QuadWidth, 0.5f * QuadHeight, 0.5f * QuadWidth)
                .Values
            ;

            rpi.RenderMesh(quadModelRef);
            rpi.GlToggleBlend(true, EnumBlendMode.Standard);
        }

        /// <summary>
        /// To be called once after all OnRenderFrame() calls for the current frame.
        /// (This is achieved by calling this from an AfterSignRenderer renderer)
        /// </summary>
        /// <param name="api"></param>
        public static void AfterRenderFrameOnce()
        {
            if (progCached != null)
            {
                var prog = progCached;
                progCached = null;
                prog.Stop();
            }
            lastDt = -1;
        }

        public void Dispose()
        {
            api.Event.UnregisterRenderer(this, EnumRenderStage.Opaque);
            loadedTexture?.Dispose();
            quadModelRef?.Dispose();
        }

        public static void RegisterAndReserveRenderOrderRange(ICoreClientAPI api)
        {
            api.Event.RegisterRenderer(new AfterSignRenderer(), EnumRenderStage.Opaque, "aftersign", SignRendererOrder, AfterSignRendererOrder, typeof(BlockEntitySignRenderer));
        }
    }
}
