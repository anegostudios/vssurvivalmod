using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockAnvilPart : Block
    {
        

        public override Cuboidf[] GetCollisionBoxes(IBlockAccessor world, BlockPos pos)
        {
            BlockEntityAnvilPart beap = world.GetBlockEntity(pos) as BlockEntityAnvilPart;
            if (beap?.Inventory != null && beap.Inventory[2].Empty) return new Cuboidf[] { CollisionBoxes[0] };

            return base.GetCollisionBoxes(world, pos);
        }

        public override Cuboidf[] GetSelectionBoxes(IBlockAccessor world, BlockPos pos)
        {
            BlockEntityAnvilPart beap = world.GetBlockEntity(pos) as BlockEntityAnvilPart;
            if (beap?.Inventory != null && beap.Inventory[2].Empty) return new Cuboidf[] { CollisionBoxes[0] };

            return base.GetSelectionBoxes(world, pos);
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            BlockEntityAnvilPart beap = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityAnvilPart;
            beap?.OnInteract(byPlayer);
            return true;
        }

    }
}
