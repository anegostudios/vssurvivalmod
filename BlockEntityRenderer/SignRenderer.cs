using Vintagestory.API;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class BlockEntitySignRenderer : IRenderer
    {
        static int TextWidth = 200;
        static int TextHeight = 100;

        static float QuadWidth = 0.9f;
        static float QuadHeight = 0.45f;


        ICairoFont font;
        BlockPos pos;
        ICoreClientAPI api;

        LoadedTexture loadedTexture;
        MeshRef quadModelRef;
        public Matrixf ModelMat = new Matrixf();

        float rotY = 0;
        float translateX = 0;
        float translateZ = 0;

        public double RenderOrder
        {
            get { return 0.5; }
        }

        public int RenderRange
        {
            get { return 48; }
        }

        public BlockEntitySignRenderer(BlockPos pos, ICoreClientAPI api)
        {
            this.api = api;
            this.pos = pos;
            font = api.Render.GetFont(20, api.Render.StandardFontName, new double[] { 0, 0, 0, 0.8 });

            api.Event.RegisterRenderer(this, EnumRenderStage.Opaque);

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
            modeldata.Rgba2 = new byte[4 * 4];
            modeldata.Rgba2.Fill((byte)255);

            quadModelRef = api.Render.UploadMesh(modeldata);

            Block block = api.World.BlockAccessor.GetBlock(pos);
            BlockFacing facing = BlockFacing.FromCode(block.LastCodePart());
            if (facing == null) return;

            float wallOffset = block.LastCodePart(1) == "wall" ? 0.25f : 0;

            switch (facing.Index)
            {
                case 0: // N
                    translateX = 0.5f;
                    translateZ = 1 - 0.68f - wallOffset;
                    rotY = 180;
                    break;
                case 1: // E
                    translateX = 0.68f + wallOffset;
                    translateZ = 0.5f;
                    rotY = 90;
                    break;
                case 2: // S
                    translateX = 0.5f;
                    translateZ = 0.68f + wallOffset;

                    break;
                case 3: // W
                    translateX = 1 - 0.68f - wallOffset;
                    translateZ = 0.5f;
                    rotY = 270;

                    break;


            }

        }

        internal void SetNewText(string text)
        {
            loadedTexture?.Dispose();
            loadedTexture = api.Gui.Text.GenTextTexture(text, font, TextWidth, TextHeight, null, EnumTextOrientation.Center, 0.9f);
        }




        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            if (loadedTexture == null) return;

            IRenderAPI rpi = api.Render;
            Vec3d camPos = api.World.Player.Entity.CameraPos;

            rpi.GlDisableCullFace();
            rpi.GlToggleBlend(true);

            IStandardShaderProgram prog = rpi.PreparedStandardShader(pos.X, pos.Y, pos.Z);

            prog.Tex2D = loadedTexture.TextureId;

            prog.ModelMatrix = ModelMat
                .Identity()
                .Translate(pos.X - camPos.X, pos.Y - camPos.Y, pos.Z - camPos.Z)
                .Translate(translateX, 0.5625f, translateZ)
                .RotateY(rotY * GameMath.DEG2RAD)
                .Scale(0.45f * QuadWidth, 0.45f * QuadHeight, 0.45f * QuadWidth)
                .Values
            ;

            prog.ViewMatrix = rpi.CameraMatrixOriginf;
            prog.ProjectionMatrix = rpi.CurrentProjectionMatrix;
            

            rpi.RenderMesh(quadModelRef);
            prog.Stop();
        }

        public void Unregister()
        {
            api.Event.UnregisterRenderer(this, EnumRenderStage.Opaque);
        }

        // Called by UnregisterRenderer
        public void Dispose()
        {
            loadedTexture?.Dispose();
            quadModelRef?.Dispose();
        }

    }
}
