using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class IngotMoldRenderer : IRenderer
    {
        BlockPos pos;
        ICoreClientAPI api;
        MeshRef quadModelRef;
        Matrixf ModelMat = new Matrixf();

        public double RenderOrder
        {
            get { return 0.5; }
        }

        public int RenderRange
        {
            get { return 24; }
        }

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

        public IngotMoldRenderer(BlockPos pos, ICoreClientAPI api)
        {
            this.pos = pos;
            this.api = api;

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
            EntityPos plrPos = worldAccess.Player.Entity.Pos;
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
            prog.AddRenderFlags = 0;

            if (LevelLeft > 0 && TextureNameLeft != null)
            {
                Vec4f lightrgbs = api.World.BlockAccessor.GetLightRGBs(pos.X, pos.Y, pos.Z);
                float[] glowColor = ColorUtil.GetIncandescenceColorAsColor4f((int)TemperatureLeft);
                lightrgbs[0] += 2 * glowColor[0];
                lightrgbs[1] += 2 * glowColor[1];
                lightrgbs[2] += 2 * glowColor[2];

                prog.RgbaLightIn = lightrgbs;
                prog.RgbaBlockIn = ColorUtil.WhiteArgbVec;
                prog.ExtraGlow = (int)GameMath.Clamp((TemperatureLeft - 500) / 4, 0, 255);

                int texid = api.Render.GetOrLoadTexture(TextureNameLeft);
                rpi.BindTexture2d(texid);
         
                float xzOffset = (QuantityMolds > 1) ? 4.5f : 8.5f;

                prog.ModelMatrix = ModelMat
                    .Identity()
                    .Translate(pos.X - camPos.X, pos.Y - camPos.Y, pos.Z - camPos.Z)
                    .Translate(xzOffset / 16f, 1 / 16f + LevelLeft / 850f, 8.5f / 16)
                    .RotateX(90 * GameMath.DEG2RAD)
                    .Scale(0.5f * 3 / 16f, 0.5f * 7 / 16f, 0.5f)
                    .Values
                ;
                prog.ProjectionMatrix = rpi.CurrentProjectionMatrix;
                prog.ViewMatrix = rpi.CameraMatrixOriginf;

                rpi.RenderMesh(quadModelRef);
            }
            

            if (LevelRight > 0 && QuantityMolds > 1 && TextureNameRight != null)
            {
                Vec4f lightrgbs = api.World.BlockAccessor.GetLightRGBs(pos.X, pos.Y, pos.Z);
                float[] glowColor = ColorUtil.GetIncandescenceColorAsColor4f((int)TemperatureRight);
                lightrgbs[0] += 2 * glowColor[0];
                lightrgbs[1] += 2 * glowColor[1];
                lightrgbs[2] += 2 * glowColor[2];

                prog.RgbaLightIn = lightrgbs;
                prog.RgbaBlockIn = ColorUtil.WhiteArgbVec;
                prog.ExtraGlow = (int)GameMath.Clamp((TemperatureRight - 500) / 4, 0, 255);


                int texid = api.Render.GetOrLoadTexture(TextureNameRight);
                rpi.BindTexture2d(texid);

                /*
                rpi.GlPushMatrix();
                rpi.GlLoadMatrix(api.Render.CameraMatrixOrigin);
                rpi.GlTranslate(pos.X - camPos.X, pos.Y - camPos.Y, pos.Z - camPos.Z);
                rpi.GlTranslate(11.5f / 16f, 1 / 16f + LevelRight / 850f, 8.5f / 16);
                rpi.GlRotate(90, 1, 0, 0);
                rpi.GlScale(0.5f * 3 / 16f, 0.5f * 7 / 16f, 0.5f);
                prog.ProjectionMatrix = rpi.CurrentProjectionMatrix;
                prog.ModelViewMatrix = rpi.CurrentModelviewMatrix;
                rpi.GlPopMatrix();
                */

                prog.ModelMatrix = ModelMat
                    .Identity()
                    .Translate(pos.X - camPos.X, pos.Y - camPos.Y, pos.Z - camPos.Z)
                    .Translate(11.5f / 16f, 1 / 16f + LevelRight / 850f, 8.5f / 16)
                    .RotateX(90 * GameMath.DEG2RAD)
                    .Scale(0.5f * 3 / 16f, 0.5f * 7 / 16f, 0.5f)
                    .Values
                ;
                prog.ProjectionMatrix = rpi.CurrentProjectionMatrix;
                prog.ViewMatrix = rpi.CameraMatrixOriginf;


                rpi.RenderMesh(quadModelRef);
                
            }


            prog.Stop();
            rpi.GlEnableCullFace();
        }

        public void Unregister()
        {
            api.Event.UnregisterRenderer(this, EnumRenderStage.Opaque);
        }

        // Called by UnregisterRenderer
        public void Dispose()
        {
            api.Render.DeleteMesh(quadModelRef);
        }
    }
}
