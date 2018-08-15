using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    /// <summary>
    /// Spawns an EntityBlockFalling when the user places a block that has air underneath it or if a neighbor block is
    /// removed and causes air to be underneath it.
    /// </summary>
    public class BlockBehaviorUnstableFalling : BlockBehavior
    {
        public BlockBehaviorUnstableFalling(Block block) : base(block)
        {
        }

        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref EnumHandling handling)
        {
            handling = EnumHandling.NotHandled;

            if (TryFalling(world, blockSel.Position))
            {
                handling = EnumHandling.PreventDefault;
            }

            return true;
        }

        public override void OnNeighourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos, ref EnumHandling handling)
        {
            base.OnNeighourBlockChange(world, pos, neibpos, ref handling);

            TryFalling(world, pos);
        }

        private bool TryFalling(IWorldAccessor world, BlockPos pos)
        {
            if (IsReplacableBeneath(world, pos))
            {
                // Prevents duplication
                IEntity entity = world.GetNearestEntity(pos.ToVec3d().Add(0.5, 0.5, 0.5), 1, 3, (e) =>
                {
                    return e is EntityBlockFalling && ((EntityBlockFalling)e).initialPos.Equals(pos);
                }
                );

                if (entity == null)
                {
                    world.SpawnEntity(new EntityBlockFalling(block, world.BlockAccessor.GetBlockEntity(pos), pos));
                }
                
                return true;
            }

            return false;
        }

        private bool IsReplacableBeneath(IWorldAccessor world, BlockPos pos)
        {
            Block bottomBlock = world.BlockAccessor.GetBlock(pos.DownCopy());
            return (bottomBlock != null && bottomBlock.Replaceable > 6000);
        }
    }
}
