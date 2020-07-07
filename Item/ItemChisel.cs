using Cairo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public enum EnumChiselMode
    {
        Size1 = 0,
        Size2 = 1,
        Size4 = 2,
        Size8 = 3,
        Rotate = 4,
        Flip = 5,
        Rename = 6
    }

    /// <summary>
    /// When right clicked on a block, this chisel tool will exchange given block into a chiseledblock which 
    /// takes on the model of the block the player interacted with in the first place, but with each voxel being selectable and removable
    /// </summary>
    public class ItemChisel : Item
    {
        SkillItem[] toolModes;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            if (api is ICoreClientAPI capi)
            {
                toolModes = ObjectCacheUtil.GetOrCreate(api, "chiselToolModes", () =>
                {
                    SkillItem[] modes = new SkillItem[7];

                    modes[0] = new SkillItem() { Code = new AssetLocation("1size"), Name = Lang.Get("1x1x1") }.WithIcon(capi, ItemClay.Drawcreate1_svg);
                    modes[1] = new SkillItem() { Code = new AssetLocation("2size"), Name = Lang.Get("2x2x2") }.WithIcon(capi, ItemClay.Drawcreate4_svg);
                    modes[2] = new SkillItem() { Code = new AssetLocation("4size"), Name = Lang.Get("4x4x4") }.WithIcon(capi, Drawcreate16_svg);
                    modes[3] = new SkillItem() { Code = new AssetLocation("8size"), Name = Lang.Get("8x8x8") }.WithIcon(capi, Drawcreate64_svg);
                    modes[4] = new SkillItem() { Code = new AssetLocation("rotate"), Name = Lang.Get("Rotate") }.WithIcon(capi, Drawrotate_svg);
                    modes[5] = new SkillItem() { Code = new AssetLocation("flip"), Name = Lang.Get("Flip") }.WithIcon(capi, capi.Gui.Icons.Drawrepeat_svg);
                    modes[6] = new SkillItem() { Code = new AssetLocation("rename"), Name = Lang.Get("Set name") }.WithIcon(capi, Drawedit_svg);

                    return modes;
                });
            }
        }


        public override void OnUnloaded(ICoreAPI api)
        {
            for (int i = 0; toolModes != null && i < toolModes.Length; i++)
            {
                toolModes[i]?.Dispose();
            }
        }

        public override void OnHeldAttackStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandHandling handling)
        {
            IPlayer byPlayer = (byEntity as EntityPlayer)?.Player;

            if (blockSel?.Position == null) return;
            Block block = byEntity.World.BlockAccessor.GetBlock(blockSel.Position);

            if (api.ModLoader.GetModSystem<ModSystemBlockReinforcement>()?.IsReinforced(blockSel.Position) == true)
            {
                byPlayer.InventoryManager.ActiveHotbarSlot.MarkDirty();
                return;
            }

            if (!byEntity.World.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak))
            {
                byPlayer.InventoryManager.ActiveHotbarSlot.MarkDirty();
                return;
            }

            if (!IsChiselingAllowedFor(block, byPlayer))
            {
                base.OnHeldAttackStart(slot, byEntity, blockSel, entitySel, ref handling);
                return;
            }

            if (blockSel == null)
            {
                base.OnHeldAttackStart(slot, byEntity, blockSel, entitySel, ref handling);
                return;
            }

            
            Block chiseledblock = byEntity.World.GetBlock(new AssetLocation("chiseledblock"));

            if (block == chiseledblock)
            {   
                OnBlockInteract(byEntity.World, byPlayer, blockSel, true, ref handling);
                return;
            }
        }

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            IPlayer byPlayer = (byEntity as EntityPlayer)?.Player;

            if (blockSel?.Position == null) return;
            Block block = byEntity.World.BlockAccessor.GetBlock(blockSel.Position);

            if (api.ModLoader.GetModSystem<ModSystemBlockReinforcement>()?.IsReinforced(blockSel.Position) == true)
            {
                byPlayer.InventoryManager.ActiveHotbarSlot.MarkDirty();
                return;
            }

            if (!byEntity.World.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak))
            {
                byPlayer.InventoryManager.ActiveHotbarSlot.MarkDirty();
                return;
            }

            if (!IsChiselingAllowedFor(block, byPlayer))
            {
                base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
                return;
            }

            if (blockSel == null)
            {
                base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
                return;
            }
            
            
            string blockName = block.GetPlacedBlockName(byEntity.World, blockSel.Position);

            Block chiseledblock = byEntity.World.GetBlock(new AssetLocation("chiseledblock"));

            if (block == chiseledblock)
            {
                OnBlockInteract(byEntity.World, byPlayer, blockSel, false, ref handling);
                return;
            }

            
            

            byEntity.World.BlockAccessor.SetBlock(chiseledblock.BlockId, blockSel.Position);

            BlockEntityChisel be = byEntity.World.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityChisel;
            if (be == null) return;

            be.WasPlaced(block, blockName);
            handling = EnumHandHandling.PreventDefaultAction;
        }


        public bool IsChiselingAllowedFor(Block block, IPlayer player)
        {
            if (block is BlockChisel) return true;

            // Never non cubic blocks or blocks that have chiseling explicitly disallowed
            if (block.DrawType != EnumDrawType.Cube && block.Attributes?["canChisel"].AsBool(false) != true) return false;
            
            // Otherwise if in creative mode, sure go ahead
            if (player?.WorldData.CurrentGameMode == EnumGameMode.Creative) return true;

            // Lastly the config depends
            ITreeAttribute worldConfig = api.World.Config;
            string mode = worldConfig.GetString("microblockChiseling");

            if (mode == "off") return false;
            if (mode == "stonewood") return block.BlockMaterial == EnumBlockMaterial.Wood || block.BlockMaterial == EnumBlockMaterial.Stone;

            return true;
        }

        public void OnBlockInteract(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, bool isBreak, ref EnumHandHandling handling)
        {
            if (!world.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak))
            {
                byPlayer.InventoryManager.ActiveHotbarSlot.MarkDirty();
                return;
            }

            BlockEntityChisel bec = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityChisel;
            if (bec != null)
            {
                bec.OnBlockInteract(byPlayer, blockSel, isBreak);
                handling = EnumHandHandling.PreventDefaultAction;
            }
        }


        public override SkillItem[] GetToolModes(ItemSlot slot, IClientPlayer forPlayer, BlockSelection blockSel)
        {
            if (blockSel == null) return null;
            Block block = forPlayer.Entity.World.BlockAccessor.GetBlock(blockSel.Position);
            return block is BlockChisel ? toolModes : null;
        }

        public override int GetToolMode(ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel)
        {
            return slot.Itemstack.Attributes.GetInt("toolMode");
        }

        public override void SetToolMode(ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel, int toolMode)
        {
            slot.Itemstack.Attributes.SetInt("toolMode", toolMode);
        }

        public void Drawrotate_svg(Context cr, int x, int y, float width, float height, double[] rgba)
        {
            Pattern pattern = null;
            Matrix matrix = cr.Matrix;

            cr.Save();
            float w = 119;
            float h = 115;
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
            cr.MoveTo(100.761719, 29.972656);
            cr.CurveTo(116.078125, 46.824219, 111.929688, 74.050781, 98.03125, 89.949219);
            cr.CurveTo(78.730469, 112.148438, 45.628906, 113.027344, 23.527344, 93.726563);
            cr.CurveTo(-13.023438, 56.238281, 17.898438, 7.355469, 61.082031, 7.5);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            matrix = new Matrix(1, 0, 0, 1, 219.348174, -337.87843);
            pattern.Matrix = matrix;
            cr.StrokePreserve();
            if (pattern != null) pattern.Dispose();

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            cr.MoveTo(81.890625, 11.0625);
            cr.CurveTo(86.824219, 21.769531, 91.550781, 36.472656, 92.332031, 47.808594);
            cr.LineTo(100.761719, 29.972656);
            cr.LineTo(118.585938, 21.652344);
            cr.CurveTo(107.269531, 20.804688, 92.609375, 15.976563, 81.890625, 11.0625);
            cr.ClosePath();
            cr.MoveTo(81.890625, 11.0625);
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            cr.FillRule = FillRule.Winding;
            cr.FillPreserve();
            if (pattern != null) pattern.Dispose();

            cr.Restore();
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
            cr.LineWidth = 6;
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
            cr.LineWidth = 6;
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
            cr.LineWidth = 6;
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
            cr.LineWidth = 6;
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
