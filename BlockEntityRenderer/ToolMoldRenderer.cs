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
    public class ToolMoldRenderer : IRenderer
    {
        BlockPos pos;
        ICoreClientAPI api;


        MeshRef[] quadModelRefs;
        public Matrixf ModelMat = new Matrixf();

        public double RenderOrder
        {
            get { return 0.5; }
        }

        public int RenderRange
        {
            get { return 24; }
        }

        /// <summary>
        /// 0..1
        /// </summary>
        public float Level = 0;
        /// <summary>
        /// 0..1300
        /// </summary>
        public float Temperature = 0;

        public AssetLocation TextureName = null;

        internal Cuboidf[] fillQuadsByLevel;

        public ToolMoldRenderer(BlockPos pos, ICoreClientAPI api, Cuboidf[] fillQuadsByLevel = null)
        {
            this.pos = pos;
            this.api = api;

            this.fillQuadsByLevel = fillQuadsByLevel;

            quadModelRefs = new MeshRef[fillQuadsByLevel.Length];
            MeshData modeldata = QuadMeshUtil.GetQuad();
            modeldata.Rgba = new byte[4 * 4];
            modeldata.Rgba.Fill((byte)255);
            modeldata.Flags = new int[4 * 4];

            for (int i = 0; i < quadModelRefs.Length; i++)
            {
                Cuboidf size = fillQuadsByLevel[i];

                modeldata.Uv = new float[]
                {
                    size.X2/16f, size.Z2/16f,
                    size.X1/16f, size.Z2/16f,
                    size.X1/16f, size.Z1/16f,
                    size.X2/16f, size.Z1/16f
                };

                quadModelRefs[i] = api.Render.UploadMesh(modeldata);
            }
        }


        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            if (Level <= 0 || TextureName == null) return;

            int voxelY = (int)GameMath.Clamp(Level, 0, fillQuadsByLevel.Length - 1);

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
            prog.ExtraGodray = 0;
            prog.NormalShaded = 0;

            Vec4f lightrgbs = api.World.BlockAccessor.GetLightRGBs(pos.X, pos.Y, pos.Z);
            float[] glowColor = ColorUtil.GetIncandescenceColorAsColor4f((int)Temperature);
            int extraGlow = (int)GameMath.Clamp((Temperature - 550) / 2, 0, 255);

            prog.RgbaLightIn = lightrgbs;
            prog.RgbaGlowIn = new Vec4f(glowColor[0], glowColor[1], glowColor[2], extraGlow / 255f);
            prog.RgbaBlockIn = ColorUtil.WhiteArgbVec;
            prog.ExtraGlow = extraGlow;

            int texid = api.Render.GetOrLoadTexture(TextureName);
            Cuboidf rect = fillQuadsByLevel[voxelY];

            rpi.BindTexture2d(texid);

            prog.ModelMatrix = ModelMat
                .Identity()
                .Translate(pos.X - camPos.X, pos.Y - camPos.Y, pos.Z - camPos.Z)
                .Translate(1 - rect.X1 / 16f, 1.01f / 16f + Math.Max(0, Level / 16f - 0.0625f / 3), 1 - rect.Z1 / 16f)
                .RotateX(90 * GameMath.DEG2RAD)
                .Scale(0.5f * rect.Width / 16f, 0.5f * rect.Length / 16f, 0.5f)
                .Translate(-1, -1, 0)
                .Values
            ;

            prog.ViewMatrix = rpi.CameraMatrixOriginf;
            prog.ProjectionMatrix = rpi.CurrentProjectionMatrix;
            
            rpi.RenderMesh(quadModelRefs[voxelY]);
            
            prog.Stop();

            rpi.GlEnableCullFace();
        }


        public void Dispose()
        {
            api.Event.UnregisterRenderer(this, EnumRenderStage.Opaque);

            for (int i = 0; i < quadModelRefs.Length; i++)
            {
                quadModelRefs[i]?.Dispose();
            }

            
        }
    }
}
