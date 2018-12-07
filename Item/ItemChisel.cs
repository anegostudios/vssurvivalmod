using Cairo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    /// <summary>
    /// When right clicked on a block, this chisel tool will exchange given block into a chiseledblock which 
    /// takes on the model of the block the player interacted with in the first place, but with each voxel being selectable and removable
    /// </summary>
    public class ItemChisel : Item
    {
        public bool canMicroChisel;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            canMicroChisel = Attributes?["microBlockChiseling"].AsBool() == true;
        }

        public override void OnHeldAttackStart(IItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandHandling handling)
        {
            IPlayer byPlayer = (byEntity as EntityPlayer)?.Player;

            if (!canMicroChisel && byPlayer?.WorldData.CurrentGameMode != EnumGameMode.Creative)
            {
                base.OnHeldAttackStart(slot, byEntity, blockSel, entitySel, ref handling);
                return;
            }

            if (blockSel == null)
            {
                base.OnHeldAttackStart(slot, byEntity, blockSel, entitySel, ref handling);
                return;
            }

            Block block = byEntity.World.BlockAccessor.GetBlock(blockSel.Position);
            Block chiseledblock = byEntity.World.GetBlock(new AssetLocation("chiseledblock"));

            if (block == chiseledblock)
            {
                
                OnBlockInteract(byEntity.World, byPlayer, blockSel, true, ref handling);
                return;
            }
        }

        public override void OnHeldInteractStart(IItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandHandling handling)
        {
            IPlayer byPlayer = (byEntity as EntityPlayer)?.Player;

            if (!canMicroChisel && byPlayer?.WorldData.CurrentGameMode != EnumGameMode.Creative)
            {
                base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, ref handling);
                return;
            }

            if (blockSel == null)
            {
                base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, ref handling);
                return;
            }
            
            Block block = byEntity.World.BlockAccessor.GetBlock(blockSel.Position);


            Block chiseledblock = byEntity.World.GetBlock(new AssetLocation("chiseledblock"));

            if (block == chiseledblock)
            {
                OnBlockInteract(byEntity.World, byPlayer, blockSel, false, ref handling);
                return;
            }

            if (block.DrawType != API.Client.EnumDrawType.Cube) return;
            

            byEntity.World.BlockAccessor.SetBlock(chiseledblock.BlockId, blockSel.Position);

            BlockEntityChisel be = byEntity.World.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityChisel;
            if (be == null) return;

            be.WasPlaced(block);
            handling = EnumHandHandling.PreventDefaultAction;
        }


        public void OnBlockInteract(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, bool isBreak, ref EnumHandHandling handling)
        {
            BlockEntityChisel bec = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityChisel;
            if (bec != null)
            {
                bec.OnBlockInteract(byPlayer, blockSel, isBreak);
                handling = EnumHandHandling.PreventDefaultAction;
            }
        }



        public override int GetQuantityToolModes(IItemSlot slot, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (blockSel == null) return 0;
            Block block = byPlayer.Entity.World.BlockAccessor.GetBlock(blockSel.Position);
            return block is BlockChisel ? 5 : 0;
        }

        public override void DrawToolModeIcon(IItemSlot slot, IPlayer byPlayer, BlockSelection blockSelection, Context cr, int x, int y, int width, int height, int toolMode, int color)
        {
            double[] colordoubles = ColorUtil.ToRGBADoubles(color);

            switch (toolMode)
            {
                case 0: ItemClay.Drawcreate1_svg(cr, x, y, width, height, colordoubles); break;
                case 1: ItemClay.Drawcreate4_svg(cr, x, y, width, height, colordoubles); break;
                case 2: Drawcreate16_svg(cr, x, y, width, height, colordoubles); break;
                case 3: Drawcreate64_svg(cr, x, y, width, height, colordoubles); break;
                case 4: Drawrotate_svg(cr, x, y, width, height, colordoubles); break;
            }
        }

        public override int GetToolMode(IItemSlot slot, IPlayer byPlayer, BlockSelection blockSel)
        {
            return slot.Itemstack.Attributes.GetInt("toolMode");
        }

        public override void SetToolMode(IItemSlot slot, IPlayer byPlayer, BlockSelection blockSel, int toolMode)
        {
            slot.Itemstack.Attributes.SetInt("toolMode", toolMode);
        }

        public void Drawrotate_svg(Context cr, int x, int y, float width, float height, double[] rgba)
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
            cr.LineWidth = 15;
            cr.MiterLimit = 10;
            cr.LineCap = LineCap.Butt;
            cr.LineJoin = LineJoin.Miter;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(119.050781, 45.898438);
            cr.CurveTo(126.949219, 64.101563, 124.148438, 86.101563, 110.25, 102);
            cr.CurveTo(90.949219, 124.199219, 57.25, 126.398438, 35.148438, 107.101563);
            cr.CurveTo(19.648438, 93.601563, 13.851563, 73.101563, 18.449219, 54.398438);
            cr.CurveTo(20.449219, 46.398438, 24.25, 38.699219, 30.050781, 32);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            matrix = new Matrix(1, 0, 0, 1, 232.15, -324);
            pattern.Matrix = matrix;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(105.148438, 26.898438);
            cr.CurveTo(108.148438, 38.300781, 110.25, 53.601563, 109.050781, 64.898438);
            cr.LineTo(120.449219, 48.800781);
            cr.LineTo(139.449219, 43.699219);
            cr.CurveTo(128.449219, 40.898438, 114.851563, 33.601563, 105.148438, 26.898438);
            cr.ClosePath();
            cr.MoveTo(105.148438, 26.898438);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            cr.FillRule = FillRule.Winding;
            cr.FillPreserve();
            if (pattern != null) pattern.Dispose();

            cr.Restore();
        }

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
