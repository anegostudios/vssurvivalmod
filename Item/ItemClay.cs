using System;
using System.Text;
using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class ItemClay : Item
    {
        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandHandling handling)
        {
            if (blockSel == null) return;

            BlockEntityClayForm bec = byEntity.World.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityClayForm;
            if (bec != null && bec.BaseMaterial.Collectible.LastCodePart() != LastCodePart()) return;

            if (byEntity.Controls.Sneak)
            {
                IPlayer player = byEntity.World.PlayerByUid((byEntity as EntityPlayer)?.PlayerUID);

                if (!byEntity.World.Claims.TryAccess(player, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak))
                {
                    slot.MarkDirty();
                    return;
                }
                
                if (bec != null)
                {
                    OnHeldAttackStart(slot, byEntity, blockSel, entitySel, ref handling);
                    return;
                }

                IWorldAccessor world = byEntity.World;
                Block clayformBlock = world.GetBlock(new AssetLocation("clayform"));
                if (clayformBlock == null) return;

                BlockPos belowPos = blockSel.Position.AddCopy(blockSel.Face).Down();
                Block belowBlock = world.BlockAccessor.GetBlock(belowPos);

                if (!belowBlock.CanAttachBlockAt(byEntity.World.BlockAccessor, clayformBlock, belowPos, BlockFacing.UP)) return;

                BlockPos pos = blockSel.Position.AddCopy(blockSel.Face);
                if (!world.BlockAccessor.GetBlock(pos).IsReplacableBy(clayformBlock)) return;

                world.BlockAccessor.SetBlock(clayformBlock.BlockId, pos);

                if (clayformBlock.Sounds != null) world.PlaySoundAt(clayformBlock.Sounds.Place, blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z);

                ItemStack fromitemstack = slot.Itemstack.Clone();
                bec = byEntity.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityClayForm;

                if (bec != null) bec.PutClay(slot);

                handling = EnumHandHandling.PreventDefaultAction;
                return;
            }

            OnHeldAttackStart(slot, byEntity, blockSel, entitySel, ref handling);
        }

        public override bool OnHeldInteractCancel(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, EnumItemUseCancelReason cancelReason)
        {
            return false;
        }

        public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            return false;
        }

        public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (blockSel == null) return;
            if (!(byEntity.World.BlockAccessor.GetBlock(blockSel.Position) is BlockClayForm)) return;

            BlockEntityClayForm bea = byEntity.World.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityClayForm;
            if (bea == null) return;

            IPlayer byPlayer = null;
            if (byEntity is EntityPlayer) byPlayer = byEntity.World.PlayerByUid(((EntityPlayer)byEntity).PlayerUID);
            if (byPlayer == null) return;

            if (!byEntity.World.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.Use))
            {
                return;
            }

            if (bea.AvailableVoxels <= 0)
            {
                slot.TakeOut(1);
                slot.MarkDirty();
                bea.AvailableVoxels += 25;
            }

            // The server side call is made using a custom network packet
            if (byEntity.World is IClientWorldAccessor)
            {
                bea.OnUseOver(byPlayer, blockSel.SelectionBoxIndex, blockSel.Face, false);
            }
        }


        public override void OnHeldAttackStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandHandling handling)
        {
            if (blockSel == null) return;
            if (!(byEntity.World.BlockAccessor.GetBlock(blockSel.Position) is BlockClayForm)) return;

            BlockEntityClayForm bea = byEntity.World.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityClayForm;
            if (bea == null) return;

            IPlayer byPlayer = null;
            if (byEntity is EntityPlayer) byPlayer = byEntity.World.PlayerByUid(((EntityPlayer)byEntity).PlayerUID);
            if (byPlayer == null) return;

            if (!byEntity.World.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.Use))
            {
                return;
            }

            bea.OnBeginUse(byPlayer, blockSel);
            handling = EnumHandHandling.PreventDefaultAction;
        }

        public override bool OnHeldAttackCancel(float secondsPassed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSelection, EntitySelection entitySel, EnumItemUseCancelReason cancelReason)
        {
            return false;
        }

        public override bool OnHeldAttackStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSelection, EntitySelection entitySel)
        {
            return false;
        }

        public override void OnHeldAttackStop(float secondsPassed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (blockSel == null) return;
            if (!(byEntity.World.BlockAccessor.GetBlock(blockSel.Position) is BlockClayForm)) return;

            BlockEntityClayForm bea = byEntity.World.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityClayForm;
            if (bea == null) return;

            IPlayer byPlayer = null;
            if (byEntity is EntityPlayer) byPlayer = byEntity.World.PlayerByUid(((EntityPlayer)byEntity).PlayerUID);
            if (byPlayer == null) return;

            int curMode = GetToolMode(slot, byPlayer, blockSel);

            if (!byEntity.World.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.Use))
            {
                return;
            }

            // The server side call is made using a custom network packet
            if (byEntity.World is IClientWorldAccessor)
            {
                bea.OnUseOver(byPlayer, blockSel.SelectionBoxIndex, blockSel.Face, true);
            }
        }


        public override int GetQuantityToolModes(ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (blockSel == null) return 0;
            Block block = byPlayer.Entity.World.BlockAccessor.GetBlock(blockSel.Position);
            return block is BlockClayForm ? 4 : 0;
        }

        public override void DrawToolModeIcon(ItemSlot slot, IPlayer byPlayer, BlockSelection blockSelection, Context cr, int x, int y, int width, int height, int toolMode, int color)
        {
            double[] colordoubles = ColorUtil.ToRGBADoubles(color);

            switch (toolMode)
            {
                case 0: Drawcreate1_svg(cr, x, y, width, height, colordoubles); break;
                case 1: Drawcreate4_svg(cr, x, y, width, height, colordoubles); break;
                case 2: Drawcreate9_svg(cr, x, y, width, height, colordoubles); break;
                case 3: Drawduplicate_svg(cr, x, y, width, height, colordoubles); break;
            }
        }


        #region Icons

        public static void Drawcreate1_svg(Context cr, int x, int y, float width, float height, double[] rgba)
        {
            Pattern pattern = null;
            Matrix matrix = cr.Matrix;

            cr.Save();
            float w = 129;
            float h = 129;
            float scale = Math.Min(width / w, height / h);
            matrix.Translate(x + Math.Max(0, (width - w * scale) / 2), y + Math.Max(0, (height - h * scale) / 2));
            matrix.Scale(scale, scale);
            cr.Matrix = matrix;

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(51.828125, 51.828125);
            cr.LineTo(76.828125, 51.828125);
            cr.LineTo(76.828125, 76.828125);
            cr.LineTo(51.828125, 76.828125);
            cr.ClosePath();
            cr.MoveTo(51.828125, 51.828125);
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
            cr.MoveTo(51.828125, 51.828125);
            cr.LineTo(76.828125, 51.828125);
            cr.LineTo(76.828125, 76.828125);
            cr.LineTo(51.828125, 76.828125);
            cr.ClosePath();
            cr.MoveTo(51.828125, 51.828125);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Restore();
        }

        public void Drawremove1_svg(Context cr, int x, int y, float width, float height, double[] rgba)
        {
            Pattern pattern = null;
            Matrix matrix = cr.Matrix;

            cr.Save();
            float w = 129;
            float h = 129;
            float scale = Math.Min(width / w, height / h);
            matrix.Translate(x + Math.Max(0, (width - w * scale) / 2), y + Math.Max(0, (height - h * scale) / 2));
            matrix.Scale(scale, scale);
            cr.Matrix = matrix;

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(51.828125, 51.828125);
            cr.LineTo(76.828125, 51.828125);
            cr.LineTo(76.828125, 76.828125);
            cr.LineTo(51.828125, 76.828125);
            cr.ClosePath();
            cr.MoveTo(51.828125, 51.828125);
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
            cr.MoveTo(51.828125, 51.828125);
            cr.LineTo(76.828125, 51.828125);
            cr.LineTo(76.828125, 76.828125);
            cr.LineTo(51.828125, 76.828125);
            cr.ClosePath();
            cr.MoveTo(51.828125, 51.828125);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            cr.LineWidth = 8;
            cr.MiterLimit = 10;
            cr.LineCap = LineCap.Butt;
            cr.LineJoin = LineJoin.Miter;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(91.828125, 36.828125);
            cr.LineTo(36.328125, 92.328125);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Restore();
        }

        public static void Drawcreate4_svg(Context cr, int x, int y, float width, float height, double[] rgba)
        {
            Pattern pattern = null;
            Matrix matrix = cr.Matrix;

            cr.Save();
            float w = 129;
            float h = 129;
            float scale = Math.Min(width / w, height / h);
            matrix.Translate(x + Math.Max(0, (width - w * scale) / 2), y + Math.Max(0, (height - h * scale) / 2));
            matrix.Scale(scale, scale);
            cr.Matrix = matrix;

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(34.078125, 33.828125);
            cr.LineTo(59.078125, 33.828125);
            cr.LineTo(59.078125, 58.828125);
            cr.LineTo(34.078125, 58.828125);
            cr.ClosePath();
            cr.MoveTo(34.078125, 33.828125);
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
            cr.MoveTo(34.078125, 33.828125);
            cr.LineTo(59.078125, 33.828125);
            cr.LineTo(59.078125, 58.828125);
            cr.LineTo(34.078125, 58.828125);
            cr.ClosePath();
            cr.MoveTo(34.078125, 33.828125);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(71.578125, 33.828125);
            cr.LineTo(96.578125, 33.828125);
            cr.LineTo(96.578125, 58.828125);
            cr.LineTo(71.578125, 58.828125);
            cr.ClosePath();
            cr.MoveTo(71.578125, 33.828125);
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
            cr.MoveTo(71.578125, 33.828125);
            cr.LineTo(96.578125, 33.828125);
            cr.LineTo(96.578125, 58.828125);
            cr.LineTo(71.578125, 58.828125);
            cr.ClosePath();
            cr.MoveTo(71.578125, 33.828125);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(34.078125, 71.828125);
            cr.LineTo(59.078125, 71.828125);
            cr.LineTo(59.078125, 96.828125);
            cr.LineTo(34.078125, 96.828125);
            cr.ClosePath();
            cr.MoveTo(34.078125, 71.828125);
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
            cr.MoveTo(34.078125, 71.828125);
            cr.LineTo(59.078125, 71.828125);
            cr.LineTo(59.078125, 96.828125);
            cr.LineTo(34.078125, 96.828125);
            cr.ClosePath();
            cr.MoveTo(34.078125, 71.828125);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(71.578125, 71.828125);
            cr.LineTo(96.578125, 71.828125);
            cr.LineTo(96.578125, 96.828125);
            cr.LineTo(71.578125, 96.828125);
            cr.ClosePath();
            cr.MoveTo(71.578125, 71.828125);
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
            cr.MoveTo(71.578125, 71.828125);
            cr.LineTo(96.578125, 71.828125);
            cr.LineTo(96.578125, 96.828125);
            cr.LineTo(71.578125, 96.828125);
            cr.ClosePath();
            cr.MoveTo(71.578125, 71.828125);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Restore();
        }

        public void Drawremove4_svg(Context cr, int x, int y, float width, float height, double[] rgba)
        {
            Pattern pattern = null;
            Matrix matrix = cr.Matrix;

            cr.Save();
            float w = 129;
            float h = 129;
            float scale = Math.Min(width / w, height / h);
            matrix.Translate(x + Math.Max(0, (width - w * scale) / 2), y + Math.Max(0, (height - h * scale) / 2));
            matrix.Scale(scale, scale);
            cr.Matrix = matrix;

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(34.078125, 33.828125);
            cr.LineTo(59.078125, 33.828125);
            cr.LineTo(59.078125, 58.828125);
            cr.LineTo(34.078125, 58.828125);
            cr.ClosePath();
            cr.MoveTo(34.078125, 33.828125);
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
            cr.MoveTo(34.078125, 33.828125);
            cr.LineTo(59.078125, 33.828125);
            cr.LineTo(59.078125, 58.828125);
            cr.LineTo(34.078125, 58.828125);
            cr.ClosePath();
            cr.MoveTo(34.078125, 33.828125);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(71.578125, 33.828125);
            cr.LineTo(96.578125, 33.828125);
            cr.LineTo(96.578125, 58.828125);
            cr.LineTo(71.578125, 58.828125);
            cr.ClosePath();
            cr.MoveTo(71.578125, 33.828125);
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
            cr.MoveTo(71.578125, 33.828125);
            cr.LineTo(96.578125, 33.828125);
            cr.LineTo(96.578125, 58.828125);
            cr.LineTo(71.578125, 58.828125);
            cr.ClosePath();
            cr.MoveTo(71.578125, 33.828125);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(34.078125, 71.828125);
            cr.LineTo(59.078125, 71.828125);
            cr.LineTo(59.078125, 96.828125);
            cr.LineTo(34.078125, 96.828125);
            cr.ClosePath();
            cr.MoveTo(34.078125, 71.828125);
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
            cr.MoveTo(34.078125, 71.828125);
            cr.LineTo(59.078125, 71.828125);
            cr.LineTo(59.078125, 96.828125);
            cr.LineTo(34.078125, 96.828125);
            cr.ClosePath();
            cr.MoveTo(34.078125, 71.828125);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(71.578125, 71.828125);
            cr.LineTo(96.578125, 71.828125);
            cr.LineTo(96.578125, 96.828125);
            cr.LineTo(71.578125, 96.828125);
            cr.ClosePath();
            cr.MoveTo(71.578125, 71.828125);
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
            cr.MoveTo(71.578125, 71.828125);
            cr.LineTo(96.578125, 71.828125);
            cr.LineTo(96.578125, 96.828125);
            cr.LineTo(71.578125, 96.828125);
            cr.ClosePath();
            cr.MoveTo(71.578125, 71.828125);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            cr.LineWidth = 8;
            cr.MiterLimit = 10;
            cr.LineCap = LineCap.Butt;
            cr.LineJoin = LineJoin.Miter;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(108.828125, 21.828125);
            cr.LineTo(19.328125, 111.328125);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Restore();
        }

        public void Drawcreate9_svg(Context cr, int x, int y, float width, float height, double[] rgba)
        {
            Pattern pattern = null;
            Matrix matrix = cr.Matrix;

            cr.Save();
            float w = 129;
            float h = 129;
            float scale = Math.Min(width / w, height / h);
            matrix.Translate(x + Math.Max(0, (width - w * scale) / 2), y + Math.Max(0, (height - h * scale) / 2));
            matrix.Scale(scale, scale);
            cr.Matrix = matrix;

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(15.328125, 14.828125);
            cr.LineTo(40.328125, 14.828125);
            cr.LineTo(40.328125, 39.828125);
            cr.LineTo(15.328125, 39.828125);
            cr.ClosePath();
            cr.MoveTo(15.328125, 14.828125);
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
            cr.MoveTo(15.328125, 14.828125);
            cr.LineTo(40.328125, 14.828125);
            cr.LineTo(40.328125, 39.828125);
            cr.LineTo(15.328125, 39.828125);
            cr.ClosePath();
            cr.MoveTo(15.328125, 14.828125);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(52.828125, 14.828125);
            cr.LineTo(77.828125, 14.828125);
            cr.LineTo(77.828125, 39.828125);
            cr.LineTo(52.828125, 39.828125);
            cr.ClosePath();
            cr.MoveTo(52.828125, 14.828125);
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
            cr.MoveTo(52.828125, 14.828125);
            cr.LineTo(77.828125, 14.828125);
            cr.LineTo(77.828125, 39.828125);
            cr.LineTo(52.828125, 39.828125);
            cr.ClosePath();
            cr.MoveTo(52.828125, 14.828125);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(15.328125, 52.828125);
            cr.LineTo(40.328125, 52.828125);
            cr.LineTo(40.328125, 77.828125);
            cr.LineTo(15.328125, 77.828125);
            cr.ClosePath();
            cr.MoveTo(15.328125, 52.828125);
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
            cr.MoveTo(15.328125, 52.828125);
            cr.LineTo(40.328125, 52.828125);
            cr.LineTo(40.328125, 77.828125);
            cr.LineTo(15.328125, 77.828125);
            cr.ClosePath();
            cr.MoveTo(15.328125, 52.828125);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(52.828125, 52.828125);
            cr.LineTo(77.828125, 52.828125);
            cr.LineTo(77.828125, 77.828125);
            cr.LineTo(52.828125, 77.828125);
            cr.ClosePath();
            cr.MoveTo(52.828125, 52.828125);
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
            cr.MoveTo(52.828125, 52.828125);
            cr.LineTo(77.828125, 52.828125);
            cr.LineTo(77.828125, 77.828125);
            cr.LineTo(52.828125, 77.828125);
            cr.ClosePath();
            cr.MoveTo(52.828125, 52.828125);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(90.328125, 14.828125);
            cr.LineTo(115.328125, 14.828125);
            cr.LineTo(115.328125, 39.828125);
            cr.LineTo(90.328125, 39.828125);
            cr.ClosePath();
            cr.MoveTo(90.328125, 14.828125);
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
            cr.MoveTo(90.328125, 14.828125);
            cr.LineTo(115.328125, 14.828125);
            cr.LineTo(115.328125, 39.828125);
            cr.LineTo(90.328125, 39.828125);
            cr.ClosePath();
            cr.MoveTo(90.328125, 14.828125);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(90.328125, 52.828125);
            cr.LineTo(115.328125, 52.828125);
            cr.LineTo(115.328125, 77.828125);
            cr.LineTo(90.328125, 77.828125);
            cr.ClosePath();
            cr.MoveTo(90.328125, 52.828125);
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
            cr.MoveTo(90.328125, 52.828125);
            cr.LineTo(115.328125, 52.828125);
            cr.LineTo(115.328125, 77.828125);
            cr.LineTo(90.328125, 77.828125);
            cr.ClosePath();
            cr.MoveTo(90.328125, 52.828125);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(15.328125, 91.328125);
            cr.LineTo(40.328125, 91.328125);
            cr.LineTo(40.328125, 116.328125);
            cr.LineTo(15.328125, 116.328125);
            cr.ClosePath();
            cr.MoveTo(15.328125, 91.328125);
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
            cr.MoveTo(15.328125, 91.328125);
            cr.LineTo(40.328125, 91.328125);
            cr.LineTo(40.328125, 116.328125);
            cr.LineTo(15.328125, 116.328125);
            cr.ClosePath();
            cr.MoveTo(15.328125, 91.328125);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(52.828125, 91.328125);
            cr.LineTo(77.828125, 91.328125);
            cr.LineTo(77.828125, 116.328125);
            cr.LineTo(52.828125, 116.328125);
            cr.ClosePath();
            cr.MoveTo(52.828125, 91.328125);
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
            cr.MoveTo(52.828125, 91.328125);
            cr.LineTo(77.828125, 91.328125);
            cr.LineTo(77.828125, 116.328125);
            cr.LineTo(52.828125, 116.328125);
            cr.ClosePath();
            cr.MoveTo(52.828125, 91.328125);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(90.328125, 91.328125);
            cr.LineTo(115.328125, 91.328125);
            cr.LineTo(115.328125, 116.328125);
            cr.LineTo(90.328125, 116.328125);
            cr.ClosePath();
            cr.MoveTo(90.328125, 91.328125);
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
            cr.MoveTo(90.328125, 91.328125);
            cr.LineTo(115.328125, 91.328125);
            cr.LineTo(115.328125, 116.328125);
            cr.LineTo(90.328125, 116.328125);
            cr.ClosePath();
            cr.MoveTo(90.328125, 91.328125);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Restore();
        }
        
        public void Drawremove9_svg(Context cr, int x, int y, float width, float height, double[] rgba)
        {
            Pattern pattern = null;
            Matrix matrix = cr.Matrix;

            cr.Save();
            float w = 129;
            float h = 129;
            float scale = Math.Min(width / w, height / h);
            matrix.Translate(x + Math.Max(0, (width - w * scale) / 2), y + Math.Max(0, (height - h * scale) / 2));
            matrix.Scale(scale, scale);
            cr.Matrix = matrix;

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(15.328125, 14.828125);
            cr.LineTo(40.328125, 14.828125);
            cr.LineTo(40.328125, 39.828125);
            cr.LineTo(15.328125, 39.828125);
            cr.ClosePath();
            cr.MoveTo(15.328125, 14.828125);
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
            cr.MoveTo(15.328125, 14.828125);
            cr.LineTo(40.328125, 14.828125);
            cr.LineTo(40.328125, 39.828125);
            cr.LineTo(15.328125, 39.828125);
            cr.ClosePath();
            cr.MoveTo(15.328125, 14.828125);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(52.828125, 14.828125);
            cr.LineTo(77.828125, 14.828125);
            cr.LineTo(77.828125, 39.828125);
            cr.LineTo(52.828125, 39.828125);
            cr.ClosePath();
            cr.MoveTo(52.828125, 14.828125);
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
            cr.MoveTo(52.828125, 14.828125);
            cr.LineTo(77.828125, 14.828125);
            cr.LineTo(77.828125, 39.828125);
            cr.LineTo(52.828125, 39.828125);
            cr.ClosePath();
            cr.MoveTo(52.828125, 14.828125);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(15.328125, 52.828125);
            cr.LineTo(40.328125, 52.828125);
            cr.LineTo(40.328125, 77.828125);
            cr.LineTo(15.328125, 77.828125);
            cr.ClosePath();
            cr.MoveTo(15.328125, 52.828125);
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
            cr.MoveTo(15.328125, 52.828125);
            cr.LineTo(40.328125, 52.828125);
            cr.LineTo(40.328125, 77.828125);
            cr.LineTo(15.328125, 77.828125);
            cr.ClosePath();
            cr.MoveTo(15.328125, 52.828125);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(52.828125, 52.828125);
            cr.LineTo(77.828125, 52.828125);
            cr.LineTo(77.828125, 77.828125);
            cr.LineTo(52.828125, 77.828125);
            cr.ClosePath();
            cr.MoveTo(52.828125, 52.828125);
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
            cr.MoveTo(52.828125, 52.828125);
            cr.LineTo(77.828125, 52.828125);
            cr.LineTo(77.828125, 77.828125);
            cr.LineTo(52.828125, 77.828125);
            cr.ClosePath();
            cr.MoveTo(52.828125, 52.828125);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(90.328125, 14.828125);
            cr.LineTo(115.328125, 14.828125);
            cr.LineTo(115.328125, 39.828125);
            cr.LineTo(90.328125, 39.828125);
            cr.ClosePath();
            cr.MoveTo(90.328125, 14.828125);
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
            cr.MoveTo(90.328125, 14.828125);
            cr.LineTo(115.328125, 14.828125);
            cr.LineTo(115.328125, 39.828125);
            cr.LineTo(90.328125, 39.828125);
            cr.ClosePath();
            cr.MoveTo(90.328125, 14.828125);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(90.328125, 52.828125);
            cr.LineTo(115.328125, 52.828125);
            cr.LineTo(115.328125, 77.828125);
            cr.LineTo(90.328125, 77.828125);
            cr.ClosePath();
            cr.MoveTo(90.328125, 52.828125);
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
            cr.MoveTo(90.328125, 52.828125);
            cr.LineTo(115.328125, 52.828125);
            cr.LineTo(115.328125, 77.828125);
            cr.LineTo(90.328125, 77.828125);
            cr.ClosePath();
            cr.MoveTo(90.328125, 52.828125);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(15.328125, 91.328125);
            cr.LineTo(40.328125, 91.328125);
            cr.LineTo(40.328125, 116.328125);
            cr.LineTo(15.328125, 116.328125);
            cr.ClosePath();
            cr.MoveTo(15.328125, 91.328125);
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
            cr.MoveTo(15.328125, 91.328125);
            cr.LineTo(40.328125, 91.328125);
            cr.LineTo(40.328125, 116.328125);
            cr.LineTo(15.328125, 116.328125);
            cr.ClosePath();
            cr.MoveTo(15.328125, 91.328125);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(52.828125, 91.328125);
            cr.LineTo(77.828125, 91.328125);
            cr.LineTo(77.828125, 116.328125);
            cr.LineTo(52.828125, 116.328125);
            cr.ClosePath();
            cr.MoveTo(52.828125, 91.328125);
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
            cr.MoveTo(52.828125, 91.328125);
            cr.LineTo(77.828125, 91.328125);
            cr.LineTo(77.828125, 116.328125);
            cr.LineTo(52.828125, 116.328125);
            cr.ClosePath();
            cr.MoveTo(52.828125, 91.328125);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(90.328125, 91.328125);
            cr.LineTo(115.328125, 91.328125);
            cr.LineTo(115.328125, 116.328125);
            cr.LineTo(90.328125, 116.328125);
            cr.ClosePath();
            cr.MoveTo(90.328125, 91.328125);
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
            cr.MoveTo(90.328125, 91.328125);
            cr.LineTo(115.328125, 91.328125);
            cr.LineTo(115.328125, 116.328125);
            cr.LineTo(90.328125, 116.328125);
            cr.ClosePath();
            cr.MoveTo(90.328125, 91.328125);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            cr.LineWidth = 8;
            cr.MiterLimit = 10;
            cr.LineCap = LineCap.Butt;
            cr.LineJoin = LineJoin.Miter;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(125.828125, 2.828125);
            cr.LineTo(2.828125, 125.828125);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Restore();
        }

        public void Drawduplicate_svg(Context cr, int x, int y, float width, float height, double[] rgba)
        {
            Pattern pattern = null;
            Matrix matrix = cr.Matrix;

            cr.Save();
            float w = 129;
            float h = 129;
            float scale = Math.Min(width / w, height / h);
            matrix.Translate(x + Math.Max(0, (width - w * scale) / 2), y + Math.Max(0, (height - h * scale) / 2));
            matrix.Scale(scale, scale);
            cr.Matrix = matrix;

            cr.Operator = Operator.Over;
            cr.LineWidth = 5;
            cr.MiterLimit = 10;
            cr.LineCap = LineCap.Butt;
            cr.LineJoin = LineJoin.Miter;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(71.328125, 66.042969);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            cr.LineWidth = 5;
            cr.MiterLimit = 10;
            cr.LineCap = LineCap.Butt;
            cr.LineJoin = LineJoin.Miter;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(71.328125, 46.078125);
            cr.LineTo(71.328125, 30.828125);
            cr.CurveTo(71.328125, 29.691406, 70.667969, 28.097656, 69.863281, 27.292969);
            cr.LineTo(60.363281, 17.792969);
            cr.CurveTo(59.558594, 16.988281, 57.96875, 16.328125, 56.828125, 16.328125);
            cr.LineTo(29.898438, 16.328125);
            cr.CurveTo(28.761719, 16.328125, 27.828125, 17.261719, 27.828125, 18.398438);
            cr.LineTo(27.828125, 76.398438);
            cr.CurveTo(27.828125, 77.539063, 28.761719, 78.472656, 29.898438, 78.472656);
            cr.LineTo(50.828125, 78.472656);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(58.035156, 27.6875);
            cr.CurveTo(58.035156, 28.828125, 58.96875, 29.757813, 60.109375, 29.757813);
            cr.LineTo(68.394531, 29.757813);
            cr.CurveTo(69.535156, 29.757813, 69.804688, 29.097656, 69, 28.292969);
            cr.LineTo(59.5, 18.796875);
            cr.CurveTo(58.695313, 17.988281, 58.035156, 18.261719, 58.035156, 19.402344);
            cr.ClosePath();
            cr.MoveTo(58.035156, 27.6875);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            cr.FillRule = FillRule.Winding;
            cr.FillPreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            cr.LineWidth = 5;
            cr.MiterLimit = 10;
            cr.LineCap = LineCap.Butt;
            cr.LineJoin = LineJoin.Miter;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(94.328125, 95.792969);
            cr.LineTo(94.328125, 60.578125);
            cr.CurveTo(94.328125, 59.441406, 93.667969, 57.847656, 92.863281, 57.042969);
            cr.LineTo(83.363281, 47.542969);
            cr.CurveTo(82.558594, 46.738281, 80.96875, 46.078125, 79.828125, 46.078125);
            cr.LineTo(52.898438, 46.078125);
            cr.CurveTo(51.761719, 46.078125, 50.828125, 47.011719, 50.828125, 48.148438);
            cr.LineTo(50.828125, 106.148438);
            cr.CurveTo(50.828125, 107.289063, 51.761719, 108.222656, 52.898438, 108.222656);
            cr.LineTo(92.257813, 108.222656);
            cr.CurveTo(93.398438, 108.222656, 94.328125, 107.289063, 94.328125, 106.148438);
            cr.LineTo(94.328125, 95.792969);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(81.035156, 57.4375);
            cr.CurveTo(81.035156, 58.578125, 81.96875, 59.507813, 83.109375, 59.507813);
            cr.LineTo(91.394531, 59.507813);
            cr.CurveTo(92.535156, 59.507813, 92.804688, 58.847656, 92, 58.042969);
            cr.LineTo(82.5, 48.546875);
            cr.CurveTo(81.695313, 47.738281, 81.035156, 48.011719, 81.035156, 49.152344);
            cr.ClosePath();
            cr.MoveTo(81.035156, 57.4375);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            cr.FillRule = FillRule.Winding;
            cr.FillPreserve();
            if (pattern != null) pattern.Dispose();

            cr.Restore();
        }



        #endregion




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
