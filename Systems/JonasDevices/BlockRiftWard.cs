using Vintagestory.API.Common;

namespace Vintagestory.GameContent
{
    public class BlockRiftWard : Block
    {
        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            var be = GetBlockEntity<BlockEntityRiftWard>(blockSel);
            if (be != null && be.OnInteract(blockSel, byPlayer))
            {
                return true;
            }

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }
    }
}
