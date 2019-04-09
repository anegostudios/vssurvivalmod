using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class ItemHammer : Item
    {
        public override void OnHeldAttackStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandHandling handling)
        {
            if (blockSel == null) return;
            if (!(byEntity.World.BlockAccessor.GetBlock(blockSel.Position) is BlockAnvil)) return;

            BlockEntityAnvil bea = byEntity.World.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityAnvil;
            if (bea == null) return;

            IPlayer byPlayer = null;
            if (byEntity is EntityPlayer) byPlayer = byEntity.World.PlayerByUid(((EntityPlayer)byEntity).PlayerUID);
            if (byPlayer == null) return;


            bea.OnBeginUse(byPlayer, blockSel);

            byEntity.World.RegisterCallback((dt) =>
            {
                if (byEntity.Controls.HandUse == EnumHandInteract.HeldItemAttack)
                {
                    byPlayer.Entity.World.PlaySoundAt(new AssetLocation("sounds/effect/anvilhit"), byPlayer, byPlayer);
                }
            }, 464);


            handling = EnumHandHandling.PreventDefault;
        }

        public override bool OnHeldAttackCancel(float secondsPassed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSelection, EntitySelection entitySel, EnumItemUseCancelReason cancelReason)
        {
            return false;
        }

        public override bool OnHeldAttackStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSelection, EntitySelection entitySel)
        {
            if (byEntity.World is IClientWorldAccessor)
            {
                ModelTransform tf = new ModelTransform();
                tf.EnsureDefaultValues();

                //30 - secondsUsed * 90 * 15 = -110
                //-30 + secondsUsed * 90 * 15 = 110
                //secondsUsed * 90 * 15 = 110 - 30
                //secondsUsed = (110 - 30) / (90*15)

                float speed = 5 + 20 * Math.Max(0, secondsUsed - 0.25f);
                float start = secondsUsed * 120;
                float rotationY = Math.Min(110, Math.Max(0, secondsUsed - 0.25f) * 90 * speed - start);


                tf.Origin.Set(0, 0, 0.5f);
                tf.Translation.Set(0, 0, Math.Max(-0.25f, -1.25f * Math.Max(0, secondsUsed - 0.25f)));
                tf.Rotation.Y = rotationY;
                byEntity.Controls.UsingHeldItemTransformBefore = tf;
            }
            

            return secondsUsed < 0.6f;
        }

        public override void OnHeldAttackStop(float secondsPassed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (blockSel == null || secondsPassed < 0.4f) return;
            if (!(byEntity.World.BlockAccessor.GetBlock(blockSel.Position) is BlockAnvil)) return;

            BlockEntityAnvil bea = byEntity.World.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityAnvil;
            if (bea == null) return;

            IPlayer byPlayer = null;
            if (byEntity is EntityPlayer) byPlayer = byEntity.World.PlayerByUid(((EntityPlayer)byEntity).PlayerUID);
            if (byPlayer == null) return;


            if (bea.AvailableVoxels <= 0 && GetToolMode(slot, byPlayer, blockSel) != 5)
            {
                return;
            }

            // The server side call is made using a custom network packet
            if (byEntity.World is IClientWorldAccessor)
            {
                bea.OnUseOver(byPlayer, blockSel.SelectionBoxIndex);
            }
            
        }


        public override int GetQuantityToolModes(ItemSlot slot, IPlayer byPlayer, BlockSelection blockSelection)
        {
            if (blockSelection == null) return 0;
            Block block = byPlayer.Entity.World.BlockAccessor.GetBlock(blockSelection.Position);
            return block is BlockAnvil ? 6 : 0;
        }

        public override void DrawToolModeIcon(ItemSlot slot, IPlayer byPlayer, BlockSelection blockSelection, Context cr, int x, int y, int width, int height, int toolMode, int color)
        {
            float[] colordoubles = ColorUtil.ToRGBAFloats(color);
            
            switch (toolMode)
            {
                case 0:
                    DrawHit(cr, x, y, width, height, colordoubles);
                    break;
                case 1:
                    DrawUpset(cr, x, y, width, height, colordoubles, 0);
                    break;
                case 2:
                    DrawUpset(cr, x, y, width, height, colordoubles, GameMath.PI / 2);
                    break;
                case 3:
                    DrawUpset(cr, x, y, width, height, colordoubles, GameMath.PI);
                    break;
                case 4:
                    DrawUpset(cr, x, y, width, height, colordoubles, 3 * GameMath.PI / 2);
                    break;
                case 5:
                    DrawSplit(cr, x, y, width, height, colordoubles);
                    break;
            }
        }

        private void DrawSplit(Context cr, int x, int y, float width, float height, float[] colordoubles)
        {
            Pattern pattern = null;
            Matrix matrix = cr.Matrix;

            cr.Save();
            float w = 220;
            float h = 182;
            float scale = Math.Min(width / w, height / h);
            matrix.Translate(x + Math.Max(0, (width - w * scale) / 2), y + Math.Max(0, (height - h * scale) / 2));
            matrix.Scale(scale, scale);
            cr.Matrix = matrix;

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(colordoubles[0], colordoubles[1], colordoubles[2], colordoubles[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(59, 105.003906);
            cr.LineTo(1, 105.003906);
            cr.LineTo(1, 182.003906);
            cr.LineTo(101.5, 182.003906);
            cr.ClosePath();
            cr.MoveTo(59, 105.003906);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            cr.FillRule = FillRule.Winding;
            cr.FillPreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            cr.LineWidth = 1;
            cr.MiterLimit = 10;
            cr.LineCap = LineCap.Butt;
            cr.LineJoin = LineJoin.Miter;
            pattern = new SolidPattern(colordoubles[0], colordoubles[1], colordoubles[2], colordoubles[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(59, 105.003906);
            cr.LineTo(1, 105.003906);
            cr.LineTo(1, 182.003906);
            cr.LineTo(101.5, 182.003906);
            cr.ClosePath();
            cr.MoveTo(59, 105.003906);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(colordoubles[0], colordoubles[1], colordoubles[2], colordoubles[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(161.5, 105.003906);
            cr.LineTo(219.5, 105.003906);
            cr.LineTo(219.5, 182.003906);
            cr.LineTo(119, 182.003906);
            cr.ClosePath();
            cr.MoveTo(161.5, 105.003906);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            cr.FillRule = FillRule.Winding;
            cr.FillPreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            cr.LineWidth = 1;
            cr.MiterLimit = 10;
            cr.LineCap = LineCap.Butt;
            cr.LineJoin = LineJoin.Miter;
            pattern = new SolidPattern(colordoubles[0], colordoubles[1], colordoubles[2], colordoubles[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(161.5, 105.003906);
            cr.LineTo(219.5, 105.003906);
            cr.LineTo(219.5, 182.003906);
            cr.LineTo(119, 182.003906);
            cr.ClosePath();
            cr.MoveTo(161.5, 105.003906);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(colordoubles[0], colordoubles[1], colordoubles[2], colordoubles[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(106.648438, 118.003906);
            cr.CurveTo(104.824219, 113.109375, 103.148438, 108.210938, 101.621094, 103.316406);
            cr.CurveTo(100.0625, 98.421875, 98.644531, 93.523438, 97.25, 88.628906);
            cr.CurveTo(95.914063, 83.730469, 94.53125, 78.835938, 93.371094, 73.941406);
            cr.CurveTo(92.183594, 69.042969, 91.214844, 64.148438, 90.199219, 59.253906);
            cr.CurveTo(89.710938, 56.804688, 89.003906, 54.355469, 88.191406, 51.90625);
            cr.CurveTo(87.378906, 49.460938, 86.460938, 47.011719, 85.734375, 44.5625);
            cr.CurveTo(85.015625, 42.117188, 84.542969, 39.667969, 84.503906, 37.21875);
            cr.CurveTo(84.453125, 34.773438, 84.820313, 32.324219, 85.5, 29.875);
            cr.CurveTo(86.886719, 24.980469, 89.078125, 20.085938, 92.378906, 15.1875);
            cr.CurveTo(95.769531, 10.292969, 99.902344, 5.394531, 106.648438, 0.5);
            cr.LineTo(111.351563, 0.5);
            cr.CurveTo(118.097656, 5.394531, 122.230469, 10.292969, 125.621094, 15.1875);
            cr.CurveTo(128.921875, 20.085938, 131.113281, 24.980469, 132.5, 29.875);
            cr.CurveTo(133.179688, 32.324219, 133.546875, 34.773438, 133.496094, 37.21875);
            cr.CurveTo(133.457031, 39.667969, 132.984375, 42.117188, 132.265625, 44.5625);
            cr.CurveTo(131.539063, 47.011719, 130.621094, 49.460938, 129.808594, 51.90625);
            cr.CurveTo(128.996094, 54.355469, 128.289063, 56.804688, 127.800781, 59.253906);
            cr.CurveTo(126.785156, 64.148438, 125.820313, 69.042969, 124.628906, 73.941406);
            cr.CurveTo(123.46875, 78.835938, 122.085938, 83.730469, 120.75, 88.628906);
            cr.CurveTo(119.355469, 93.523438, 117.9375, 98.421875, 116.378906, 103.316406);
            cr.CurveTo(114.855469, 108.210938, 113.175781, 113.105469, 111.351563, 118.003906);
            cr.ClosePath();
            cr.MoveTo(106.648438, 118.003906);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            cr.FillRule = FillRule.Winding;
            cr.FillPreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            cr.LineWidth = 4;
            cr.MiterLimit = 10;
            cr.LineCap = LineCap.Butt;
            cr.LineJoin = LineJoin.Miter;
            pattern = new SolidPattern(colordoubles[0], colordoubles[1], colordoubles[2], colordoubles[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(130.261719, 118.003906);
            cr.LineTo(165.261719, 70.003906);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            cr.LineWidth = 4;
            cr.MiterLimit = 10;
            cr.LineCap = LineCap.Butt;
            cr.LineJoin = LineJoin.Miter;
            pattern = new SolidPattern(colordoubles[0], colordoubles[1], colordoubles[2], colordoubles[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(51.25, 70.003906);
            cr.LineTo(86.25, 118.003906);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Restore();
        }

        private void DrawUpset(Context cr, int x, int y, float width, float height, float[] colordoubles, double rot)
        {
            Pattern pattern = null;
            Matrix matrix = cr.Matrix;

            cr.Save();
            float w = 91;
            float h = 170;
            float scale = Math.Min(width / w, height / h);
            matrix.Translate(x + Math.Max(0, (width - w * scale) / 2), y + Math.Max(0, (height - h * scale) / 2));
            matrix.Scale(scale, scale);
            matrix.Translate(w / 2, h / 2);
            matrix.Rotate(rot);
            matrix.Translate(-w/2, -h/2);

            cr.Matrix = matrix;

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(colordoubles[0], colordoubles[1], colordoubles[2], colordoubles[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(91, 124.667969);
            cr.CurveTo(91, 149.519531, 70.851563, 169.667969, 46, 169.667969);
            cr.CurveTo(21.148438, 169.667969, 1, 149.519531, 1, 124.667969);
            cr.CurveTo(1, 99.816406, 21.148438, 79.667969, 46, 79.667969);
            cr.CurveTo(70.851563, 79.667969, 91, 99.816406, 91, 124.667969);
            cr.ClosePath();
            cr.MoveTo(91, 124.667969);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            cr.FillRule = FillRule.Winding;
            cr.FillPreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            cr.LineWidth = 1;
            cr.MiterLimit = 10;
            cr.LineCap = LineCap.Butt;
            cr.LineJoin = LineJoin.Miter;
            pattern = new SolidPattern(colordoubles[0], colordoubles[1], colordoubles[2], colordoubles[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(91, 124.667969);
            cr.CurveTo(91, 149.519531, 70.851563, 169.667969, 46, 169.667969);
            cr.CurveTo(21.148438, 169.667969, 1, 149.519531, 1, 124.667969);
            cr.CurveTo(1, 99.816406, 21.148438, 79.667969, 46, 79.667969);
            cr.CurveTo(70.851563, 79.667969, 91, 99.816406, 91, 124.667969);
            cr.ClosePath();
            cr.MoveTo(91, 124.667969);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(colordoubles[0], colordoubles[1], colordoubles[2], colordoubles[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(82.265625, 21.296875);
            cr.LineTo(47.160156, 0.5);
            cr.LineTo(11.734375, 21.296875);
            cr.LineTo(26.457031, 21.296875);
            cr.LineTo(26.457031, 71.335938);
            cr.LineTo(67.808594, 71.335938);
            cr.LineTo(67.808594, 21.296875);
            cr.ClosePath();
            cr.MoveTo(82.265625, 21.296875);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            cr.FillRule = FillRule.Winding;
            cr.FillPreserve();
            if (pattern != null) pattern.Dispose();

            cr.Restore();
        }

        private void DrawHit(Context cr, int x, int y, float width, float height, float[] colordoubles)
        {
            Pattern pattern = null;
            Matrix matrix = cr.Matrix;

            cr.Save();
            float w = 227;
            float h = 218;
            float scale = Math.Min(width / w, height / h);
            matrix.Translate(x + Math.Max(0, (width - w * scale) / 2), y + Math.Max(0, (height - h * scale) / 2));
            matrix.Scale(scale, scale);
            cr.Matrix = matrix;

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(colordoubles[0], colordoubles[1], colordoubles[2], colordoubles[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(159.96875, 110.125);
            cr.CurveTo(159.96875, 134.976563, 139.824219, 155.125, 114.96875, 155.125);
            cr.CurveTo(90.117188, 155.125, 69.96875, 134.976563, 69.96875, 110.125);
            cr.CurveTo(69.96875, 85.273438, 90.117188, 65.125, 114.96875, 65.125);
            cr.CurveTo(139.824219, 65.125, 159.96875, 85.273438, 159.96875, 110.125);
            cr.ClosePath();
            cr.MoveTo(159.96875, 110.125);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            cr.FillRule = FillRule.Winding;
            cr.FillPreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            cr.LineWidth = 1;
            cr.MiterLimit = 10;
            cr.LineCap = LineCap.Butt;
            cr.LineJoin = LineJoin.Miter;
            pattern = new SolidPattern(colordoubles[0], colordoubles[1], colordoubles[2], colordoubles[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(159.96875, 110.125);
            cr.CurveTo(159.96875, 134.976563, 139.824219, 155.125, 114.96875, 155.125);
            cr.CurveTo(90.117188, 155.125, 69.96875, 134.976563, 69.96875, 110.125);
            cr.CurveTo(69.96875, 85.273438, 90.117188, 65.125, 114.96875, 65.125);
            cr.CurveTo(139.824219, 65.125, 159.96875, 85.273438, 159.96875, 110.125);
            cr.ClosePath();
            cr.MoveTo(159.96875, 110.125);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(colordoubles[0], colordoubles[1], colordoubles[2], colordoubles[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(110.71875, 0);
            cr.LineTo(119.21875, 0);
            cr.LineTo(119.21875, 52);
            cr.LineTo(110.71875, 52);
            cr.ClosePath();
            cr.MoveTo(110.71875, 0);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            cr.FillRule = FillRule.Winding;
            cr.FillPreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            cr.LineWidth = 1;
            cr.MiterLimit = 10;
            cr.LineCap = LineCap.Butt;
            cr.LineJoin = LineJoin.Miter;
            pattern = new SolidPattern(colordoubles[0], colordoubles[1], colordoubles[2], colordoubles[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(110.71875, 0);
            cr.LineTo(119.21875, 0);
            cr.LineTo(119.21875, 52);
            cr.LineTo(110.71875, 52);
            cr.ClosePath();
            cr.MoveTo(110.71875, 0);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(colordoubles[0], colordoubles[1], colordoubles[2], colordoubles[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(110.71875, 164.710938);
            cr.LineTo(119.21875, 164.710938);
            cr.LineTo(119.21875, 216.710938);
            cr.LineTo(110.71875, 216.710938);
            cr.ClosePath();
            cr.MoveTo(110.71875, 164.710938);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            cr.FillRule = FillRule.Winding;
            cr.FillPreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            cr.LineWidth = 1;
            cr.MiterLimit = 10;
            cr.LineCap = LineCap.Butt;
            cr.LineJoin = LineJoin.Miter;
            pattern = new SolidPattern(colordoubles[0], colordoubles[1], colordoubles[2], colordoubles[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(110.71875, 164.710938);
            cr.LineTo(119.21875, 164.710938);
            cr.LineTo(119.21875, 216.710938);
            cr.LineTo(110.71875, 216.710938);
            cr.ClosePath();
            cr.MoveTo(110.71875, 164.710938);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(colordoubles[0], colordoubles[1], colordoubles[2], colordoubles[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(173.804688, 105.875);
            cr.LineTo(225.804688, 105.875);
            cr.LineTo(225.804688, 114.375);
            cr.LineTo(173.804688, 114.375);
            cr.ClosePath();
            cr.MoveTo(173.804688, 105.875);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            cr.FillRule = FillRule.Winding;
            cr.FillPreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            cr.LineWidth = 1;
            cr.MiterLimit = 10;
            cr.LineCap = LineCap.Butt;
            cr.LineJoin = LineJoin.Miter;
            pattern = new SolidPattern(colordoubles[0], colordoubles[1], colordoubles[2], colordoubles[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(173.804688, 105.875);
            cr.LineTo(225.804688, 105.875);
            cr.LineTo(225.804688, 114.375);
            cr.LineTo(173.804688, 114.375);
            cr.ClosePath();
            cr.MoveTo(173.804688, 105.875);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(colordoubles[0], colordoubles[1], colordoubles[2], colordoubles[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(0, 105.375);
            cr.LineTo(52, 105.375);
            cr.LineTo(52, 113.875);
            cr.LineTo(0, 113.875);
            cr.ClosePath();
            cr.MoveTo(0, 105.375);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            cr.FillRule = FillRule.Winding;
            cr.FillPreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            cr.LineWidth = 1;
            cr.MiterLimit = 10;
            cr.LineCap = LineCap.Butt;
            cr.LineJoin = LineJoin.Miter;
            pattern = new SolidPattern(colordoubles[0], colordoubles[1], colordoubles[2], colordoubles[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(0, 105.375);
            cr.LineTo(52, 105.375);
            cr.LineTo(52, 113.875);
            cr.LineTo(0, 113.875);
            cr.ClosePath();
            cr.MoveTo(0, 105.375);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(colordoubles[0], colordoubles[1], colordoubles[2], colordoubles[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(173.757813, 68.78125);
            cr.LineTo(167.75, 62.769531);
            cr.LineTo(204.515625, 26.003906);
            cr.LineTo(210.527344, 32.011719);
            cr.ClosePath();
            cr.MoveTo(173.757813, 68.78125);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            cr.FillRule = FillRule.Winding;
            cr.FillPreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            cr.LineWidth = 1;
            cr.MiterLimit = 10;
            cr.LineCap = LineCap.Butt;
            cr.LineJoin = LineJoin.Miter;
            pattern = new SolidPattern(colordoubles[0], colordoubles[1], colordoubles[2], colordoubles[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(173.757813, 68.78125);
            cr.LineTo(167.75, 62.769531);
            cr.LineTo(204.515625, 26.003906);
            cr.LineTo(210.527344, 32.011719);
            cr.ClosePath();
            cr.MoveTo(173.757813, 68.78125);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            matrix = new Matrix(-0.7071, -0.7071, 0.7071, -0.7071, 289.3736, 214.6403);
            pattern.Matrix = matrix;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(colordoubles[0], colordoubles[1], colordoubles[2], colordoubles[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(32.007813, 190.707031);
            cr.LineTo(25.996094, 184.699219);
            cr.LineTo(62.757813, 147.925781);
            cr.LineTo(68.769531, 153.933594);
            cr.ClosePath();
            cr.MoveTo(32.007813, 190.707031);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            cr.FillRule = FillRule.Winding;
            cr.FillPreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            cr.LineWidth = 1;
            cr.MiterLimit = 10;
            cr.LineCap = LineCap.Butt;
            cr.LineJoin = LineJoin.Miter;
            pattern = new SolidPattern(colordoubles[0], colordoubles[1], colordoubles[2], colordoubles[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(32.007813, 190.707031);
            cr.LineTo(25.996094, 184.699219);
            cr.LineTo(62.757813, 147.925781);
            cr.LineTo(68.769531, 153.933594);
            cr.ClosePath();
            cr.MoveTo(32.007813, 190.707031);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            matrix = new Matrix(-0.7072, -0.707, 0.707, -0.7072, -38.8126, 322.5648);
            pattern.Matrix = matrix;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(colordoubles[0], colordoubles[1], colordoubles[2], colordoubles[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(68.78125, 62.773438);
            cr.LineTo(62.769531, 68.78125);
            cr.LineTo(26, 32.015625);
            cr.LineTo(32.011719, 26.003906);
            cr.ClosePath();
            cr.MoveTo(68.78125, 62.773438);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            cr.FillRule = FillRule.Winding;
            cr.FillPreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            cr.LineWidth = 1;
            cr.MiterLimit = 10;
            cr.LineCap = LineCap.Butt;
            cr.LineJoin = LineJoin.Miter;
            pattern = new SolidPattern(colordoubles[0], colordoubles[1], colordoubles[2], colordoubles[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(68.78125, 62.773438);
            cr.LineTo(62.769531, 68.78125);
            cr.LineTo(26, 32.015625);
            cr.LineTo(32.011719, 26.003906);
            cr.ClosePath();
            cr.MoveTo(68.78125, 62.773438);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            matrix = new Matrix(-0.7071, 0.7071, -0.7071, -0.7071, 114.4105, 47.3931);
            pattern.Matrix = matrix;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(colordoubles[0], colordoubles[1], colordoubles[2], colordoubles[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(210.527344, 184.6875);
            cr.LineTo(204.515625, 190.695313);
            cr.LineTo(167.75, 153.921875);
            cr.LineTo(173.761719, 147.910156);
            cr.ClosePath();
            cr.MoveTo(210.527344, 184.6875);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            cr.FillRule = FillRule.Winding;
            cr.FillPreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            cr.LineWidth = 1;
            cr.MiterLimit = 10;
            cr.LineCap = LineCap.Butt;
            cr.LineJoin = LineJoin.Miter;
            pattern = new SolidPattern(colordoubles[0], colordoubles[1], colordoubles[2], colordoubles[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(210.527344, 184.6875);
            cr.LineTo(204.515625, 190.695313);
            cr.LineTo(167.75, 153.921875);
            cr.LineTo(173.761719, 147.910156);
            cr.ClosePath();
            cr.MoveTo(210.527344, 184.6875);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            matrix = new Matrix(-0.7072, 0.707, -0.707, -0.7072, 442.6037, 155.3283);
            pattern.Matrix = matrix;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Restore();
        }









        public override int GetToolMode(ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel)
        {
            return slot.Itemstack.Attributes.GetInt("toolMode");
        }

        public override void SetToolMode(ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel, int toolMode)
        {
            slot.Itemstack.Attributes.SetInt("toolMode", toolMode);
        }
    }
}
