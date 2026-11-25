using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockShelf : Block
    {
        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            PlacedPriorityInteract = true;
        }

        public override bool DoPartialSelection(IWorldAccessor world, BlockPos pos)
        {
            // Shelves stay wall attached with horizontalattachable, so they cannot use RotateablePlaceable, forward these queries to BEBehaviorDisplay instead
            return world.BlockAccessor.GetBlockEntity(pos)?.GetBehavior<BEBehaviorDisplay>()?.DoPartialSelection() ?? base.DoPartialSelection(world, pos);
        }

        public override Cuboidf[] GetCollisionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            return blockAccessor.GetBlockEntity(pos)?.GetBehavior<BEBehaviorDisplay>()?.GetCollisionBoxes() ?? base.GetCollisionBoxes(blockAccessor, pos);
        }

        public override Cuboidf[] GetParticleCollisionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            return GetCollisionBoxes(blockAccessor, pos);
        }

        public override Cuboidf[] GetSelectionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            return blockAccessor.GetBlockEntity(pos)?.GetBehavior<BEBehaviorDisplay>()?.GetSelectionBoxes() ?? base.GetSelectionBoxes(blockAccessor, pos);
        }
    }
}
