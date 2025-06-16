using Cairo;
using System;
using Vintagestory.API.Client;

#nullable disable

namespace VSSurvivalMod.Systems.ChiselModes
{
    public class FourByChiselMode : ChiselMode
    {
        public override int ChiselSize => 4;

        public override DrawSkillIconDelegate DrawAction(ICoreClientAPI capi) => Drawcreate16_svg;

        public void Drawcreate16_svg(Context cr, int x, int y, float width, float height, double[] rgba)
        {
            Pattern pattern = null;
            Matrix matrix = cr.Matrix;

            cr.Save();
            float w = 146;
            float h = 146;
            float scale = Math.Min(width / w, height / h);
            matrix.Translate(x + Math.Max(0, (width - w * scale) / 2), y + Math.Max(0, (height - h * scale) / 2));
            matrix.Scale(scale, scale);
            cr.Matrix = matrix;

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(4.25, 4);
            cr.LineTo(29.25, 4);
            cr.LineTo(29.25, 29);
            cr.LineTo(4.25, 29);
            cr.ClosePath();
            cr.MoveTo(4.25, 4);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            cr.FillRule = FillRule.Winding;
            cr.FillPreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            cr.LineWidth = 8;
            cr.MiterLimit = 10;
            cr.LineCap = LineCap.Butt;
            cr.LineJoin = LineJoin.Miter;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(4.25, 4);
            cr.LineTo(29.25, 4);
            cr.LineTo(29.25, 29);
            cr.LineTo(4.25, 29);
            cr.ClosePath();
            cr.MoveTo(4.25, 4);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            matrix = new Matrix(1, 0, 0, 1, 240.15, -333.7);
            pattern.Matrix = matrix;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(41.75, 4);
            cr.LineTo(66.75, 4);
            cr.LineTo(66.75, 29);
            cr.LineTo(41.75, 29);
            cr.ClosePath();
            cr.MoveTo(41.75, 4);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            cr.FillRule = FillRule.Winding;
            cr.FillPreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            cr.LineWidth = 8;
            cr.MiterLimit = 10;
            cr.LineCap = LineCap.Butt;
            cr.LineJoin = LineJoin.Miter;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(41.75, 4);
            cr.LineTo(66.75, 4);
            cr.LineTo(66.75, 29);
            cr.LineTo(41.75, 29);
            cr.ClosePath();
            cr.MoveTo(41.75, 4);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            matrix = new Matrix(1, 0, 0, 1, 240.15, -333.7);
            pattern.Matrix = matrix;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(4.25, 42);
            cr.LineTo(29.25, 42);
            cr.LineTo(29.25, 67);
            cr.LineTo(4.25, 67);
            cr.ClosePath();
            cr.MoveTo(4.25, 42);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            cr.FillRule = FillRule.Winding;
            cr.FillPreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            cr.LineWidth = 8;
            cr.MiterLimit = 10;
            cr.LineCap = LineCap.Butt;
            cr.LineJoin = LineJoin.Miter;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(4.25, 42);
            cr.LineTo(29.25, 42);
            cr.LineTo(29.25, 67);
            cr.LineTo(4.25, 67);
            cr.ClosePath();
            cr.MoveTo(4.25, 42);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            matrix = new Matrix(1, 0, 0, 1, 240.15, -333.7);
            pattern.Matrix = matrix;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(41.75, 42);
            cr.LineTo(66.75, 42);
            cr.LineTo(66.75, 67);
            cr.LineTo(41.75, 67);
            cr.ClosePath();
            cr.MoveTo(41.75, 42);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            cr.FillRule = FillRule.Winding;
            cr.FillPreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            cr.LineWidth = 8;
            cr.MiterLimit = 10;
            cr.LineCap = LineCap.Butt;
            cr.LineJoin = LineJoin.Miter;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(41.75, 42);
            cr.LineTo(66.75, 42);
            cr.LineTo(66.75, 67);
            cr.LineTo(41.75, 67);
            cr.ClosePath();
            cr.MoveTo(41.75, 42);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            matrix = new Matrix(1, 0, 0, 1, 240.15, -333.7);
            pattern.Matrix = matrix;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(79.351563, 4);
            cr.LineTo(104.351563, 4);
            cr.LineTo(104.351563, 29);
            cr.LineTo(79.351563, 29);
            cr.ClosePath();
            cr.MoveTo(79.351563, 4);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            cr.FillRule = FillRule.Winding;
            cr.FillPreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            cr.LineWidth = 8;
            cr.MiterLimit = 10;
            cr.LineCap = LineCap.Butt;
            cr.LineJoin = LineJoin.Miter;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(79.351563, 4);
            cr.LineTo(104.351563, 4);
            cr.LineTo(104.351563, 29);
            cr.LineTo(79.351563, 29);
            cr.ClosePath();
            cr.MoveTo(79.351563, 4);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            matrix = new Matrix(1, 0, 0, 1, 240.15, -333.7);
            pattern.Matrix = matrix;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(116.851563, 4);
            cr.LineTo(141.851563, 4);
            cr.LineTo(141.851563, 29);
            cr.LineTo(116.851563, 29);
            cr.ClosePath();
            cr.MoveTo(116.851563, 4);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            cr.FillRule = FillRule.Winding;
            cr.FillPreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            cr.LineWidth = 8;
            cr.MiterLimit = 10;
            cr.LineCap = LineCap.Butt;
            cr.LineJoin = LineJoin.Miter;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(116.851563, 4);
            cr.LineTo(141.851563, 4);
            cr.LineTo(141.851563, 29);
            cr.LineTo(116.851563, 29);
            cr.ClosePath();
            cr.MoveTo(116.851563, 4);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            matrix = new Matrix(1, 0, 0, 1, 240.15, -333.7);
            pattern.Matrix = matrix;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(79.351563, 42);
            cr.LineTo(104.351563, 42);
            cr.LineTo(104.351563, 67);
            cr.LineTo(79.351563, 67);
            cr.ClosePath();
            cr.MoveTo(79.351563, 42);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            cr.FillRule = FillRule.Winding;
            cr.FillPreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            cr.LineWidth = 8;
            cr.MiterLimit = 10;
            cr.LineCap = LineCap.Butt;
            cr.LineJoin = LineJoin.Miter;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(79.351563, 42);
            cr.LineTo(104.351563, 42);
            cr.LineTo(104.351563, 67);
            cr.LineTo(79.351563, 67);
            cr.ClosePath();
            cr.MoveTo(79.351563, 42);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            matrix = new Matrix(1, 0, 0, 1, 240.15, -333.7);
            pattern.Matrix = matrix;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(116.851563, 42);
            cr.LineTo(141.851563, 42);
            cr.LineTo(141.851563, 67);
            cr.LineTo(116.851563, 67);
            cr.ClosePath();
            cr.MoveTo(116.851563, 42);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            cr.FillRule = FillRule.Winding;
            cr.FillPreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            cr.LineWidth = 8;
            cr.MiterLimit = 10;
            cr.LineCap = LineCap.Butt;
            cr.LineJoin = LineJoin.Miter;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(116.851563, 42);
            cr.LineTo(141.851563, 42);
            cr.LineTo(141.851563, 67);
            cr.LineTo(116.851563, 67);
            cr.ClosePath();
            cr.MoveTo(116.851563, 42);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            matrix = new Matrix(1, 0, 0, 1, 240.15, -333.7);
            pattern.Matrix = matrix;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(4.148438, 79);
            cr.LineTo(29.148438, 79);
            cr.LineTo(29.148438, 104);
            cr.LineTo(4.148438, 104);
            cr.ClosePath();
            cr.MoveTo(4.148438, 79);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            cr.FillRule = FillRule.Winding;
            cr.FillPreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            cr.LineWidth = 8;
            cr.MiterLimit = 10;
            cr.LineCap = LineCap.Butt;
            cr.LineJoin = LineJoin.Miter;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(4.148438, 79);
            cr.LineTo(29.148438, 79);
            cr.LineTo(29.148438, 104);
            cr.LineTo(4.148438, 104);
            cr.ClosePath();
            cr.MoveTo(4.148438, 79);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            matrix = new Matrix(1, 0, 0, 1, 240.15, -333.7);
            pattern.Matrix = matrix;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(41.648438, 79);
            cr.LineTo(66.648438, 79);
            cr.LineTo(66.648438, 104);
            cr.LineTo(41.648438, 104);
            cr.ClosePath();
            cr.MoveTo(41.648438, 79);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            cr.FillRule = FillRule.Winding;
            cr.FillPreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            cr.LineWidth = 8;
            cr.MiterLimit = 10;
            cr.LineCap = LineCap.Butt;
            cr.LineJoin = LineJoin.Miter;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(41.648438, 79);
            cr.LineTo(66.648438, 79);
            cr.LineTo(66.648438, 104);
            cr.LineTo(41.648438, 104);
            cr.ClosePath();
            cr.MoveTo(41.648438, 79);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            matrix = new Matrix(1, 0, 0, 1, 240.15, -333.7);
            pattern.Matrix = matrix;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(4.148438, 117);
            cr.LineTo(29.148438, 117);
            cr.LineTo(29.148438, 142);
            cr.LineTo(4.148438, 142);
            cr.ClosePath();
            cr.MoveTo(4.148438, 117);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            cr.FillRule = FillRule.Winding;
            cr.FillPreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            cr.LineWidth = 8;
            cr.MiterLimit = 10;
            cr.LineCap = LineCap.Butt;
            cr.LineJoin = LineJoin.Miter;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(4.148438, 117);
            cr.LineTo(29.148438, 117);
            cr.LineTo(29.148438, 142);
            cr.LineTo(4.148438, 142);
            cr.ClosePath();
            cr.MoveTo(4.148438, 117);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            matrix = new Matrix(1, 0, 0, 1, 240.15, -333.7);
            pattern.Matrix = matrix;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(41.648438, 117);
            cr.LineTo(66.648438, 117);
            cr.LineTo(66.648438, 142);
            cr.LineTo(41.648438, 142);
            cr.ClosePath();
            cr.MoveTo(41.648438, 117);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            cr.FillRule = FillRule.Winding;
            cr.FillPreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            cr.LineWidth = 8;
            cr.MiterLimit = 10;
            cr.LineCap = LineCap.Butt;
            cr.LineJoin = LineJoin.Miter;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(41.648438, 117);
            cr.LineTo(66.648438, 117);
            cr.LineTo(66.648438, 142);
            cr.LineTo(41.648438, 142);
            cr.ClosePath();
            cr.MoveTo(41.648438, 117);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            matrix = new Matrix(1, 0, 0, 1, 240.15, -333.7);
            pattern.Matrix = matrix;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(79.25, 79);
            cr.LineTo(104.25, 79);
            cr.LineTo(104.25, 104);
            cr.LineTo(79.25, 104);
            cr.ClosePath();
            cr.MoveTo(79.25, 79);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            cr.FillRule = FillRule.Winding;
            cr.FillPreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            cr.LineWidth = 8;
            cr.MiterLimit = 10;
            cr.LineCap = LineCap.Butt;
            cr.LineJoin = LineJoin.Miter;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(79.25, 79);
            cr.LineTo(104.25, 79);
            cr.LineTo(104.25, 104);
            cr.LineTo(79.25, 104);
            cr.ClosePath();
            cr.MoveTo(79.25, 79);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            matrix = new Matrix(1, 0, 0, 1, 240.15, -333.7);
            pattern.Matrix = matrix;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(116.75, 79);
            cr.LineTo(141.75, 79);
            cr.LineTo(141.75, 104);
            cr.LineTo(116.75, 104);
            cr.ClosePath();
            cr.MoveTo(116.75, 79);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            cr.FillRule = FillRule.Winding;
            cr.FillPreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            cr.LineWidth = 8;
            cr.MiterLimit = 10;
            cr.LineCap = LineCap.Butt;
            cr.LineJoin = LineJoin.Miter;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(116.75, 79);
            cr.LineTo(141.75, 79);
            cr.LineTo(141.75, 104);
            cr.LineTo(116.75, 104);
            cr.ClosePath();
            cr.MoveTo(116.75, 79);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            matrix = new Matrix(1, 0, 0, 1, 240.15, -333.7);
            pattern.Matrix = matrix;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(79.25, 117);
            cr.LineTo(104.25, 117);
            cr.LineTo(104.25, 142);
            cr.LineTo(79.25, 142);
            cr.ClosePath();
            cr.MoveTo(79.25, 117);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            cr.FillRule = FillRule.Winding;
            cr.FillPreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            cr.LineWidth = 8;
            cr.MiterLimit = 10;
            cr.LineCap = LineCap.Butt;
            cr.LineJoin = LineJoin.Miter;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(79.25, 117);
            cr.LineTo(104.25, 117);
            cr.LineTo(104.25, 142);
            cr.LineTo(79.25, 142);
            cr.ClosePath();
            cr.MoveTo(79.25, 117);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            matrix = new Matrix(1, 0, 0, 1, 240.15, -333.7);
            pattern.Matrix = matrix;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(116.75, 117);
            cr.LineTo(141.75, 117);
            cr.LineTo(141.75, 142);
            cr.LineTo(116.75, 142);
            cr.ClosePath();
            cr.MoveTo(116.75, 117);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            cr.FillRule = FillRule.Winding;
            cr.FillPreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            cr.LineWidth = 8;
            cr.MiterLimit = 10;
            cr.LineCap = LineCap.Butt;
            cr.LineJoin = LineJoin.Miter;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(116.75, 117);
            cr.LineTo(141.75, 117);
            cr.LineTo(141.75, 142);
            cr.LineTo(116.75, 142);
            cr.ClosePath();
            cr.MoveTo(116.75, 117);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            matrix = new Matrix(1, 0, 0, 1, 240.15, -333.7);
            pattern.Matrix = matrix;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Restore();
        }
    }
}
