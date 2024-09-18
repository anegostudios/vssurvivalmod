using Vintagestory.API.Client;
using Vintagestory.API.Client.Tesselation;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockDeadCrop : Block, IDrawYAdjustable
    {

        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
        {
            var be = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityDeadCrop;
            if (be != null) return be.GetDrops(byPlayer, dropQuantityMultiplier);

            return base.GetDrops(world, pos, byPlayer, dropQuantityMultiplier);
        }

        public override string GetPlacedBlockName(IWorldAccessor world, BlockPos pos)
        {
            var be = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityDeadCrop;
            if (be != null) return be.GetPlacedBlockName();

            return base.GetPlacedBlockName(world, pos);
        }

        public float AdjustYPosition(BlockPos pos, Block[] chunkExtBlocks, int extIndex3d)
        {
            Block nblock = chunkExtBlocks[extIndex3d + TileSideEnum.MoveIndex[TileSideEnum.Down]];
            return nblock is BlockFarmland ? -0.0625f : 0f;
        }

        public override int GetColorWithoutTint(ICoreClientAPI capi, BlockPos pos)
        {
            return ColorUtil.ColorFromRgba(90, 84, 67, 255); //base.GetColorWithoutTint(capi, pos);
        }
    }
}
