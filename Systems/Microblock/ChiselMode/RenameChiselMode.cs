using Cairo;
using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

#nullable disable

namespace VSSurvivalMod.Systems.ChiselModes
{
    public class RenameChiselMode : ChiselMode
    {
        public override DrawSkillIconDelegate DrawAction(ICoreClientAPI capi) => Drawedit_svg;

        public override bool Apply(BlockEntityChisel chiselEntity, IPlayer byPlayer, Vec3i voxelPos, BlockFacing facing, bool isBreak, byte currentMaterialIndex)
        {
            IClientWorldAccessor clientWorld = (IClientWorldAccessor)chiselEntity.Api.World;

            string prevName = chiselEntity.BlockName;
            GuiDialogBlockEntityTextInput dlg = new GuiDialogBlockEntityTextInput(
                Lang.Get("Block name"),
                chiselEntity.Pos,
                chiselEntity.BlockName,
                chiselEntity.Api as ICoreClientAPI,
                new TextAreaConfig() { MaxWidth = 500 }
            );
            dlg.OnTextChanged = (text) => chiselEntity.BlockName = text;
            dlg.OnCloseCancel = () => chiselEntity.BlockName = prevName;
            dlg.TryOpen();
            
            return false;
        }

        public void Drawedit_svg(Context cr, int x, int y, float width, float height, double[] rgba)
        {
            Pattern pattern = null;
            Matrix matrix = cr.Matrix;

            cr.Save();
            float w = 382;
            float h = 200;
            float scale = Math.Min(width / w, height / h);
            matrix.Translate(x + Math.Max(0, (width - w * scale) / 2), y + Math.Max(0, (height - h * scale) / 2));
            matrix.Scale(scale, scale);
            cr.Matrix = matrix;

            cr.Operator = Operator.Over;
            cr.LineWidth = 9;
            cr.MiterLimit = 4;
            cr.LineCap = LineCap.Butt;
            cr.LineJoin = LineJoin.Miter;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(10.628906, 10.628906);
            cr.LineTo(371.445313, 10.628906);
            cr.LineTo(371.445313, 189.617188);
            cr.LineTo(10.628906, 189.617188);
            cr.ClosePath();
            cr.MoveTo(10.628906, 10.628906);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            matrix = new Matrix(3.543307, 0, 0, 3.543307, -219.495455, -129.753943);
            pattern.Matrix = matrix;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            cr.LineWidth = 9;
            cr.MiterLimit = 4;
            cr.LineCap = LineCap.Butt;
            cr.LineJoin = LineJoin.Miter;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(75.972656, 47.5625);
            cr.LineTo(75.972656, 150.789063);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            matrix = new Matrix(3.543307, 0, 0, 3.543307, -219.495455, -129.753943);
            pattern.Matrix = matrix;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            cr.LineWidth = 9;
            cr.MiterLimit = 4;
            cr.LineCap = LineCap.Butt;
            cr.LineJoin = LineJoin.Miter;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(52.308594, 49.4375);
            cr.LineTo(98.714844, 49.4375);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            matrix = new Matrix(3.543307, 0, 0, 3.543307, -219.495455, -129.753943);
            pattern.Matrix = matrix;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            cr.LineWidth = 9;
            cr.MiterLimit = 4;
            cr.LineCap = LineCap.Butt;
            cr.LineJoin = LineJoin.Miter;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(53.265625, 151.5);
            cr.LineTo(99.667969, 151.5);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            matrix = new Matrix(3.543307, 0, 0, 3.543307, -219.495455, -129.753943);
            pattern.Matrix = matrix;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Restore();
        }
    }
}
