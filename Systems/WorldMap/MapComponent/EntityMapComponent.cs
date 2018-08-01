using Cairo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class EntityMapComponent : MapComponent
    {
        public Entity entity;
        internal MeshRef quadModel;
        public LoadedTexture Texture;

        Vec2f viewPos = new Vec2f();        

        public EntityMapComponent(ICoreClientAPI capi, LoadedTexture texture, Entity entity) : base(capi)
        {
            quadModel = capi.Render.UploadMesh(QuadMeshUtil.GetQuad());
            this.Texture = texture;
            this.entity = entity;
        }

        public override void Render(GuiElementMap map, float dt)
        {
            map.TranslateWorldPosToViewPos(entity.Pos.XYZ, ref viewPos);

            float x = (float)(map.Bounds.renderX + viewPos.X);
            float y = (float)(map.Bounds.renderY + viewPos.Y);

            //api.Render.GlToggleBlend(true, EnumBlendMode.PremultipliedAlpha);
            ICoreClientAPI api = map.Api;

            IShaderProgram prog = api.Render.GetEngineShader(EnumShaderProgram.Gui);
            prog.Uniform("rgbaIn", ColorUtil.WhiteArgbVec);
            prog.Uniform("extraGlow", 0);
            prog.Uniform("applyColor", 0);
            prog.Uniform("noTexture", 0f);
            prog.BindTexture2D("tex2d", Texture.TextureId, 0);

            api.Render.GlPushMatrix();
            api.Render.GlTranslate(x, y, 60);
            api.Render.GlScale(Texture.Width, Texture.Height, 0);
            api.Render.GlScale(0.5f, 0.5f, 0);
            //api.Render.GlTranslate(1f, 1f, 0);
            api.Render.GlRotate(-entity.Pos.Yaw * GameMath.RAD2DEG + 90, 0, 0, 1);

            prog.UniformMatrix("projectionMatrix", api.Render.CurrentProjectionMatrix);
            prog.UniformMatrix("modelViewMatrix", api.Render.CurrentModelviewMatrix);


            api.Render.RenderMesh(quadModel);
            api.Render.GlPopMatrix();
        }

        public override void Dispose()
        {
            base.Dispose();

            quadModel.Dispose();
        }


        public override void OnMouseMove(MouseEvent args, GuiElementMap mapElem, StringBuilder hoverText)
        {
            Vec2f viewPos = new Vec2f();
            mapElem.TranslateWorldPosToViewPos(entity.Pos.XYZ, ref viewPos);

            double mouseX = args.X - mapElem.Bounds.renderX;
            double mouseY = args.Y - mapElem.Bounds.renderY;

            if (Math.Abs(viewPos.X - mouseX) < 5 && Math.Abs(viewPos.Y - mouseY) < 5)
            {
                EntityPlayer eplr = entity as EntityPlayer;
                if (eplr != null)
                {
                    hoverText.AppendLine("Player " + capi.World.PlayerByUid(eplr.PlayerUID).PlayerName);
                }
            }
        }
    }

}
