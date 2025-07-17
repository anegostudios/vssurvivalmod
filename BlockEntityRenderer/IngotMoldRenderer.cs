using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent
{
    public class IngotMoldRenderer : IRenderer
    {
        BlockPos pos;
        ICoreClientAPI api;
        MeshRef quadModelRef;
        Matrixf ModelMat = new Matrixf();

        public double RenderOrder => 0.5;
        public int RenderRange => 24;

        /// <summary>
        /// 0..100
        /// </summary>
        public int LevelLeft = 0;
        public int LevelRight = 0;
        /// <summary>
        /// 0..1300
        /// </summary>
        public float TemperatureLeft = 0;
        public float TemperatureRight = 0;

        public AssetLocation TextureNameLeft = null;
        public AssetLocation TextureNameRight = null;

        public int QuantityMolds = 1;
        private readonly BlockEntityIngotMold entity;

        public ItemStack stack;

        public IngotMoldRenderer(BlockEntityIngotMold beim, ICoreClientAPI api)
        {
            this.pos = beim.Pos;
            this.api = api;
            entity = beim;

            MeshData modeldata = QuadMeshUtil.GetQuad();
            modeldata.Uv = new float[]
            {
                3/16f, 7/16f,
                0, 7/16f,
                0, 0,
                3/16f, 0
            };

            modeldata.Rgba = new byte[4 * 4];
            modeldata.Rgba.Fill((byte)255);
            modeldata.Flags = new int[4 * 4];

            quadModelRef = api.Render.UploadMesh(modeldata);
        }


        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            if (LevelLeft <= 0 && LevelRight <= 0) return;

            IRenderAPI rpi = api.Render;
            IClientWorldAccessor worldAccess = api.World;
            Vec3d camPos = worldAccess.Player.Entity.CameraPos;

            rpi.GlDisableCullFace();
            IStandardShaderProgram prog = rpi.StandardShader;
            prog.Use();
            prog.RgbaAmbientIn = rpi.AmbientColor;
            prog.RgbaFogIn = rpi.FogColor;
            prog.FogMinIn = rpi.FogMin;
            prog.FogDensityIn = rpi.FogDensity;
            prog.RgbaTint = ColorUtil.WhiteArgbVec;
            prog.DontWarpVertices = 0;
            prog.ExtraGodray = 0;
            prog.AddRenderFlags = 0;
            if (stack != null)
            {
                prog.AverageColor = ColorUtil.ToRGBAVec4f(api.BlockTextureAtlas.GetAverageColor((stack.Item?.FirstTexture ?? stack.Block.FirstTextureInventory).Baked.TextureSubId));
                prog.TempGlowMode = 1;
            }
            


            if (LevelLeft > 0 && TextureNameLeft != null)
            {
                Vec4f lightrgbs = api.World.BlockAccessor.GetLightRGBs(pos.X, pos.Y, pos.Z);
                float[] glowColor = ColorUtil.GetIncandescenceColorAsColor4f((int)TemperatureLeft);
                int extraGlow = (int)GameMath.Clamp((TemperatureLeft - 550) / 1.5f, 0, 255);

                prog.RgbaLightIn = lightrgbs;
                prog.RgbaGlowIn = new Vec4f(glowColor[0], glowColor[1], glowColor[2], extraGlow / 255f);

                prog.ExtraGlow = extraGlow;
                prog.NormalShaded = 0;

                int texid = api.Render.GetOrLoadTexture(TextureNameLeft);
                rpi.BindTexture2d(texid);

                float xzOffset = QuantityMolds > 1 ? 4.5f : 8.5f;

                ModelMat
                    .Identity()
                    .Translate(pos.X - camPos.X, pos.Y - camPos.Y, pos.Z - camPos.Z)

                    .Translate(0.5f,0f,0.5f)
                    .RotateY(entity.MeshAngle)
                    .Translate(-0.5f,0f,-0.5f)

                    .Translate(xzOffset / 16f, 1 / 16f + LevelLeft / 850f, 8.5f / 16)

                    .RotateX(90 * GameMath.DEG2RAD)

                    .Scale(0.5f * 3 / 16f, 0.5f * 7 / 16f, 0.5f)
                ;
                prog.ModelMatrix = ModelMat.Values;
                prog.ProjectionMatrix = rpi.CurrentProjectionMatrix;
                prog.ViewMatrix = rpi.CameraMatrixOriginf;

                rpi.RenderMesh(quadModelRef);
            }

            if (LevelRight > 0 && QuantityMolds > 1 && TextureNameRight != null)
            {
                Vec4f lightrgbs = api.World.BlockAccessor.GetLightRGBs(pos.X, pos.Y, pos.Z);
                float[] glowColor = ColorUtil.GetIncandescenceColorAsColor4f((int)TemperatureRight);
                int extraGlow = (int)GameMath.Clamp((TemperatureRight - 550) / 1.5f, 0, 255);

                prog.RgbaLightIn = lightrgbs;
                prog.RgbaGlowIn = new Vec4f(glowColor[0], glowColor[1], glowColor[2], extraGlow / 255f);

                prog.ExtraGlow = extraGlow;
                prog.NormalShaded = 0;

                int texid = api.Render.GetOrLoadTexture(TextureNameRight);
                rpi.BindTexture2d(texid);

                ModelMat
                    .Identity()
                    .Translate(pos.X - camPos.X, pos.Y - camPos.Y, pos.Z - camPos.Z)
                    .Translate(0.5f,0f,0.5f)
                    .RotateY(entity.MeshAngle)
                    .Translate(-0.5f,0f,-0.5f)
                    .Translate(11.5f / 16f, 1 / 16f + LevelRight / 850f, 8.5f / 16)
                    .RotateX(90 * GameMath.DEG2RAD)

                    .Scale(0.5f * 3 / 16f, 0.5f * 7 / 16f, 0.5f)
                    ;
                prog.ModelMatrix = ModelMat.Values;
                prog.ProjectionMatrix = rpi.CurrentProjectionMatrix;
                prog.ViewMatrix = rpi.CameraMatrixOriginf;


                rpi.RenderMesh(quadModelRef);

            }


            prog.Stop();
            rpi.GlEnableCullFace();
        }

        public void Dispose()
        {
            api.Event.UnregisterRenderer(this, EnumRenderStage.Opaque);

            quadModelRef?.Dispose();
        }
    }
}
