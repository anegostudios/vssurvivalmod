using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class ChestLabelRenderer : BlockEntitySignRenderer
    {
        
        public ChestLabelRenderer(BlockPos pos, ICoreClientAPI api) : base(pos, api)
        {
            TextWidth = 200;
        }

        public void SetRotation(float radY)
        {
            this.rotY = radY;
        }


        public override void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            if (loadedTexture == null) return;

            IRenderAPI rpi = api.Render;
            Vec3d camPos = api.World.Player.Entity.CameraPos;

            if (camPos.SquareDistanceTo(pos.X, pos.Y, pos.Z) > 20 * 20) return;

            rpi.GlDisableCullFace();
            rpi.GlToggleBlend(true);

            IStandardShaderProgram prog = rpi.PreparedStandardShader(pos.X, pos.Y, pos.Z);

            prog.Tex2D = loadedTexture.TextureId;

            prog.ModelMatrix = ModelMat
                .Identity()
                .Translate(pos.X - camPos.X, pos.Y - camPos.Y, pos.Z - camPos.Z)
                .Translate(0.5f, 0.5f, 0.5f)
                .RotateY(rotY + GameMath.PI)
                .Translate(-0.5f, -0.5, -0.5f)
                .Translate(0.5f, 0.35f, 1 / 16f + 0.03f)
                .Scale(0.45f * QuadWidth, 0.45f * QuadHeight, 0.45f * QuadWidth)
                .Values
            ;

            prog.ViewMatrix = rpi.CameraMatrixOriginf;
            prog.ProjectionMatrix = rpi.CurrentProjectionMatrix;
            prog.NormalShaded = 0;

            rpi.RenderMesh(quadModelRef);
            prog.Stop();
        }
    }
}
