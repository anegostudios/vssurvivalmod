using Cairo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class WaypointMapComponent : MapComponent
    {
        public MeshRef quadModel;
        public LoadedTexture Texture;

        Vec2f viewPos = new Vec2f();
        Vec4f color = new Vec4f();
        Waypoint waypoint;

        public WaypointMapComponent(Waypoint waypoint, LoadedTexture texture, ICoreClientAPI capi) : base(capi)
        {
            this.waypoint = waypoint;
            this.Texture = texture;
            ColorUtil.ToRGBAVec4f(waypoint.Color, ref color);

            quadModel = capi.Render.UploadMesh(QuadMeshUtil.GetQuad());
        }

        public override void Render(GuiElementMap map, float dt)
        {
            map.TranslateWorldPosToViewPos(waypoint.Position, ref viewPos);

            float x = (float)(map.Bounds.renderX + viewPos.X);
            float y = (float)(map.Bounds.renderY + viewPos.Y);

            ICoreClientAPI api = map.Api;

            IShaderProgram prog = api.Render.GetEngineShader(EnumShaderProgram.Gui);
            prog.Uniform("rgbaIn", color);
            prog.Uniform("extraGlow", 0);
            prog.Uniform("applyColor", 0);
            prog.Uniform("noTexture", 0f);
            prog.BindTexture2D("tex2d", Texture.TextureId, 0);

            api.Render.GlPushMatrix();
            api.Render.GlTranslate(x, y, 60);
            api.Render.GlScale(Texture.Width, Texture.Height, 0);
            api.Render.GlScale(0.5f, 0.5f, 0);

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
            mapElem.TranslateWorldPosToViewPos(waypoint.Position, ref viewPos);

            double mouseX = args.X - mapElem.Bounds.renderX;
            double mouseY = args.Y - mapElem.Bounds.renderY;
            
            if (Math.Abs(viewPos.X - mouseX) < 5 && Math.Abs(viewPos.Y - mouseY) < 5)
            {
                hoverText.Append("Waypoint '" + waypoint.Title + "'");
            }
        }
    }

}
