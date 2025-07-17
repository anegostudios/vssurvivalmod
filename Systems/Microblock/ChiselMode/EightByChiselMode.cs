using Cairo;
using System;
using Vintagestory.API.Client;

#nullable disable

namespace VSSurvivalMod.Systems.ChiselModes
{
    public class EightByChiselModeData : ChiselMode
    {
        public override int ChiselSize => 8;

        public override DrawSkillIconDelegate DrawAction(ICoreClientAPI capi) => Drawcreate64_svg;

        public void Drawcreate64_svg(Context cr, int x, int y, float width, float height, double[] rgba)
        {
            Pattern pattern = null;
            Matrix matrix = cr.Matrix;

            cr.Save();
            float w = 296;
            float h = 296;
            float scale = Math.Min(width / w, height / h);
            matrix.Translate(x + Math.Max(0, (width - w * scale) / 2), y + Math.Max(0, (height - h * scale) / 2));
            matrix.Scale(scale, scale);
            cr.Matrix = matrix;

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(4.105469, 4.007813);
            cr.LineTo(29.148438, 4.007813);
            cr.LineTo(29.148438, 29.050781);
            cr.LineTo(4.105469, 29.050781);
            cr.ClosePath();
            cr.MoveTo(4.105469, 4.007813);
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
            cr.MoveTo(4.105469, 4.007813);
            cr.LineTo(29.148438, 4.007813);
            cr.LineTo(29.148438, 29.050781);
            cr.LineTo(4.105469, 29.050781);
            cr.ClosePath();
            cr.MoveTo(4.105469, 4.007813);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            matrix = new Matrix(1.001692, 0, 0, 1.001692, 232.392555, -324.548223);
            pattern.Matrix = matrix;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(41.671875, 4.007813);
            cr.LineTo(66.710938, 4.007813);
            cr.LineTo(66.710938, 29.050781);
            cr.LineTo(41.671875, 29.050781);
            cr.ClosePath();
            cr.MoveTo(41.671875, 4.007813);
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
            cr.MoveTo(41.671875, 4.007813);
            cr.LineTo(66.710938, 4.007813);
            cr.LineTo(66.710938, 29.050781);
            cr.LineTo(41.671875, 29.050781);
            cr.ClosePath();
            cr.MoveTo(41.671875, 4.007813);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            matrix = new Matrix(1.001692, 0, 0, 1.001692, 232.392555, -324.548223);
            pattern.Matrix = matrix;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(4.105469, 42.070313);
            cr.LineTo(29.148438, 42.070313);
            cr.LineTo(29.148438, 67.113281);
            cr.LineTo(4.105469, 67.113281);
            cr.ClosePath();
            cr.MoveTo(4.105469, 42.070313);
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
            cr.MoveTo(4.105469, 42.070313);
            cr.LineTo(29.148438, 42.070313);
            cr.LineTo(29.148438, 67.113281);
            cr.LineTo(4.105469, 67.113281);
            cr.ClosePath();
            cr.MoveTo(4.105469, 42.070313);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            matrix = new Matrix(1.001692, 0, 0, 1.001692, 232.392555, -324.548223);
            pattern.Matrix = matrix;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(41.671875, 42.070313);
            cr.LineTo(66.710938, 42.070313);
            cr.LineTo(66.710938, 67.113281);
            cr.LineTo(41.671875, 67.113281);
            cr.ClosePath();
            cr.MoveTo(41.671875, 42.070313);
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
            cr.MoveTo(41.671875, 42.070313);
            cr.LineTo(66.710938, 42.070313);
            cr.LineTo(66.710938, 67.113281);
            cr.LineTo(41.671875, 67.113281);
            cr.ClosePath();
            cr.MoveTo(41.671875, 42.070313);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            matrix = new Matrix(1.001692, 0, 0, 1.001692, 232.392555, -324.548223);
            pattern.Matrix = matrix;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(79.335938, 4.007813);
            cr.LineTo(104.375, 4.007813);
            cr.LineTo(104.375, 29.050781);
            cr.LineTo(79.335938, 29.050781);
            cr.ClosePath();
            cr.MoveTo(79.335938, 4.007813);
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
            cr.MoveTo(79.335938, 4.007813);
            cr.LineTo(104.375, 4.007813);
            cr.LineTo(104.375, 29.050781);
            cr.LineTo(79.335938, 29.050781);
            cr.ClosePath();
            cr.MoveTo(79.335938, 4.007813);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            matrix = new Matrix(1.001692, 0, 0, 1.001692, 232.392555, -324.548223);
            pattern.Matrix = matrix;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(116.898438, 4.007813);
            cr.LineTo(141.941406, 4.007813);
            cr.LineTo(141.941406, 29.050781);
            cr.LineTo(116.898438, 29.050781);
            cr.ClosePath();
            cr.MoveTo(116.898438, 4.007813);
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
            cr.MoveTo(116.898438, 4.007813);
            cr.LineTo(141.941406, 4.007813);
            cr.LineTo(141.941406, 29.050781);
            cr.LineTo(116.898438, 29.050781);
            cr.ClosePath();
            cr.MoveTo(116.898438, 4.007813);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            matrix = new Matrix(1.001692, 0, 0, 1.001692, 232.392555, -324.548223);
            pattern.Matrix = matrix;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(79.335938, 42.070313);
            cr.LineTo(104.375, 42.070313);
            cr.LineTo(104.375, 67.113281);
            cr.LineTo(79.335938, 67.113281);
            cr.ClosePath();
            cr.MoveTo(79.335938, 42.070313);
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
            cr.MoveTo(79.335938, 42.070313);
            cr.LineTo(104.375, 42.070313);
            cr.LineTo(104.375, 67.113281);
            cr.LineTo(79.335938, 67.113281);
            cr.ClosePath();
            cr.MoveTo(79.335938, 42.070313);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            matrix = new Matrix(1.001692, 0, 0, 1.001692, 232.392555, -324.548223);
            pattern.Matrix = matrix;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(116.898438, 42.070313);
            cr.LineTo(141.941406, 42.070313);
            cr.LineTo(141.941406, 67.113281);
            cr.LineTo(116.898438, 67.113281);
            cr.ClosePath();
            cr.MoveTo(116.898438, 42.070313);
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
            cr.MoveTo(116.898438, 42.070313);
            cr.LineTo(141.941406, 42.070313);
            cr.LineTo(141.941406, 67.113281);
            cr.LineTo(116.898438, 67.113281);
            cr.ClosePath();
            cr.MoveTo(116.898438, 42.070313);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            matrix = new Matrix(1.001692, 0, 0, 1.001692, 232.392555, -324.548223);
            pattern.Matrix = matrix;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(4.007813, 79.132813);
            cr.LineTo(29.050781, 79.132813);
            cr.LineTo(29.050781, 104.175781);
            cr.LineTo(4.007813, 104.175781);
            cr.ClosePath();
            cr.MoveTo(4.007813, 79.132813);
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
            cr.MoveTo(4.007813, 79.132813);
            cr.LineTo(29.050781, 79.132813);
            cr.LineTo(29.050781, 104.175781);
            cr.LineTo(4.007813, 104.175781);
            cr.ClosePath();
            cr.MoveTo(4.007813, 79.132813);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            matrix = new Matrix(1.001692, 0, 0, 1.001692, 232.392555, -324.548223);
            pattern.Matrix = matrix;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(41.570313, 79.132813);
            cr.LineTo(66.613281, 79.132813);
            cr.LineTo(66.613281, 104.175781);
            cr.LineTo(41.570313, 104.175781);
            cr.ClosePath();
            cr.MoveTo(41.570313, 79.132813);
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
            cr.MoveTo(41.570313, 79.132813);
            cr.LineTo(66.613281, 79.132813);
            cr.LineTo(66.613281, 104.175781);
            cr.LineTo(41.570313, 104.175781);
            cr.ClosePath();
            cr.MoveTo(41.570313, 79.132813);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            matrix = new Matrix(1.001692, 0, 0, 1.001692, 232.392555, -324.548223);
            pattern.Matrix = matrix;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(4.007813, 117.199219);
            cr.LineTo(29.050781, 117.199219);
            cr.LineTo(29.050781, 142.242188);
            cr.LineTo(4.007813, 142.242188);
            cr.ClosePath();
            cr.MoveTo(4.007813, 117.199219);
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
            cr.MoveTo(4.007813, 117.199219);
            cr.LineTo(29.050781, 117.199219);
            cr.LineTo(29.050781, 142.242188);
            cr.LineTo(4.007813, 142.242188);
            cr.ClosePath();
            cr.MoveTo(4.007813, 117.199219);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            matrix = new Matrix(1.001692, 0, 0, 1.001692, 232.392555, -324.548223);
            pattern.Matrix = matrix;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(41.570313, 117.199219);
            cr.LineTo(66.613281, 117.199219);
            cr.LineTo(66.613281, 142.242188);
            cr.LineTo(41.570313, 142.242188);
            cr.ClosePath();
            cr.MoveTo(41.570313, 117.199219);
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
            cr.MoveTo(41.570313, 117.199219);
            cr.LineTo(66.613281, 117.199219);
            cr.LineTo(66.613281, 142.242188);
            cr.LineTo(41.570313, 142.242188);
            cr.ClosePath();
            cr.MoveTo(41.570313, 117.199219);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            matrix = new Matrix(1.001692, 0, 0, 1.001692, 232.392555, -324.548223);
            pattern.Matrix = matrix;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(79.234375, 79.132813);
            cr.LineTo(104.277344, 79.132813);
            cr.LineTo(104.277344, 104.175781);
            cr.LineTo(79.234375, 104.175781);
            cr.ClosePath();
            cr.MoveTo(79.234375, 79.132813);
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
            cr.MoveTo(79.234375, 79.132813);
            cr.LineTo(104.277344, 79.132813);
            cr.LineTo(104.277344, 104.175781);
            cr.LineTo(79.234375, 104.175781);
            cr.ClosePath();
            cr.MoveTo(79.234375, 79.132813);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            matrix = new Matrix(1.001692, 0, 0, 1.001692, 232.392555, -324.548223);
            pattern.Matrix = matrix;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(116.796875, 79.132813);
            cr.LineTo(141.839844, 79.132813);
            cr.LineTo(141.839844, 104.175781);
            cr.LineTo(116.796875, 104.175781);
            cr.ClosePath();
            cr.MoveTo(116.796875, 79.132813);
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
            cr.MoveTo(116.796875, 79.132813);
            cr.LineTo(141.839844, 79.132813);
            cr.LineTo(141.839844, 104.175781);
            cr.LineTo(116.796875, 104.175781);
            cr.ClosePath();
            cr.MoveTo(116.796875, 79.132813);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            matrix = new Matrix(1.001692, 0, 0, 1.001692, 232.392555, -324.548223);
            pattern.Matrix = matrix;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(79.234375, 117.199219);
            cr.LineTo(104.277344, 117.199219);
            cr.LineTo(104.277344, 142.242188);
            cr.LineTo(79.234375, 142.242188);
            cr.ClosePath();
            cr.MoveTo(79.234375, 117.199219);
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
            cr.MoveTo(79.234375, 117.199219);
            cr.LineTo(104.277344, 117.199219);
            cr.LineTo(104.277344, 142.242188);
            cr.LineTo(79.234375, 142.242188);
            cr.ClosePath();
            cr.MoveTo(79.234375, 117.199219);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            matrix = new Matrix(1.001692, 0, 0, 1.001692, 232.392555, -324.548223);
            pattern.Matrix = matrix;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(116.796875, 117.199219);
            cr.LineTo(141.839844, 117.199219);
            cr.LineTo(141.839844, 142.242188);
            cr.LineTo(116.796875, 142.242188);
            cr.ClosePath();
            cr.MoveTo(116.796875, 117.199219);
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
            cr.MoveTo(116.796875, 117.199219);
            cr.LineTo(141.839844, 117.199219);
            cr.LineTo(141.839844, 142.242188);
            cr.LineTo(116.796875, 142.242188);
            cr.ClosePath();
            cr.MoveTo(116.796875, 117.199219);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            matrix = new Matrix(1.001692, 0, 0, 1.001692, 232.392555, -324.548223);
            pattern.Matrix = matrix;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(154.160156, 4.007813);
            cr.LineTo(179.203125, 4.007813);
            cr.LineTo(179.203125, 29.050781);
            cr.LineTo(154.160156, 29.050781);
            cr.ClosePath();
            cr.MoveTo(154.160156, 4.007813);
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
            cr.MoveTo(154.160156, 4.007813);
            cr.LineTo(179.203125, 4.007813);
            cr.LineTo(179.203125, 29.050781);
            cr.LineTo(154.160156, 29.050781);
            cr.ClosePath();
            cr.MoveTo(154.160156, 4.007813);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            matrix = new Matrix(1.001692, 0, 0, 1.001692, 232.392555, -324.548223);
            pattern.Matrix = matrix;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(191.722656, 4.007813);
            cr.LineTo(216.765625, 4.007813);
            cr.LineTo(216.765625, 29.050781);
            cr.LineTo(191.722656, 29.050781);
            cr.ClosePath();
            cr.MoveTo(191.722656, 4.007813);
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
            cr.MoveTo(191.722656, 4.007813);
            cr.LineTo(216.765625, 4.007813);
            cr.LineTo(216.765625, 29.050781);
            cr.LineTo(191.722656, 29.050781);
            cr.ClosePath();
            cr.MoveTo(191.722656, 4.007813);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            matrix = new Matrix(1.001692, 0, 0, 1.001692, 232.392555, -324.548223);
            pattern.Matrix = matrix;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(154.160156, 42.070313);
            cr.LineTo(179.203125, 42.070313);
            cr.LineTo(179.203125, 67.113281);
            cr.LineTo(154.160156, 67.113281);
            cr.ClosePath();
            cr.MoveTo(154.160156, 42.070313);
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
            cr.MoveTo(154.160156, 42.070313);
            cr.LineTo(179.203125, 42.070313);
            cr.LineTo(179.203125, 67.113281);
            cr.LineTo(154.160156, 67.113281);
            cr.ClosePath();
            cr.MoveTo(154.160156, 42.070313);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            matrix = new Matrix(1.001692, 0, 0, 1.001692, 232.392555, -324.548223);
            pattern.Matrix = matrix;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(191.722656, 42.070313);
            cr.LineTo(216.765625, 42.070313);
            cr.LineTo(216.765625, 67.113281);
            cr.LineTo(191.722656, 67.113281);
            cr.ClosePath();
            cr.MoveTo(191.722656, 42.070313);
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
            cr.MoveTo(191.722656, 42.070313);
            cr.LineTo(216.765625, 42.070313);
            cr.LineTo(216.765625, 67.113281);
            cr.LineTo(191.722656, 67.113281);
            cr.ClosePath();
            cr.MoveTo(191.722656, 42.070313);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            matrix = new Matrix(1.001692, 0, 0, 1.001692, 232.392555, -324.548223);
            pattern.Matrix = matrix;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(229.386719, 4.007813);
            cr.LineTo(254.429688, 4.007813);
            cr.LineTo(254.429688, 29.050781);
            cr.LineTo(229.386719, 29.050781);
            cr.ClosePath();
            cr.MoveTo(229.386719, 4.007813);
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
            cr.MoveTo(229.386719, 4.007813);
            cr.LineTo(254.429688, 4.007813);
            cr.LineTo(254.429688, 29.050781);
            cr.LineTo(229.386719, 29.050781);
            cr.ClosePath();
            cr.MoveTo(229.386719, 4.007813);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            matrix = new Matrix(1.001692, 0, 0, 1.001692, 232.392555, -324.548223);
            pattern.Matrix = matrix;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(266.949219, 4.007813);
            cr.LineTo(291.992188, 4.007813);
            cr.LineTo(291.992188, 29.050781);
            cr.LineTo(266.949219, 29.050781);
            cr.ClosePath();
            cr.MoveTo(266.949219, 4.007813);
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
            cr.MoveTo(266.949219, 4.007813);
            cr.LineTo(291.992188, 4.007813);
            cr.LineTo(291.992188, 29.050781);
            cr.LineTo(266.949219, 29.050781);
            cr.ClosePath();
            cr.MoveTo(266.949219, 4.007813);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            matrix = new Matrix(1.001692, 0, 0, 1.001692, 232.392555, -324.548223);
            pattern.Matrix = matrix;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(229.386719, 42.070313);
            cr.LineTo(254.429688, 42.070313);
            cr.LineTo(254.429688, 67.113281);
            cr.LineTo(229.386719, 67.113281);
            cr.ClosePath();
            cr.MoveTo(229.386719, 42.070313);
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
            cr.MoveTo(229.386719, 42.070313);
            cr.LineTo(254.429688, 42.070313);
            cr.LineTo(254.429688, 67.113281);
            cr.LineTo(229.386719, 67.113281);
            cr.ClosePath();
            cr.MoveTo(229.386719, 42.070313);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            matrix = new Matrix(1.001692, 0, 0, 1.001692, 232.392555, -324.548223);
            pattern.Matrix = matrix;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(266.949219, 42.070313);
            cr.LineTo(291.992188, 42.070313);
            cr.LineTo(291.992188, 67.113281);
            cr.LineTo(266.949219, 67.113281);
            cr.ClosePath();
            cr.MoveTo(266.949219, 42.070313);
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
            cr.MoveTo(266.949219, 42.070313);
            cr.LineTo(291.992188, 42.070313);
            cr.LineTo(291.992188, 67.113281);
            cr.LineTo(266.949219, 67.113281);
            cr.ClosePath();
            cr.MoveTo(266.949219, 42.070313);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            matrix = new Matrix(1.001692, 0, 0, 1.001692, 232.392555, -324.548223);
            pattern.Matrix = matrix;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(154.058594, 79.132813);
            cr.LineTo(179.101563, 79.132813);
            cr.LineTo(179.101563, 104.175781);
            cr.LineTo(154.058594, 104.175781);
            cr.ClosePath();
            cr.MoveTo(154.058594, 79.132813);
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
            cr.MoveTo(154.058594, 79.132813);
            cr.LineTo(179.101563, 79.132813);
            cr.LineTo(179.101563, 104.175781);
            cr.LineTo(154.058594, 104.175781);
            cr.ClosePath();
            cr.MoveTo(154.058594, 79.132813);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            matrix = new Matrix(1.001692, 0, 0, 1.001692, 232.392555, -324.548223);
            pattern.Matrix = matrix;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(191.625, 79.132813);
            cr.LineTo(216.664063, 79.132813);
            cr.LineTo(216.664063, 104.175781);
            cr.LineTo(191.625, 104.175781);
            cr.ClosePath();
            cr.MoveTo(191.625, 79.132813);
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
            cr.MoveTo(191.625, 79.132813);
            cr.LineTo(216.664063, 79.132813);
            cr.LineTo(216.664063, 104.175781);
            cr.LineTo(191.625, 104.175781);
            cr.ClosePath();
            cr.MoveTo(191.625, 79.132813);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            matrix = new Matrix(1.001692, 0, 0, 1.001692, 232.392555, -324.548223);
            pattern.Matrix = matrix;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(154.058594, 117.199219);
            cr.LineTo(179.101563, 117.199219);
            cr.LineTo(179.101563, 142.242188);
            cr.LineTo(154.058594, 142.242188);
            cr.ClosePath();
            cr.MoveTo(154.058594, 117.199219);
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
            cr.MoveTo(154.058594, 117.199219);
            cr.LineTo(179.101563, 117.199219);
            cr.LineTo(179.101563, 142.242188);
            cr.LineTo(154.058594, 142.242188);
            cr.ClosePath();
            cr.MoveTo(154.058594, 117.199219);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            matrix = new Matrix(1.001692, 0, 0, 1.001692, 232.392555, -324.548223);
            pattern.Matrix = matrix;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(191.625, 117.199219);
            cr.LineTo(216.664063, 117.199219);
            cr.LineTo(216.664063, 142.242188);
            cr.LineTo(191.625, 142.242188);
            cr.ClosePath();
            cr.MoveTo(191.625, 117.199219);
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
            cr.MoveTo(191.625, 117.199219);
            cr.LineTo(216.664063, 117.199219);
            cr.LineTo(216.664063, 142.242188);
            cr.LineTo(191.625, 142.242188);
            cr.ClosePath();
            cr.MoveTo(191.625, 117.199219);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            matrix = new Matrix(1.001692, 0, 0, 1.001692, 232.392555, -324.548223);
            pattern.Matrix = matrix;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(229.289063, 79.132813);
            cr.LineTo(254.328125, 79.132813);
            cr.LineTo(254.328125, 104.175781);
            cr.LineTo(229.289063, 104.175781);
            cr.ClosePath();
            cr.MoveTo(229.289063, 79.132813);
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
            cr.MoveTo(229.289063, 79.132813);
            cr.LineTo(254.328125, 79.132813);
            cr.LineTo(254.328125, 104.175781);
            cr.LineTo(229.289063, 104.175781);
            cr.ClosePath();
            cr.MoveTo(229.289063, 79.132813);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            matrix = new Matrix(1.001692, 0, 0, 1.001692, 232.392555, -324.548223);
            pattern.Matrix = matrix;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(266.851563, 79.132813);
            cr.LineTo(291.894531, 79.132813);
            cr.LineTo(291.894531, 104.175781);
            cr.LineTo(266.851563, 104.175781);
            cr.ClosePath();
            cr.MoveTo(266.851563, 79.132813);
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
            cr.MoveTo(266.851563, 79.132813);
            cr.LineTo(291.894531, 79.132813);
            cr.LineTo(291.894531, 104.175781);
            cr.LineTo(266.851563, 104.175781);
            cr.ClosePath();
            cr.MoveTo(266.851563, 79.132813);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            matrix = new Matrix(1.001692, 0, 0, 1.001692, 232.392555, -324.548223);
            pattern.Matrix = matrix;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(229.289063, 117.199219);
            cr.LineTo(254.328125, 117.199219);
            cr.LineTo(254.328125, 142.242188);
            cr.LineTo(229.289063, 142.242188);
            cr.ClosePath();
            cr.MoveTo(229.289063, 117.199219);
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
            cr.MoveTo(229.289063, 117.199219);
            cr.LineTo(254.328125, 117.199219);
            cr.LineTo(254.328125, 142.242188);
            cr.LineTo(229.289063, 142.242188);
            cr.ClosePath();
            cr.MoveTo(229.289063, 117.199219);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            matrix = new Matrix(1.001692, 0, 0, 1.001692, 232.392555, -324.548223);
            pattern.Matrix = matrix;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(266.851563, 117.199219);
            cr.LineTo(291.894531, 117.199219);
            cr.LineTo(291.894531, 142.242188);
            cr.LineTo(266.851563, 142.242188);
            cr.ClosePath();
            cr.MoveTo(266.851563, 117.199219);
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
            cr.MoveTo(266.851563, 117.199219);
            cr.LineTo(291.894531, 117.199219);
            cr.LineTo(291.894531, 142.242188);
            cr.LineTo(266.851563, 142.242188);
            cr.ClosePath();
            cr.MoveTo(266.851563, 117.199219);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            matrix = new Matrix(1.001692, 0, 0, 1.001692, 232.392555, -324.548223);
            pattern.Matrix = matrix;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(4.105469, 153.757813);
            cr.LineTo(29.148438, 153.757813);
            cr.LineTo(29.148438, 178.800781);
            cr.LineTo(4.105469, 178.800781);
            cr.ClosePath();
            cr.MoveTo(4.105469, 153.757813);
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
            cr.MoveTo(4.105469, 153.757813);
            cr.LineTo(29.148438, 153.757813);
            cr.LineTo(29.148438, 178.800781);
            cr.LineTo(4.105469, 178.800781);
            cr.ClosePath();
            cr.MoveTo(4.105469, 153.757813);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            matrix = new Matrix(1.001692, 0, 0, 1.001692, 232.392555, -324.548223);
            pattern.Matrix = matrix;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(41.671875, 153.757813);
            cr.LineTo(66.710938, 153.757813);
            cr.LineTo(66.710938, 178.800781);
            cr.LineTo(41.671875, 178.800781);
            cr.ClosePath();
            cr.MoveTo(41.671875, 153.757813);
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
            cr.MoveTo(41.671875, 153.757813);
            cr.LineTo(66.710938, 153.757813);
            cr.LineTo(66.710938, 178.800781);
            cr.LineTo(41.671875, 178.800781);
            cr.ClosePath();
            cr.MoveTo(41.671875, 153.757813);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            matrix = new Matrix(1.001692, 0, 0, 1.001692, 232.392555, -324.548223);
            pattern.Matrix = matrix;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(4.105469, 191.824219);
            cr.LineTo(29.148438, 191.824219);
            cr.LineTo(29.148438, 216.867188);
            cr.LineTo(4.105469, 216.867188);
            cr.ClosePath();
            cr.MoveTo(4.105469, 191.824219);
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
            cr.MoveTo(4.105469, 191.824219);
            cr.LineTo(29.148438, 191.824219);
            cr.LineTo(29.148438, 216.867188);
            cr.LineTo(4.105469, 216.867188);
            cr.ClosePath();
            cr.MoveTo(4.105469, 191.824219);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            matrix = new Matrix(1.001692, 0, 0, 1.001692, 232.392555, -324.548223);
            pattern.Matrix = matrix;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(41.671875, 191.824219);
            cr.LineTo(66.710938, 191.824219);
            cr.LineTo(66.710938, 216.867188);
            cr.LineTo(41.671875, 216.867188);
            cr.ClosePath();
            cr.MoveTo(41.671875, 191.824219);
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
            cr.MoveTo(41.671875, 191.824219);
            cr.LineTo(66.710938, 191.824219);
            cr.LineTo(66.710938, 216.867188);
            cr.LineTo(41.671875, 216.867188);
            cr.ClosePath();
            cr.MoveTo(41.671875, 191.824219);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            matrix = new Matrix(1.001692, 0, 0, 1.001692, 232.392555, -324.548223);
            pattern.Matrix = matrix;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(79.335938, 153.757813);
            cr.LineTo(104.375, 153.757813);
            cr.LineTo(104.375, 178.800781);
            cr.LineTo(79.335938, 178.800781);
            cr.ClosePath();
            cr.MoveTo(79.335938, 153.757813);
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
            cr.MoveTo(79.335938, 153.757813);
            cr.LineTo(104.375, 153.757813);
            cr.LineTo(104.375, 178.800781);
            cr.LineTo(79.335938, 178.800781);
            cr.ClosePath();
            cr.MoveTo(79.335938, 153.757813);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            matrix = new Matrix(1.001692, 0, 0, 1.001692, 232.392555, -324.548223);
            pattern.Matrix = matrix;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(116.898438, 153.757813);
            cr.LineTo(141.941406, 153.757813);
            cr.LineTo(141.941406, 178.800781);
            cr.LineTo(116.898438, 178.800781);
            cr.ClosePath();
            cr.MoveTo(116.898438, 153.757813);
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
            cr.MoveTo(116.898438, 153.757813);
            cr.LineTo(141.941406, 153.757813);
            cr.LineTo(141.941406, 178.800781);
            cr.LineTo(116.898438, 178.800781);
            cr.ClosePath();
            cr.MoveTo(116.898438, 153.757813);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            matrix = new Matrix(1.001692, 0, 0, 1.001692, 232.392555, -324.548223);
            pattern.Matrix = matrix;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(79.335938, 191.824219);
            cr.LineTo(104.375, 191.824219);
            cr.LineTo(104.375, 216.867188);
            cr.LineTo(79.335938, 216.867188);
            cr.ClosePath();
            cr.MoveTo(79.335938, 191.824219);
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
            cr.MoveTo(79.335938, 191.824219);
            cr.LineTo(104.375, 191.824219);
            cr.LineTo(104.375, 216.867188);
            cr.LineTo(79.335938, 216.867188);
            cr.ClosePath();
            cr.MoveTo(79.335938, 191.824219);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            matrix = new Matrix(1.001692, 0, 0, 1.001692, 232.392555, -324.548223);
            pattern.Matrix = matrix;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(116.898438, 191.824219);
            cr.LineTo(141.941406, 191.824219);
            cr.LineTo(141.941406, 216.867188);
            cr.LineTo(116.898438, 216.867188);
            cr.ClosePath();
            cr.MoveTo(116.898438, 191.824219);
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
            cr.MoveTo(116.898438, 191.824219);
            cr.LineTo(141.941406, 191.824219);
            cr.LineTo(141.941406, 216.867188);
            cr.LineTo(116.898438, 216.867188);
            cr.ClosePath();
            cr.MoveTo(116.898438, 191.824219);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            matrix = new Matrix(1.001692, 0, 0, 1.001692, 232.392555, -324.548223);
            pattern.Matrix = matrix;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(4.007813, 228.886719);
            cr.LineTo(29.050781, 228.886719);
            cr.LineTo(29.050781, 253.929688);
            cr.LineTo(4.007813, 253.929688);
            cr.ClosePath();
            cr.MoveTo(4.007813, 228.886719);
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
            cr.MoveTo(4.007813, 228.886719);
            cr.LineTo(29.050781, 228.886719);
            cr.LineTo(29.050781, 253.929688);
            cr.LineTo(4.007813, 253.929688);
            cr.ClosePath();
            cr.MoveTo(4.007813, 228.886719);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            matrix = new Matrix(1.001692, 0, 0, 1.001692, 232.392555, -324.548223);
            pattern.Matrix = matrix;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(41.570313, 228.886719);
            cr.LineTo(66.613281, 228.886719);
            cr.LineTo(66.613281, 253.929688);
            cr.LineTo(41.570313, 253.929688);
            cr.ClosePath();
            cr.MoveTo(41.570313, 228.886719);
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
            cr.MoveTo(41.570313, 228.886719);
            cr.LineTo(66.613281, 228.886719);
            cr.LineTo(66.613281, 253.929688);
            cr.LineTo(41.570313, 253.929688);
            cr.ClosePath();
            cr.MoveTo(41.570313, 228.886719);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            matrix = new Matrix(1.001692, 0, 0, 1.001692, 232.392555, -324.548223);
            pattern.Matrix = matrix;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(4.007813, 266.949219);
            cr.LineTo(29.050781, 266.949219);
            cr.LineTo(29.050781, 291.992188);
            cr.LineTo(4.007813, 291.992188);
            cr.ClosePath();
            cr.MoveTo(4.007813, 266.949219);
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
            cr.MoveTo(4.007813, 266.949219);
            cr.LineTo(29.050781, 266.949219);
            cr.LineTo(29.050781, 291.992188);
            cr.LineTo(4.007813, 291.992188);
            cr.ClosePath();
            cr.MoveTo(4.007813, 266.949219);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            matrix = new Matrix(1.001692, 0, 0, 1.001692, 232.392555, -324.548223);
            pattern.Matrix = matrix;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(41.570313, 266.949219);
            cr.LineTo(66.613281, 266.949219);
            cr.LineTo(66.613281, 291.992188);
            cr.LineTo(41.570313, 291.992188);
            cr.ClosePath();
            cr.MoveTo(41.570313, 266.949219);
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
            cr.MoveTo(41.570313, 266.949219);
            cr.LineTo(66.613281, 266.949219);
            cr.LineTo(66.613281, 291.992188);
            cr.LineTo(41.570313, 291.992188);
            cr.ClosePath();
            cr.MoveTo(41.570313, 266.949219);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            matrix = new Matrix(1.001692, 0, 0, 1.001692, 232.392555, -324.548223);
            pattern.Matrix = matrix;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(79.234375, 228.886719);
            cr.LineTo(104.277344, 228.886719);
            cr.LineTo(104.277344, 253.929688);
            cr.LineTo(79.234375, 253.929688);
            cr.ClosePath();
            cr.MoveTo(79.234375, 228.886719);
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
            cr.MoveTo(79.234375, 228.886719);
            cr.LineTo(104.277344, 228.886719);
            cr.LineTo(104.277344, 253.929688);
            cr.LineTo(79.234375, 253.929688);
            cr.ClosePath();
            cr.MoveTo(79.234375, 228.886719);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            matrix = new Matrix(1.001692, 0, 0, 1.001692, 232.392555, -324.548223);
            pattern.Matrix = matrix;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(116.796875, 228.886719);
            cr.LineTo(141.839844, 228.886719);
            cr.LineTo(141.839844, 253.929688);
            cr.LineTo(116.796875, 253.929688);
            cr.ClosePath();
            cr.MoveTo(116.796875, 228.886719);
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
            cr.MoveTo(116.796875, 228.886719);
            cr.LineTo(141.839844, 228.886719);
            cr.LineTo(141.839844, 253.929688);
            cr.LineTo(116.796875, 253.929688);
            cr.ClosePath();
            cr.MoveTo(116.796875, 228.886719);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            matrix = new Matrix(1.001692, 0, 0, 1.001692, 232.392555, -324.548223);
            pattern.Matrix = matrix;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(79.234375, 266.949219);
            cr.LineTo(104.277344, 266.949219);
            cr.LineTo(104.277344, 291.992188);
            cr.LineTo(79.234375, 291.992188);
            cr.ClosePath();
            cr.MoveTo(79.234375, 266.949219);
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
            cr.MoveTo(79.234375, 266.949219);
            cr.LineTo(104.277344, 266.949219);
            cr.LineTo(104.277344, 291.992188);
            cr.LineTo(79.234375, 291.992188);
            cr.ClosePath();
            cr.MoveTo(79.234375, 266.949219);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            matrix = new Matrix(1.001692, 0, 0, 1.001692, 232.392555, -324.548223);
            pattern.Matrix = matrix;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(116.796875, 266.949219);
            cr.LineTo(141.839844, 266.949219);
            cr.LineTo(141.839844, 291.992188);
            cr.LineTo(116.796875, 291.992188);
            cr.ClosePath();
            cr.MoveTo(116.796875, 266.949219);
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
            cr.MoveTo(116.796875, 266.949219);
            cr.LineTo(141.839844, 266.949219);
            cr.LineTo(141.839844, 291.992188);
            cr.LineTo(116.796875, 291.992188);
            cr.ClosePath();
            cr.MoveTo(116.796875, 266.949219);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            matrix = new Matrix(1.001692, 0, 0, 1.001692, 232.392555, -324.548223);
            pattern.Matrix = matrix;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(154.058594, 153.757813);
            cr.LineTo(179.101563, 153.757813);
            cr.LineTo(179.101563, 178.800781);
            cr.LineTo(154.058594, 178.800781);
            cr.ClosePath();
            cr.MoveTo(154.058594, 153.757813);
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
            cr.MoveTo(154.058594, 153.757813);
            cr.LineTo(179.101563, 153.757813);
            cr.LineTo(179.101563, 178.800781);
            cr.LineTo(154.058594, 178.800781);
            cr.ClosePath();
            cr.MoveTo(154.058594, 153.757813);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            matrix = new Matrix(1.001692, 0, 0, 1.001692, 232.392555, -324.548223);
            pattern.Matrix = matrix;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(191.625, 153.757813);
            cr.LineTo(216.664063, 153.757813);
            cr.LineTo(216.664063, 178.800781);
            cr.LineTo(191.625, 178.800781);
            cr.ClosePath();
            cr.MoveTo(191.625, 153.757813);
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
            cr.MoveTo(191.625, 153.757813);
            cr.LineTo(216.664063, 153.757813);
            cr.LineTo(216.664063, 178.800781);
            cr.LineTo(191.625, 178.800781);
            cr.ClosePath();
            cr.MoveTo(191.625, 153.757813);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            matrix = new Matrix(1.001692, 0, 0, 1.001692, 232.392555, -324.548223);
            pattern.Matrix = matrix;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(154.058594, 191.824219);
            cr.LineTo(179.101563, 191.824219);
            cr.LineTo(179.101563, 216.867188);
            cr.LineTo(154.058594, 216.867188);
            cr.ClosePath();
            cr.MoveTo(154.058594, 191.824219);
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
            cr.MoveTo(154.058594, 191.824219);
            cr.LineTo(179.101563, 191.824219);
            cr.LineTo(179.101563, 216.867188);
            cr.LineTo(154.058594, 216.867188);
            cr.ClosePath();
            cr.MoveTo(154.058594, 191.824219);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            matrix = new Matrix(1.001692, 0, 0, 1.001692, 232.392555, -324.548223);
            pattern.Matrix = matrix;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(191.625, 191.824219);
            cr.LineTo(216.664063, 191.824219);
            cr.LineTo(216.664063, 216.867188);
            cr.LineTo(191.625, 216.867188);
            cr.ClosePath();
            cr.MoveTo(191.625, 191.824219);
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
            cr.MoveTo(191.625, 191.824219);
            cr.LineTo(216.664063, 191.824219);
            cr.LineTo(216.664063, 216.867188);
            cr.LineTo(191.625, 216.867188);
            cr.ClosePath();
            cr.MoveTo(191.625, 191.824219);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            matrix = new Matrix(1.001692, 0, 0, 1.001692, 232.392555, -324.548223);
            pattern.Matrix = matrix;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(229.289063, 153.757813);
            cr.LineTo(254.328125, 153.757813);
            cr.LineTo(254.328125, 178.800781);
            cr.LineTo(229.289063, 178.800781);
            cr.ClosePath();
            cr.MoveTo(229.289063, 153.757813);
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
            cr.MoveTo(229.289063, 153.757813);
            cr.LineTo(254.328125, 153.757813);
            cr.LineTo(254.328125, 178.800781);
            cr.LineTo(229.289063, 178.800781);
            cr.ClosePath();
            cr.MoveTo(229.289063, 153.757813);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            matrix = new Matrix(1.001692, 0, 0, 1.001692, 232.392555, -324.548223);
            pattern.Matrix = matrix;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(266.851563, 153.757813);
            cr.LineTo(291.894531, 153.757813);
            cr.LineTo(291.894531, 178.800781);
            cr.LineTo(266.851563, 178.800781);
            cr.ClosePath();
            cr.MoveTo(266.851563, 153.757813);
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
            cr.MoveTo(266.851563, 153.757813);
            cr.LineTo(291.894531, 153.757813);
            cr.LineTo(291.894531, 178.800781);
            cr.LineTo(266.851563, 178.800781);
            cr.ClosePath();
            cr.MoveTo(266.851563, 153.757813);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            matrix = new Matrix(1.001692, 0, 0, 1.001692, 232.392555, -324.548223);
            pattern.Matrix = matrix;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(229.289063, 191.824219);
            cr.LineTo(254.328125, 191.824219);
            cr.LineTo(254.328125, 216.867188);
            cr.LineTo(229.289063, 216.867188);
            cr.ClosePath();
            cr.MoveTo(229.289063, 191.824219);
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
            cr.MoveTo(229.289063, 191.824219);
            cr.LineTo(254.328125, 191.824219);
            cr.LineTo(254.328125, 216.867188);
            cr.LineTo(229.289063, 216.867188);
            cr.ClosePath();
            cr.MoveTo(229.289063, 191.824219);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            matrix = new Matrix(1.001692, 0, 0, 1.001692, 232.392555, -324.548223);
            pattern.Matrix = matrix;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(266.851563, 191.824219);
            cr.LineTo(291.894531, 191.824219);
            cr.LineTo(291.894531, 216.867188);
            cr.LineTo(266.851563, 216.867188);
            cr.ClosePath();
            cr.MoveTo(266.851563, 191.824219);
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
            cr.MoveTo(266.851563, 191.824219);
            cr.LineTo(291.894531, 191.824219);
            cr.LineTo(291.894531, 216.867188);
            cr.LineTo(266.851563, 216.867188);
            cr.ClosePath();
            cr.MoveTo(266.851563, 191.824219);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            matrix = new Matrix(1.001692, 0, 0, 1.001692, 232.392555, -324.548223);
            pattern.Matrix = matrix;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(153.960938, 228.886719);
            cr.LineTo(179.003906, 228.886719);
            cr.LineTo(179.003906, 253.929688);
            cr.LineTo(153.960938, 253.929688);
            cr.ClosePath();
            cr.MoveTo(153.960938, 228.886719);
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
            cr.MoveTo(153.960938, 228.886719);
            cr.LineTo(179.003906, 228.886719);
            cr.LineTo(179.003906, 253.929688);
            cr.LineTo(153.960938, 253.929688);
            cr.ClosePath();
            cr.MoveTo(153.960938, 228.886719);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            matrix = new Matrix(1.001692, 0, 0, 1.001692, 232.392555, -324.548223);
            pattern.Matrix = matrix;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(191.523438, 228.886719);
            cr.LineTo(216.566406, 228.886719);
            cr.LineTo(216.566406, 253.929688);
            cr.LineTo(191.523438, 253.929688);
            cr.ClosePath();
            cr.MoveTo(191.523438, 228.886719);
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
            cr.MoveTo(191.523438, 228.886719);
            cr.LineTo(216.566406, 228.886719);
            cr.LineTo(216.566406, 253.929688);
            cr.LineTo(191.523438, 253.929688);
            cr.ClosePath();
            cr.MoveTo(191.523438, 228.886719);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            matrix = new Matrix(1.001692, 0, 0, 1.001692, 232.392555, -324.548223);
            pattern.Matrix = matrix;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(153.960938, 266.949219);
            cr.LineTo(179.003906, 266.949219);
            cr.LineTo(179.003906, 291.992188);
            cr.LineTo(153.960938, 291.992188);
            cr.ClosePath();
            cr.MoveTo(153.960938, 266.949219);
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
            cr.MoveTo(153.960938, 266.949219);
            cr.LineTo(179.003906, 266.949219);
            cr.LineTo(179.003906, 291.992188);
            cr.LineTo(153.960938, 291.992188);
            cr.ClosePath();
            cr.MoveTo(153.960938, 266.949219);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            matrix = new Matrix(1.001692, 0, 0, 1.001692, 232.392555, -324.548223);
            pattern.Matrix = matrix;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(191.523438, 266.949219);
            cr.LineTo(216.566406, 266.949219);
            cr.LineTo(216.566406, 291.992188);
            cr.LineTo(191.523438, 291.992188);
            cr.ClosePath();
            cr.MoveTo(191.523438, 266.949219);
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
            cr.MoveTo(191.523438, 266.949219);
            cr.LineTo(216.566406, 266.949219);
            cr.LineTo(216.566406, 291.992188);
            cr.LineTo(191.523438, 291.992188);
            cr.ClosePath();
            cr.MoveTo(191.523438, 266.949219);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            matrix = new Matrix(1.001692, 0, 0, 1.001692, 232.392555, -324.548223);
            pattern.Matrix = matrix;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(229.1875, 228.886719);
            cr.LineTo(254.230469, 228.886719);
            cr.LineTo(254.230469, 253.929688);
            cr.LineTo(229.1875, 253.929688);
            cr.ClosePath();
            cr.MoveTo(229.1875, 228.886719);
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
            cr.MoveTo(229.1875, 228.886719);
            cr.LineTo(254.230469, 228.886719);
            cr.LineTo(254.230469, 253.929688);
            cr.LineTo(229.1875, 253.929688);
            cr.ClosePath();
            cr.MoveTo(229.1875, 228.886719);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            matrix = new Matrix(1.001692, 0, 0, 1.001692, 232.392555, -324.548223);
            pattern.Matrix = matrix;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(266.75, 228.886719);
            cr.LineTo(291.792969, 228.886719);
            cr.LineTo(291.792969, 253.929688);
            cr.LineTo(266.75, 253.929688);
            cr.ClosePath();
            cr.MoveTo(266.75, 228.886719);
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
            cr.MoveTo(266.75, 228.886719);
            cr.LineTo(291.792969, 228.886719);
            cr.LineTo(291.792969, 253.929688);
            cr.LineTo(266.75, 253.929688);
            cr.ClosePath();
            cr.MoveTo(266.75, 228.886719);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            matrix = new Matrix(1.001692, 0, 0, 1.001692, 232.392555, -324.548223);
            pattern.Matrix = matrix;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(229.1875, 266.949219);
            cr.LineTo(254.230469, 266.949219);
            cr.LineTo(254.230469, 291.992188);
            cr.LineTo(229.1875, 291.992188);
            cr.ClosePath();
            cr.MoveTo(229.1875, 266.949219);
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
            cr.MoveTo(229.1875, 266.949219);
            cr.LineTo(254.230469, 266.949219);
            cr.LineTo(254.230469, 291.992188);
            cr.LineTo(229.1875, 291.992188);
            cr.ClosePath();
            cr.MoveTo(229.1875, 266.949219);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            matrix = new Matrix(1.001692, 0, 0, 1.001692, 232.392555, -324.548223);
            pattern.Matrix = matrix;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(266.75, 266.949219);
            cr.LineTo(291.792969, 266.949219);
            cr.LineTo(291.792969, 291.992188);
            cr.LineTo(266.75, 291.992188);
            cr.ClosePath();
            cr.MoveTo(266.75, 266.949219);
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
            cr.MoveTo(266.75, 266.949219);
            cr.LineTo(291.792969, 266.949219);
            cr.LineTo(291.792969, 291.992188);
            cr.LineTo(266.75, 291.992188);
            cr.ClosePath();
            cr.MoveTo(266.75, 266.949219);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            matrix = new Matrix(1.001692, 0, 0, 1.001692, 232.392555, -324.548223);
            pattern.Matrix = matrix;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Restore();
        }
    }
}
