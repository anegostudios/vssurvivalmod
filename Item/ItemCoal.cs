using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class ItemCoal : ItemPileable
    {
        protected override AssetLocation PileBlockCode => new AssetLocation("coalpile");
        GuiDialogCaveArt dlg;

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            if (blockSel?.Position == null) return;

            IPlayer byPlayer = (byEntity as EntityPlayer)?.Player;
            if (!byEntity.World.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak))
            {
                return;
            }

            handling = EnumHandHandling.PreventDefault;

            if (dlg != null && dlg.IsOpened())
            {
                return;
            }

            if (byEntity.World is IClientWorldAccessor)
            {
                dlg = new GuiDialogCaveArt("", byEntity.Api as ICoreClientAPI);
                dlg.TryOpen();
            }
        }

        public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (blockSel?.Position == null) return false;

            IPlayer byPlayer = (byEntity as EntityPlayer)?.Player;
            if (!byEntity.World.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak))
            {
                return false;
            }

            IBlockAccessor blockAccessor = byEntity.World.BlockAccessor;
            Block attachingBlock = blockAccessor.GetBlock(blockSel.Position);
            if (!attachingBlock.SideSolid[blockSel.Face.Index] || attachingBlock.BlockMaterial == EnumBlockMaterial.Snow || attachingBlock.BlockMaterial == EnumBlockMaterial.Ice)
            {
                return false;
            }

            if (byEntity.World is IClientWorldAccessor)
            {

            }

            return true;
        }

        public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (blockSel?.Position == null) return;

            IPlayer byPlayer = (byEntity as EntityPlayer)?.Player;
            if (!byEntity.World.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak))
            {
                return;
            }

            IBlockAccessor blockAccessor = byEntity.World.BlockAccessor;
            Block attachingBlock = blockAccessor.GetBlock(blockSel.Position);
            if (!attachingBlock.SideSolid[blockSel.Face.Index] || attachingBlock.BlockMaterial == EnumBlockMaterial.Snow || attachingBlock.BlockMaterial == EnumBlockMaterial.Ice)
            {
                return;
            }

            DrawCaveArt(blockSel, blockAccessor, byEntity);
        }

        private void DrawCaveArt(BlockSelection blockSel, IBlockAccessor blockAccessor, EntityAgent byEntity)
        {
            IWorldChunk c = blockAccessor.GetChunkAtBlockPos(blockSel.Position);
            if (c == null) return;

            int xx = (int)(blockSel.HitPosition.X * 4);
            int yy = 3 - (int)(blockSel.HitPosition.Y * 4);
            int zz = (int)(blockSel.HitPosition.Z * 4);
            int offset = 0;
            switch (blockSel.Face.Index)
            {
                case 0:
                    offset = (3 - xx) + yy * 4;
                    break;
                case 1:
                    offset = (3 - zz) + yy * 4;
                    break;
                case 2:
                    offset = xx + yy * 4;
                    break;
                case 3:
                    offset = zz + yy * 4;
                    break;
                case 4:
                    offset = xx + zz * 4;
                    break;
                case 5:
                    offset = xx + (3 - zz) * 4;
                    break;
            }

            Block blockToPlace = blockAccessor.GetBlock(new AssetLocation("drawnart-1-6-3"));
            if (c.AddDecor(blockAccessor, blockSel.Position, blockSel.Face.Index + 6 * (1 + offset), blockToPlace))
            {
                if (byEntity.Api.Side == EnumAppSide.Server)
                {
                    c.MarkModified();
                }
                else blockAccessor.MarkBlockDirty(blockSel.Position);
            }
        }

    }
}
