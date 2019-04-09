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

namespace Vintagestory.GameContent
{
    public class EchoChamberRenderer : IRenderer
    {
        private ICoreClientAPI api;
        private BlockPos pos;

        public MeshRef needleMeshRef;
        public MeshRef discMeshRef;

        public Vec3f needlePos = new Vec3f(0, 0.75f, 0);
        public Vec3f needleRotRad = new Vec3f(0, 1, 0);

        public Vec3f discPos = new Vec3f(0, 0.7f, 0);
        public Vec3f discRotRad = new Vec3f(0, 0, 0);

        
        Matrixf ModelMat = new Matrixf();
        float blockRotation;

        long updatedTotalMs;

        public EchoChamberRenderer(BlockPos pos, ICoreClientAPI capi, float blockRot)
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
            if (needleMeshRef == null || discMeshRef == null) return;

            long ellapsedMs = api.World.ElapsedMilliseconds;

            IRenderAPI rpi = api.Render;
            IClientWorldAccessor worldAccess = api.World;
            Vec3d camPos = worldAccess.Player.Entity.CameraPos;
            EntityPos plrPos = worldAccess.Player.Entity.Pos;
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
            prog.RgbaBlockIn = ColorUtil.WhiteArgbVec;
            prog.DontWarpVertices = 0;
            prog.AddRenderFlags = 0;

            rpi.BindTexture2d(api.BlockTextureAtlas.AtlasTextureIds[0]);

            float origx = -0.5f;
            float origz = -0.5f;

            needlePos.X = 3.5f / 16f;
            needlePos.Y = 10.8f / 16f;
            needlePos.Z = -3.5f / 16f;

            // because i'm a noob and lazy
            switch (blockRotation)
            {
                case 90: needlePos.X -= 7 / 16f; break;
                case 180: needlePos.X -= 7 / 16f; needlePos.Z += 7 / 16f; break;
                case 270: needlePos.Z += 7 / 16f; break;
            }

            float wobble = GameMath.Sin(Math.Max(0, ellapsedMs - updatedTotalMs) / 50f - 0.5f) / 80f;

            needleRotRad.Y = -GameMath.PIHALF + Math.Min(0.4f, (ellapsedMs - updatedTotalMs) / 700f) + wobble + blockRotation*GameMath.DEG2RAD;
            needleRotRad.X = wobble;

            prog.ModelMatrix = ModelMat
                .Identity()
                .Translate(pos.X - camPos.X + needlePos.X, pos.Y - camPos.Y + needlePos.Y, pos.Z - camPos.Z + needlePos.Z)
                .Translate(-origx, 0, -origz)
                .Rotate(needleRotRad)
                .Translate(origx, 0, origz)
                .Values
            ;

            prog.ViewMatrix = rpi.CameraMatrixOriginf;
            prog.ProjectionMatrix = rpi.CurrentProjectionMatrix;

            rpi.RenderMesh(needleMeshRef);



            rpi.BindTexture2d(api.ItemTextureAtlas.AtlasTextureIds[0]);

            origx = -9.25f/16f;
            float origy = -9.5f/16f;
            origz = -0.5f;

            discPos.X = -1.25f / 16f;
            discPos.Y = 11.2f / 16f;
            discPos.Z = 0f / 16f;

            discRotRad.X = GameMath.PIHALF;
            discRotRad.Z = -Math.Max(0, (ellapsedMs - updatedTotalMs) / 500f - 0.5f);

            prog.ModelMatrix = ModelMat
                .Identity()
                .Translate(pos.X - camPos.X + discPos.X, pos.Y - camPos.Y + discPos.Y, pos.Z - camPos.Z + discPos.Z)
                .Translate(-origx, 0, -origz)
                .Rotate(discRotRad)
                .Scale(0.45f, 0.45f, 0.45f)
                .Translate(origx, origy, origz)
                .Values
            ;
            rpi.RenderMesh(discMeshRef);

            prog.Stop();
        }

        internal void UpdateMeshes(MeshData needleMesh, MeshData discMesh)
        {
            Dispose();
            needleMeshRef = null;
            discMeshRef = null;

            if (needleMesh != null)
            {
                needleMeshRef = api.Render.UploadMesh(needleMesh);
            } 
            if (discMesh != null)
            {
                discMeshRef = api.Render.UploadMesh(discMesh);
            }

            updatedTotalMs = api.World.ElapsedMilliseconds;
        }

        public void Unregister()
        {
            api.Event.UnregisterRenderer(this, EnumRenderStage.Opaque);
            api.Event.UnregisterRenderer(this, EnumRenderStage.AfterFinalComposition);
        }

        // Called by UnregisterRenderer
        public void Dispose()
        {
            needleMeshRef?.Dispose();
            discMeshRef?.Dispose();
        }
    }
}
