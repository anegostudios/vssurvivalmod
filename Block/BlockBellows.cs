using Vintagestory.API.Common;

namespace Vintagestory.GameContent
{
    public class BlockBellows : Block
    {
        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            return GetBlockEntity<BlockEntityBellows>(blockSel.Position)?.Interact(byPlayer) ?? false;
        }
    }
}
