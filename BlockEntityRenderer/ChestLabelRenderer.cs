using Vintagestory.API.Client;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent
{
    public class ChestLabelRenderer : BlockEntitySignRenderer
    {
        
        public ChestLabelRenderer(BlockPos pos, ICoreClientAPI api) : base(pos, api, null)
        {
            TextWidth = 200;
        }

        public void SetRotation(float radY)
        {
            this.rotY = radY;
        }


        public override void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            if (loadedTexture == null)
            {
                if (text != null && text.Length > 0)
                {
                    loadedTexture = RenderText();
                }
                else return;
            }

            IRenderAPI rpi = api.Render;
            Vec3d camPos = api.World.Player.Entity.CameraPos;

            if (camPos.SquareDistanceTo(pos.X, pos.Y, pos.Z) > 20 * 20) return;

            rpi.GlToggleBlend(true, EnumBlendMode.PremultipliedAlpha);
            var prog = progCached;
            if (prog == null)
            {
                rpi.GlDisableCullFace();

                progCached = prog = rpi.PreparedStandardShader(pos.X, pos.InternalY, pos.Z);

                prog.ViewMatrix = rpi.CameraMatrixOriginf;
                prog.ProjectionMatrix = rpi.CurrentProjectionMatrix;
                prog.NormalShaded = 0;
                prog.ExtraGodray = 0;
                prog.SsaoAttn = 0;
                prog.AlphaTest = 0.05f;
                prog.OverlayOpacity = 0;
            }

            prog.RgbaLightIn = api.World.BlockAccessor.GetLightRGBs(pos);
            prog.AddRenderFlags = 0;
            prog.Tex2D = loadedTexture.TextureId;
            prog.ModelMatrix = ModelMat
                .Identity()
                .Translate(pos.X + 0.5f - camPos.X, pos.Y + 0.5f - camPos.Y, pos.Z + 0.5f - camPos.Z)
                .RotateY(rotY + GameMath.PI)
                .Translate(-0.5f, -0.5, -0.5f)
                .Translate(0.5f, 0.35f, 1 / 16f + 0.03f)
                .Scale(0.45f * QuadWidth, 0.45f * QuadHeight, 0.45f * QuadWidth)
                .Values
            ;

            rpi.RenderMesh(quadModelRef);
            rpi.GlToggleBlend(true, EnumBlendMode.Standard);
        }
    }
}
