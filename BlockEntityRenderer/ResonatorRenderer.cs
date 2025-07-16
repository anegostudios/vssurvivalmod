using System;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent
{
    public class ResonatorRenderer : IRenderer
    {
        private ICoreClientAPI api;
        private BlockPos pos;

        public MeshRef cylinderMeshRef;

        public Vec3f discPos = new Vec3f(0, 0.7f, 0);
        public Vec3f discRotRad = new Vec3f(0, 0, 0);

        
        Matrixf ModelMat = new Matrixf();
        float blockRotation;

        long updatedTotalMs;

        public ResonatorRenderer(BlockPos pos, ICoreClientAPI capi, float blockRot)
        {
            this.pos = pos;
            this.api = capi;
            this.blockRotation = blockRot;
        }

        public double RenderOrder
        {
            get { return 0.5; }
        }

        public int RenderRange
        {
            get { return 24; }
        }

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            if (cylinderMeshRef == null) return;

            long ellapsedMs = api.InWorldEllapsedMilliseconds;

            IRenderAPI rpi = api.Render;
            IClientWorldAccessor worldAccess = api.World;
            Vec3d camPos = worldAccess.Player.Entity.CameraPos;
            Vec4f lightrgbs = api.World.BlockAccessor.GetLightRGBs(pos.X, pos.Y, pos.Z);

            rpi.GlDisableCullFace();

            IStandardShaderProgram prog = rpi.StandardShader;
            prog.Use();
            prog.ExtraGlow = 0;
            prog.RgbaAmbientIn = rpi.AmbientColor;
            prog.RgbaFogIn = rpi.FogColor;
            prog.FogMinIn = rpi.FogMin;
            prog.FogDensityIn = rpi.FogDensity;
            prog.RgbaTint = ColorUtil.WhiteArgbVec;
            prog.RgbaLightIn = lightrgbs;
            
            prog.DontWarpVertices = 0;
            prog.AddRenderFlags = 0;
            prog.ExtraGodray = 0;
            prog.NormalShaded = 1;

            rpi.BindTexture2d(api.ItemTextureAtlas.AtlasTextures[0].TextureId);

            float origx = -8f/16f;
            float origy = -8f/16f;
            float origz = -0.5f;

            discPos.X = -4f / 16f;
            discPos.Y = 10.3f / 16f;
            discPos.Z = 2.2f / 16f;

            discRotRad.X = 0;
            discRotRad.Y = (ellapsedMs - updatedTotalMs) / 500f * GameMath.PI;
            discRotRad.Z = 0;

            prog.NormalShaded = 0;
            prog.ModelMatrix = ModelMat
                .Identity()
                .Translate(pos.X - camPos.X, pos.Y - camPos.Y, pos.Z - camPos.Z)
                .Translate(-origx, 0, -origz)
                .RotateYDeg(blockRotation)
                .Translate(discPos.X, discPos.Y, discPos.Z)
                .Rotate(discRotRad)
                .Scale(0.9f, 0.9f, 0.9f)
                .Translate(origx, origy, origz)
                .Values
            ;
            rpi.RenderMesh(cylinderMeshRef);

            prog.Stop();
        }

        internal void UpdateMeshes(MeshData cylinderMesh)
        {
            cylinderMeshRef?.Dispose();
            cylinderMeshRef = null;

            if (cylinderMesh != null)
            {
                cylinderMeshRef = api.Render.UploadMesh(cylinderMesh);                
            }

            updatedTotalMs = api.InWorldEllapsedMilliseconds;
        }

        public void Dispose()
        {
            api.Event.UnregisterRenderer(this, EnumRenderStage.Opaque);
            api.Event.UnregisterRenderer(this, EnumRenderStage.AfterFinalComposition);
            cylinderMeshRef?.Dispose();
        }
    }
}
