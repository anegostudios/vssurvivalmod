using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    /// <summary>
    /// Forces the Block to drop as an item when surrounded by air blocks. It will override drops returned by the Block
    /// when this happens.
    /// </summary>
    public class BlockBehaviorBreakIfFloating : BlockBehavior
    {
        public BlockBehaviorBreakIfFloating(Block block) : base(block)
        {
        }

        public override void OnNeighourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos, ref EnumHandling handled)
        {
            handled = EnumHandling.NotHandled;

            if (IsSurroundedByNonSolid(world, pos))
            {
                world.BlockAccessor.BreakBlock(pos, null);
            }
            base.OnNeighourBlockChange(world, pos, neibpos, ref handled);
        }

        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier, ref EnumHandling handled)
        {
            if(IsSurroundedByNonSolid(world, pos))
            {
                handled = EnumHandling.Last;
                return new ItemStack[] { new ItemStack(block) };
            }
            else
            {
                handled = EnumHandling.NotHandled;
                return null;
            }
        }

        public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, ref EnumHandling handling)
        {
            handling = EnumHandling.NotHandled;
            if (IsSurroundedByNonSolid(world, pos) && byPlayer != null && byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative)
            {
                handling = EnumHandling.Last;
            }
        }

        private bool IsSurroundedByNonSolid(IWorldAccessor world, BlockPos pos)
        {
            foreach(BlockFacing facing in BlockFacing.ALLFACES)
            {
                BlockPos neighborPos = pos.AddCopy(facing.Normali);
                Block neighborBlock = world.BlockAccessor.GetBlock(neighborPos);

                if (neighborBlock.SideSolid[facing.GetOpposite().Index]) return false;
            }
            return true;
        }
    }
}
