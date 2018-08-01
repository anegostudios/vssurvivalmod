using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class QuernTopRenderer : IRenderer
    {
        internal bool ShouldRender;
        internal bool ShouldRotate;

        private ICoreClientAPI api;
        private BlockPos pos;
        

        MeshRef meshref;


        public float Angle;

        public QuernTopRenderer(ICoreClientAPI coreClientAPI, BlockPos pos, MeshData mesh)
        {
            this.api = coreClientAPI;
            this.pos = pos;

            meshref = coreClientAPI.Render.UploadMesh(mesh);
        }

        public double RenderOrder
        {
            get { return 0.5; }
        }

        public int RenderRange => 24;




        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            if (meshref == null || !ShouldRender) return;

            IRenderAPI rpi = api.Render;
            Vec3d camPos = api.World.Player.Entity.CameraPos;

            rpi.GlDisableCullFace();
            rpi.GlToggleBlend(true);

            IStandardShaderProgram prog = rpi.PreparedStandardShader(pos.X, pos.Y, pos.Z);
            prog.Tex2D = api.BlockTextureAtlas.AtlasTextureIds[0];
            api.Render.GlMatrixModeModelView();

            api.Render.GlPushMatrix();
            api.Render.GlLoadMatrix(api.Render.CameraMatrixOrigin);

            rpi.GlTranslate(pos.X - camPos.X, pos.Y - camPos.Y, pos.Z - camPos.Z);

            rpi.GlTranslate(0.5f, 11f / 16f, 0.5f);
            rpi.GlRotate(Angle, 0, 1, 0);
            rpi.GlTranslate(-0.5f, 0, -0.5f);
            prog.ModelViewMatrix = rpi.CurrentModelviewMatrix;

            rpi.RenderMesh(meshref);

            rpi.GlPopMatrix();

            prog.Stop();

            if (ShouldRotate)
            {
                Angle += deltaTime * 40;
            }
        }


        internal void Unregister()
        {
            api.Event.UnregisterRenderer(this, EnumRenderStage.Opaque);
        }

        public void Dispose()
        {
            meshref.Dispose();
        }


    }
}
