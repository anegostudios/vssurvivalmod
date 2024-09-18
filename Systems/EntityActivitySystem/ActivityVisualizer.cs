using System;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;
using Vintagestory.Essentials;

namespace Vintagestory.GameContent
{
    public class ActivityVisualizer : IRenderer
    {
        private EntityActivity entityActivity;
        private ICoreClientAPI capi;

        MeshData pathModel;
        MeshRef pathModelRef;
        Vec3d origin;

        public ActivityVisualizer(EntityActivity entityActivity)
        {
            this.entityActivity = entityActivity;
        }

        public ActivityVisualizer(ICoreClientAPI capi, EntityActivity entityActivity)
        {
            this.capi = capi;
            this.entityActivity = entityActivity;

            capi.Event.RegisterRenderer(this, EnumRenderStage.Opaque, "activityvisualizer");
            GenerateCameraPathModel();
        }

        public double RenderOrder => 1;

        public int RenderRange => 999;

        int vertexIndex = 0;
        Vec3d startPos;
        int currentPoint = 0;
        float accum = 0;

        void InitModel()
        {
            pathModel = new MeshData(4, 4, false, false, true, true);
            pathModel.SetMode(EnumDrawMode.LineStrip);
            pathModelRef = null;
            origin = capi.World.Player.Entity.Pos.XYZ;
        }
        void GenerateCameraPathModel()
        {
            InitModel();

            startPos = capi.World.Player.Entity.Pos.XYZ;
            vertexIndex = 0;
            addPoint(startPos.X, startPos.Y, startPos.Z);

            foreach (var act in entityActivity.Actions)
            {
                act.OnVisualize(this);
            }

            pathModelRef?.Dispose();
            pathModelRef = capi.Render.UploadMesh(pathModel);
        }

        private void addPoint(double x, double y, double z)
        {
            pathModel.AddVertexSkipTex(
                (float)(x - origin.X),
                (float)(y - origin.Y),
                (float)(z - origin.Z),
                currentPoint % 2 == 0 ? ColorUtil.WhiteArgb : ColorUtil.ToRgba(255, 255, 50, 50)
            );
            pathModel.AddIndex(vertexIndex++);
        }

        internal void GoTo(Vec3d target)
        {
            currentPoint++;
            addPoint(target.X, target.Y, target.Z);
            startPos.Set(target);
        }

        internal void LineTo(Vec3d target)
        {
            currentPoint++;
            addPoint(target.X, target.Y, target.Z);
            addPoint(startPos.X, startPos.Y, startPos.Z);
        }


        public void OnRenderFrame(float dt, EnumRenderStage stage)
        {
            accum += dt;
            if (accum > 1)
            {
                GenerateCameraPathModel();
                accum = 0;
            }

            IShaderProgram prog = capi.Render.GetEngineShader(EnumShaderProgram.Autocamera);

            prog.Use();
            capi.Render.LineWidth = 2;
            capi.Render.BindTexture2d(0);

            capi.Render.GlPushMatrix();
            capi.Render.GlLoadMatrix(capi.Render.CameraMatrixOrigin);


            Vec3d cameraPos = capi.World.Player.Entity.CameraPos;
            capi.Render.GlTranslate(
                (float)(origin.X - cameraPos.X),
                (float)(origin.Y - cameraPos.Y),
                (float)(origin.Z - cameraPos.Z)
            );
            prog.UniformMatrix("projectionMatrix", capi.Render.CurrentProjectionMatrix);
            prog.UniformMatrix("modelViewMatrix", capi.Render.CurrentModelviewMatrix);

            capi.Render.RenderMesh(pathModelRef);

            capi.Render.GlPopMatrix();

            prog.Stop();
        }

        public void Dispose()
        {
            pathModelRef?.Dispose();
            capi.Event.UnregisterRenderer(this, EnumRenderStage.Opaque);
        }

        
    }
}