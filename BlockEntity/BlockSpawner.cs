using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockSpawner : Block
    {

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            BlockEntitySpawner be = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntitySpawner;
            if (be != null)
            {
                be.OnInteract(byPlayer);
                return true;
            }

            return false;
            
        }
    }
}
