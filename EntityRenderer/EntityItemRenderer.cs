using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class EntityItemRenderer : EntityRenderer
    {
        EntityItem entityitem;

        // Random y-offset to reduce z-fighting
        float yOffset;

        public EntityItemRenderer(Entity entity, ICoreClientAPI api) : base(entity, api)
        {
            entityitem = (EntityItem)entity;
            yOffset = (float)api.World.Rand.NextDouble() / 20f - 1/40f;
        }

        public override void DoRender3DOpaque(float dt, bool isShadowPass)
        {
            if (isShadowPass) return;

            IRenderAPI rapi = api.Render;
            IEntityPlayer entityPlayer = api.World.Player.Entity;


            ItemRenderInfo renderInfo = rapi.GetItemStackRenderInfo(entityitem.Itemstack, EnumItemRenderTarget.Ground);
            if (renderInfo.ModelRef == null) return;

            IStandardShaderProgram prog = rapi.StandardShader;
            prog.Use();
            prog.Tex2D = renderInfo.TextureId;
            prog.RgbaTint = ColorUtil.WhiteArgbVec;

            rapi.GlMatrixModeModelView();
            rapi.GlPushMatrix();
            rapi.GlLoadMatrix(rapi.CameraMatrixOrigin);
            
            float angle = (api.World.ElapsedMilliseconds / 15f + (entity.Entityid * 20) % 360) % 360; // Double modulo because high numbers of Entityid breaks accuracy
            float size = 0.2f * renderInfo.Transform.Scale;
            float bobbing = entity.Collided ? GameMath.Sin(angle * GameMath.DEG2RAD) / 15 : 0;

            rapi.GlTranslate(0f, 0.15f + bobbing + yOffset, 0f);

            rapi.GlTranslate(entityitem.Pos.X - entityPlayer.CameraPos.X, entityitem.Pos.Y - entityPlayer.CameraPos.Y, entityitem.Pos.Z - entityPlayer.CameraPos.Z);

            rapi.GlTranslate(renderInfo.Transform.Translation.X, renderInfo.Transform.Translation.Y + 0.5 * size, renderInfo.Transform.Translation.Z);

            rapi.GlScale(size, size, size);

            rapi.GlRotate(renderInfo.Transform.Rotation.Y + angle, 0, 1, 0);
            rapi.GlRotate(renderInfo.Transform.Rotation.Z, 0, 0, 1);
            rapi.GlRotate(renderInfo.Transform.Rotation.X, 1, 0, 0);

            rapi.GlTranslate(-0.5f, -0.5f, -0.5f);

            BlockPos pos = entityitem.Pos.AsBlockPos;
            Vec4f lightrgbs = api.World.BlockAccessor.GetLightRGBs(pos.X, pos.Y, pos.Z);
            int temp = (int)entityitem.Itemstack.Collectible.GetTemperature(api.World, entityitem.Itemstack);
            float[] glowColor = ColorUtil.getIncandescenceColorAsColor4f(temp);
            lightrgbs[0] += 2 * glowColor[0];
            lightrgbs[1] += 2 * glowColor[1];
            lightrgbs[2] += 2 * glowColor[2];

            prog.ExtraGlow = GameMath.Clamp((temp - 500) / 6, 0, 255);
            prog.RgbaAmbientIn = rapi.AmbientColor;
            prog.RgbaLightIn = lightrgbs;
            prog.RgbaBlockIn = ColorUtil.WhiteArgbVec;
            prog.RgbaFogIn = rapi.FogColor;
            prog.FogMinIn = rapi.FogMin;
            prog.FogDensityIn = rapi.FogDensity;
            prog.ProjectionMatrix = rapi.CurrentProjectionMatrix;
            prog.ModelViewMatrix = rapi.CurrentModelviewMatrix;

            if (!renderInfo.CullFaces)
            {
                rapi.GlDisableCullFace();
            }

            rapi.RenderMesh(renderInfo.ModelRef);

            if (!renderInfo.CullFaces)
            {
                rapi.GlEnableCullFace();
            }

            rapi.GlPopMatrix();
            prog.Stop();
        }


        public override void Dispose()
        {

        }

    }
}
