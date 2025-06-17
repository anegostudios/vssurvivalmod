using Vintagestory.API.Client;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent
{
    public class AnvilPartRenderer : IRenderer
    {
        ICoreClientAPI capi;
        BlockEntityAnvilPart beAnvil;
        public Matrixf ModelMat = new Matrixf();

        public AnvilPartRenderer(ICoreClientAPI capi, BlockEntityAnvilPart beAnvil)
        {
            this.capi = capi;
            this.beAnvil = beAnvil;

            capi.Event.RegisterRenderer(this, EnumRenderStage.Opaque);
        }

        public double RenderOrder => 0.5;

        public int RenderRange => 25;

        
        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            if (beAnvil.Inventory[0].Empty) return;

            IRenderAPI rpi = capi.Render;
            Vec3d camPos = capi.World.Player.Entity.CameraPos;

            var stack = beAnvil.Inventory[0].Itemstack;

            int temp = (int)stack.Collectible.GetTemperature(capi.World, stack);
            Vec4f lightrgbs = capi.World.BlockAccessor.GetLightRGBs(beAnvil.Pos.X, beAnvil.Pos.Y, beAnvil.Pos.Z);
            float[] glowColor = ColorUtil.GetIncandescenceColorAsColor4f(temp);
            int extraGlow = GameMath.Clamp((temp - 550) / 2, 0, 255);


            rpi.GlDisableCullFace();
            rpi.GlToggleBlend(true);

            IStandardShaderProgram prog = rpi.PreparedStandardShader(beAnvil.Pos.X, beAnvil.Pos.Y, beAnvil.Pos.Z);


            prog.ModelMatrix = ModelMat
                .Identity()
                .Translate(beAnvil.Pos.X - camPos.X, beAnvil.Pos.Y - camPos.Y, beAnvil.Pos.Z - camPos.Z)
                .Values
            ;

            prog.RgbaLightIn = lightrgbs;
            prog.RgbaGlowIn = new Vec4f(glowColor[0], glowColor[1], glowColor[2], extraGlow / 255f);
            prog.ExtraGlow = extraGlow;
            prog.ViewMatrix = rpi.CameraMatrixOriginf;
            prog.ProjectionMatrix = rpi.CurrentProjectionMatrix;
            prog.AverageColor = ColorUtil.ToRGBAVec4f(capi.BlockTextureAtlas.GetAverageColor((stack.Item?.FirstTexture ?? stack.Block.FirstTextureInventory).Baked.TextureSubId));
            prog.TempGlowMode = stack.ItemAttributes?["tempGlowMode"].AsInt() ?? 0;

            if (beAnvil.BaseMeshRef != null && !beAnvil.BaseMeshRef.Disposed)
            {
                rpi.RenderMultiTextureMesh(beAnvil.BaseMeshRef, "tex");
            }

            if (beAnvil.FluxMeshRef != null && !beAnvil.FluxMeshRef.Disposed)
            {
                prog.ExtraGlow = 0;
                rpi.RenderMultiTextureMesh(beAnvil.FluxMeshRef, "tex");
            }

            if (beAnvil.TopMeshRef != null && !beAnvil.TopMeshRef.Disposed)
            {
                temp = (int)beAnvil.Inventory[2].Itemstack.Collectible.GetTemperature(capi.World, beAnvil.Inventory[2].Itemstack);
                lightrgbs = capi.World.BlockAccessor.GetLightRGBs(beAnvil.Pos.X, beAnvil.Pos.Y, beAnvil.Pos.Z);
                glowColor = ColorUtil.GetIncandescenceColorAsColor4f(temp);
                extraGlow = GameMath.Clamp((temp - 550) / 2, 0, 255);

                prog.ModelMatrix = ModelMat
                    .Identity()
                    .Translate(beAnvil.Pos.X - camPos.X, beAnvil.Pos.Y - camPos.Y - beAnvil.hammerHits / 250f, beAnvil.Pos.Z - camPos.Z)
                    .Values
                ;

                prog.RgbaLightIn = lightrgbs;
                prog.RgbaGlowIn = new Vec4f(glowColor[0], glowColor[1], glowColor[2], extraGlow / 255f);
                prog.ExtraGlow = extraGlow;


                rpi.RenderMultiTextureMesh(beAnvil.TopMeshRef, "tex");
            }

            prog.Stop();
        }


        public void Dispose()
        {
            capi.Event.UnregisterRenderer(this, EnumRenderStage.Opaque);
        }

    }
}
