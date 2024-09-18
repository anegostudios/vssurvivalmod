using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent;

public class BlockMetaRemainSelectable : Block, IMetaBlock
{
    public bool IsSelectable(BlockPos pos) => true;
}
public class BlockTileConnector : Block
{
    public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
    {
        var stack = base.OnPickBlock(world, pos);

        var be = world.BlockAccessor.GetBlockEntity<BETileConnector>(pos);
        if (be != null)
        {
            stack.Attributes["constraints"] = new StringAttribute(be.Constraints);
        }

        return stack;
    }

    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        GetBlockEntity<BETileConnector>(blockSel.Position)?.OnInteract(byPlayer);
        return true;
    }
}
