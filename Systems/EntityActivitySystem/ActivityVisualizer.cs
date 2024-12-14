using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.Essentials;

namespace Vintagestory.GameContent
{
    public class VisualizerLabel
    {
        public LoadedTexture Texture { get; set; }
        public Vec3d Pos;
    }

    public class ActivityVisualizer : IRenderer
    {
        private EntityActivity entityActivity;
        private ICoreClientAPI capi;

        MeshData pathModel;
        MeshRef pathModelRef;
        Vec3d origin;
        List<VisualizerLabel> labels = new List<VisualizerLabel>();

        public ICoreClientAPI Api => capi;

        public ActivityVisualizer(EntityActivity entityActivity)
        {
            this.entityActivity = entityActivity;
        }

        public ActivityVisualizer(ICoreClientAPI capi, EntityActivity entityActivity, Entity sourceEntity)
        {
            this.capi = capi;
            this.entityActivity = entityActivity;
            this.sourceEntity = sourceEntity;

            capi.Event.RegisterRenderer(this, EnumRenderStage.Opaque, "activityvisualizer");
            capi.Event.RegisterRenderer(this, EnumRenderStage.Ortho, "activityvisualizer2d");
            GenerateCameraPathModel();
        }

        public double RenderOrder => 1;

        public int RenderRange => 999;

        int vertexIndex = 0;
        Entity sourceEntity;
        Vec3d curPos;
        int lineIndex = 0;
        float accum = 0;
        public Vec3d CurrentPos => curPos;

        void InitModel()
        {
            lineIndex = 0;
            vertexIndex = 0;
            pathModel = new MeshData(4, 4, false, false, true, true);
            pathModel.SetMode(EnumDrawMode.LineStrip);
            pathModelRef?.Dispose();
            pathModelRef = null;
            origin = capi.World.Player.Entity.Pos.XYZ;
            foreach (var val in labels)
            {
                val.Texture?.Dispose();
            }
            labels = new List<VisualizerLabel>();
        }
        void GenerateCameraPathModel()
        {
            InitModel();
            curPos = null;
            
            foreach (var act in entityActivity.Actions)
            {
                act.OnVisualize(this);
            }

            pathModelRef?.Dispose();
            pathModelRef = capi.Render.UploadMesh(pathModel);
        }

        private void addPoint(double x, double y, double z, int color)
        {
            pathModel.AddVertexSkipTex(
                (float)(x - origin.X),
                (float)(y - origin.Y + 0.1),
                (float)(z - origin.Z),
                color
            );
            pathModel.AddIndex(vertexIndex++);
        }

        public void GoTo(Vec3d target, int color = -1)
        {
            if (curPos == null)
            {
                curPos = target.Clone();
                return;
            }
            LineTo(curPos, target, color);
            curPos.Set(target);
        }

        public void LineTo(Vec3d source, Vec3d target, int color=-1)
        {
            if (color == -1)
            {
                color = lineIndex % 2 == 0 ? ColorUtil.WhiteArgb : ColorUtil.ToRgba(255, 255, 50, 50);
            }

            addPoint(curPos.X, curPos.Y, curPos.Z, color);
            addPoint(target.X, target.Y, target.Z, color);

            var bg = new TextBackground() { FillColor = GuiStyle.DialogLightBgColor, Padding = 3, Radius = GuiStyle.ElementBGRadius };
            labels.Add(new VisualizerLabel()
            {
                Pos = (curPos + target) / 2,
                Texture = Api.Gui.TextTexture.GenTextTexture(""+lineIndex, CairoFont.WhiteMediumText(), bg)
            });

            lineIndex++;
        }


        public void OnRenderFrame(float dt, EnumRenderStage stage)
        {
            if (stage == EnumRenderStage.Ortho)
            {
                Render2d(dt);
                return;
            }


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


        private void Render2d(float deltaTime)
        {
            var rapi = capi.Render;

            
            foreach (var label in labels)
            {
                Vec3d pos = MatrixToolsd.Project(label.Pos, rapi.PerspectiveProjectionMat, rapi.PerspectiveViewMat, rapi.FrameWidth, rapi.FrameHeight);
                pos.X -= label.Texture.Width / 2;
                pos.Y += label.Texture.Height*1.5;

                rapi.Render2DTexture(label.Texture.TextureId, (float)pos.X, rapi.FrameHeight - (float)pos.Y, label.Texture.Width, label.Texture.Height, 20);
            }
        }


        public void Dispose()
        {
            pathModelRef?.Dispose();
            capi.Event.UnregisterRenderer(this, EnumRenderStage.Opaque);
            foreach (var val in labels)
            {
                val.Texture?.Dispose();
            }
            labels.Clear();
        }

        
    }
}