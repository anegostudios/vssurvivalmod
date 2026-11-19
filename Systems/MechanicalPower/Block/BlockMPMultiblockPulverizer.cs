using Vintagestory.API.Common;

namespace Vintagestory.GameContent.Mechanics
{
    /// <summary>
    /// An invisible "fake" block used to fill the space taken by the pulverizer - provides collision boxes and prevents other blocks from being placed here
    /// </summary>
    public class BlockMPMultiblockPulverizer : BlockMPMultiblockBase
    {
        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            var principalPos = world.BlockAccessor.GetBlockEntity<BEMPMultiblock>(blockSel.Position)?.Principal;
            if (principalPos == null) return base.OnBlockInteractStart(world, byPlayer, blockSel);
            return world.BlockAccessor.GetBlockEntity<BEPulverizer>(principalPos)?.OnInteract(byPlayer, blockSel)
                ?? base.OnBlockInteractStart(world, byPlayer, blockSel);
        }
    }
}
