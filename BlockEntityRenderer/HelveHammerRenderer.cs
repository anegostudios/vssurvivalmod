using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent.Mechanics;

namespace Vintagestory.GameContent
{
    public class HelveHammerRenderer : IRenderer
    {
        internal bool ShouldRender;
        internal bool ShouldRotateManual;
        internal bool ShouldRotateAutomated;

        
        BEHelveHammer be;

        private ICoreClientAPI api;
        private BlockPos pos;
        

        MeshRef meshref;
        public Matrixf ModelMat = new Matrixf();

        public float AngleRad;
        internal bool Obstructed;

        

        public HelveHammerRenderer(ICoreClientAPI coreClientAPI, BEHelveHammer be, BlockPos pos, MeshData mesh)
        {
            this.api = coreClientAPI;
            this.pos = pos;
            this.be = be;

            mesh.Rgba2 = null;
            meshref = coreClientAPI.Render.UploadMesh(mesh);
        }

        public double RenderOrder
        {
            get { return 0.5; }
        }

        public int RenderRange => 24;



        Matrixf shadowMvpMat = new Matrixf();
        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            if (meshref == null || be.HammerStack == null) return;

            IRenderAPI rpi = api.Render;
            Vec3d camPos = api.World.Player.Entity.CameraPos;

            rpi.GlDisableCullFace();
            
            float rotY = be.facing.HorizontalAngleIndex * 90;
            float offx = be.facing == BlockFacing.NORTH || be.facing == BlockFacing.WEST ? -1 / 16f : 17 / 16f;
            ModelMat
                    .Identity()
                    .Translate(pos.X - camPos.X, pos.Y - camPos.Y, pos.Z - camPos.Z)
                    .RotateYDeg(rotY)
                    .Translate(offx, 12.5f / 16f, 0.5f)
                    .RotateZ(AngleRad)
                    .Translate(-offx, -12.5f / 16f, -0.5f)
                    .RotateYDeg(-rotY)
            ;

            if (stage == EnumRenderStage.Opaque)
            {
                IStandardShaderProgram prog = rpi.PreparedStandardShader(pos.X, pos.Y, pos.Z);
                prog.Tex2D = api.BlockTextureAtlas.AtlasTextureIds[0];


                prog.ModelMatrix = ModelMat.Values;
                prog.ViewMatrix = rpi.CameraMatrixOriginf;
                prog.ProjectionMatrix = rpi.CurrentProjectionMatrix;
                rpi.RenderMesh(meshref);
                prog.Stop();

                AngleRad = be.Angle;
            } else
            {
                IRenderAPI rapi = api.Render;
                shadowMvpMat.Set(rapi.CurrentProjectionMatrix).Mul(rapi.CurrentModelviewMatrix).Mul(ModelMat.Values);

                rapi.CurrentActiveShader.BindTexture2D("tex2d", api.BlockTextureAtlas.AtlasTextureIds[0], 0);
                rapi.CurrentActiveShader.UniformMatrix("mvpMatrix", shadowMvpMat.Values);
                rapi.CurrentActiveShader.Uniform("origin", new Vec3f());

                rpi.RenderMesh(meshref);
            }

        }


        public void Dispose()
        {
            api.Event.UnregisterRenderer(this, EnumRenderStage.Opaque);
            api.Event.UnregisterRenderer(this, EnumRenderStage.ShadowFar);
            api.Event.UnregisterRenderer(this, EnumRenderStage.ShadowNear);

            meshref.Dispose();
        }


    }
}
