using System;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class OreMapComponent : MapComponent
    {
        Vec2f viewPos = new Vec2f();
        Vec4f color = new Vec4f();
        PropickReading reading;
        int waypointIndex;
        Matrixf mvMat = new Matrixf();
        OreMapLayer oreLayer;
        bool mouseOver;

        public static float IconScale = 0.85f;
        public string filterByOreCode;

        public OreMapComponent(int waypointIndex, PropickReading reading, OreMapLayer wpLayer, ICoreClientAPI capi, string filterByOreCode) : base(capi)
        {
            this.waypointIndex = waypointIndex;
            this.reading = reading;
            this.oreLayer = wpLayer;

            int col = GuiStyle.DamageColorGradient[(int)Math.Min(99, reading.HighestReading * 150)];
            if (filterByOreCode != null) col = GuiStyle.DamageColorGradient[(int)Math.Min(99, reading.OreReadings[filterByOreCode].TotalFactor * 150)];

            this.color = new Vec4f();
            ColorUtil.ToRGBAVec4f(col, ref color);
            color.W = 1;
        }

        public override void Render(GuiElementMap map, float dt)
        {
            map.TranslateWorldPosToViewPos(reading.Position, ref viewPos);
            if (viewPos.X < -10 || viewPos.Y < -10 || viewPos.X > map.Bounds.OuterWidth + 10 || viewPos.Y > map.Bounds.OuterHeight + 10) return;

            float x = (float)(map.Bounds.renderX + viewPos.X);
            float y = (float)(map.Bounds.renderY + viewPos.Y);

            ICoreClientAPI api = map.Api;

            IShaderProgram prog = api.Render.GetEngineShader(EnumShaderProgram.Gui);
            prog.Uniform("rgbaIn", color);
            prog.Uniform("extraGlow", 0);
            prog.Uniform("applyColor", 0);
            prog.Uniform("noTexture", 0f);

            LoadedTexture tex = oreLayer.oremapTexture;
            float hover = (mouseOver ? 6 : 0) - 1.5f * Math.Max(1, 1 / map.ZoomLevel);
            
            if (tex != null)
            {
                prog.BindTexture2D("tex2d", tex.TextureId, 0);
                prog.UniformMatrix("projectionMatrix", api.Render.CurrentProjectionMatrix);
                mvMat
                    .Set(api.Render.CurrentModelviewMatrix)
                    .Translate(x, y, 60)
                    .Scale(tex.Width + hover, tex.Height + hover, 0)
                    .Scale(0.5f * IconScale, 0.5f * IconScale, 0)
                ;

                // Shadow
                var shadowMvMat = mvMat.Clone().Scale(1.25f, 1.25f, 1.25f);
                prog.Uniform("rgbaIn", new Vec4f(0, 0, 0, 0.7f));
                prog.UniformMatrix("modelViewMatrix", shadowMvMat.Values);
                api.Render.RenderMesh(oreLayer.quadModel);

                // Icon
                prog.Uniform("rgbaIn", color);
                prog.UniformMatrix("modelViewMatrix", mvMat.Values);
                api.Render.RenderMesh(oreLayer.quadModel);
            }
        }

        public override void Dispose()
        {
            base.Dispose();
            // Texture is disposed by WaypointMapLayer
        }



        public override void OnMouseMove(MouseEvent args, GuiElementMap mapElem, StringBuilder hoverText)
        {
            Vec2f viewPos = new Vec2f();
            mapElem.TranslateWorldPosToViewPos(reading.Position, ref viewPos);
            
            double x = viewPos.X + mapElem.Bounds.renderX;
            double y = viewPos.Y + mapElem.Bounds.renderY;
            double dX = args.X - x;
            double dY = args.Y - y;

            var size = RuntimeEnv.GUIScale * 8;
            if (mouseOver = Math.Abs(dX) < size && Math.Abs(dY) < size)
            {
                var pageCodes = capi.ModLoader.GetModSystem<ModSystemOreMap>().prospectingMetaData.PageCodes;
                var text = reading.ToHumanReadable(capi.Settings.String["language"], pageCodes);
                hoverText.AppendLine(text);
            }
        }

        
        public override void OnMouseUpOnElement(MouseEvent args, GuiElementMap mapElem)
        {
            if (args.Button == EnumMouseButton.Right)
            {
                Vec2f viewPos = new Vec2f();
                mapElem.TranslateWorldPosToViewPos(reading.Position, ref viewPos);

                double x = viewPos.X + mapElem.Bounds.renderX;
                double y = viewPos.Y + mapElem.Bounds.renderY;
                double dX = args.X - x;
                double dY = args.Y - y;

                var size = RuntimeEnv.GUIScale * 8;
                if (Math.Abs(dX) < size && Math.Abs(dY) < size)
                {
                    var dlg = new GuiDialogConfirm(capi, Lang.Get("prospecting-reading-confirmdelete"), onConfirmDone);
                    dlg.TryOpen();
                    var mapdlg = capi.ModLoader.GetModSystem<WorldMapManager>().worldMapDlg;
                    dlg.OnClosed += () => capi.Gui.RequestFocus(mapdlg);
                    args.Handled = true;
                }
            }
        }

        private void onConfirmDone(bool confirm)
        {
            if (confirm)
            {
                oreLayer.Delete(capi.World.Player, waypointIndex);
            }
        }
    }

}
