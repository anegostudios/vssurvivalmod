using System;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent
{
    public class OwnedEntityMapComponent : MapComponent
    {
        private EntityOwnership entity;
        internal MeshRef quadModel;
        public LoadedTexture Texture;

        Vec2f viewPos = new Vec2f();
        Matrixf mvMat = new Matrixf();

        int color;

        public OwnedEntityMapComponent(ICoreClientAPI capi, LoadedTexture texture, EntityOwnership entity, string color = null) : base(capi)
        {
            quadModel = capi.Render.UploadMesh(QuadMeshUtil.GetQuad());
            this.Texture = texture;
            this.entity = entity;
            this.color = color == null ? 0 : (ColorUtil.Hex2Int(color) | 255 << 24);
        }

        public override void Render(GuiElementMap map, float dt)
        {
            bool pinned = true;

            var nowpos = capi.World.GetEntityById(entity.EntityId)?.Pos;
            var pos = nowpos ?? entity.Pos;

            if (pos.DistanceTo(capi.World.Player.Entity.Pos.XYZ) < 2) return;

            map.TranslateWorldPosToViewPos(pos.XYZ, ref viewPos);
            if (pinned)
            {
                map.Api.Render.PushScissor(null);
                map.ClampButPreserveAngle(ref viewPos, 2);
            }
            else
            {
                if (viewPos.X < -10 || viewPos.Y < -10 || viewPos.X > map.Bounds.OuterWidth + 10 || viewPos.Y > map.Bounds.OuterHeight + 10) return;
            }

            float x = (float)(map.Bounds.renderX + viewPos.X);
            float y = (float)(map.Bounds.renderY + viewPos.Y);

            ICoreClientAPI api = map.Api;

            if (Texture.Disposed) throw new Exception("Fatal. Trying to render a disposed texture");
            if (quadModel.Disposed) throw new Exception("Fatal. Trying to render a disposed texture");

            capi.Render.GlToggleBlend(true);

            IShaderProgram prog = api.Render.GetEngineShader(EnumShaderProgram.Gui);
            if (color == 0)
            {
                prog.Uniform("rgbaIn", ColorUtil.WhiteArgbVec);
            }
            else
            {
                Vec4f vec = new Vec4f();
                ColorUtil.ToRGBAVec4f(color, ref vec);
                prog.Uniform("rgbaIn", vec);
            }

            prog.Uniform("applyColor", 0);
            prog.Uniform("extraGlow", 0);
            prog.Uniform("noTexture", 0f);
            prog.BindTexture2D("tex2d", Texture.TextureId, 0);

            mvMat
                .Set(api.Render.CurrentModelviewMatrix)
                .Translate(x, y, 60)
                .Scale(Texture.Width, Texture.Height, 0)
                .Scale(0.5f, 0.5f, 0)
                .RotateZ(-pos.Yaw + 180 * GameMath.DEG2RAD)
            ;

            prog.UniformMatrix("projectionMatrix", api.Render.CurrentProjectionMatrix);
            prog.UniformMatrix("modelViewMatrix", mvMat.Values);

            api.Render.RenderMesh(quadModel);

            if (pinned)
            {
                map.Api.Render.PopScissor();
            }

        }

        public override void Dispose()
        {
            base.Dispose();
            quadModel.Dispose();
        }


        public override void OnMouseMove(MouseEvent args, GuiElementMap mapElem, StringBuilder hoverText)
        {
            var nowpos = capi.World.GetEntityById(entity.EntityId)?.Pos?.XYZ;
            var pos = nowpos ?? entity.Pos.XYZ;

            Vec2f viewPos = new Vec2f();
            mapElem.TranslateWorldPosToViewPos(pos, ref viewPos);

            double mouseX = args.X - mapElem.Bounds.renderX;
            double mouseY = args.Y - mapElem.Bounds.renderY;
            double sc = GuiElement.scaled(5);
            
            if (Math.Abs(viewPos.X - mouseX) < sc && Math.Abs(viewPos.Y - mouseY) < sc)
            {
                hoverText.AppendLine(entity.Name);
                hoverText.AppendLine(Lang.Get("ownableentity-mapmarker-ownedbyyou"));
            }
        }
    }
}
