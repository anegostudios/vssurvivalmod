using Vintagestory.API;
using Vintagestory.API.Common;

#nullable disable

namespace Vintagestory.GameContent
{
    /// <summary>
    /// Disallows standard placement for this block. 
    /// If a previous listed behavior allows a special placement method (see <see cref="CollectibleBehaviorGroundStorable"/>), then that method will be allowed.
    /// Uses the "unplaceable" code. This behavior has no properties.
    /// </summary>
    /// <example><code lang="json">
    ///"behaviors": [
	///	{
	///		"name": "Unplaceable"
	///	}
	///]
    /// </code></example>
    [DocumentAsJson]
    public class BlockBehaviorUnplaceable : BlockBehavior
    {
        public BlockBehaviorUnplaceable(Block block) : base(block)
        {
        }

        public override bool CanPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref EnumHandling handling, ref string failureCode)
        {
            handling = EnumHandling.PreventSubsequent;
            failureCode = "__ignore__";
            return false;
        }
    }


    /// <summary>
    /// Disallows standard placement for this block on up faces only. 
    /// If a previous listed behavior allows a special placement method (see <see cref="CollectibleBehaviorGroundStorable"/>), then that method will be allowed.
    /// Uses the "unplaceable" code. This behavior has no properties.
    /// </summary>
    /// <example><code lang="json">
    ///"behaviors": [
	///	{
	///		"name": "GroundUnplaceable"
	///	}
	///]
    /// </code></example>
    [DocumentAsJson]
    public class BlockBehaviorGroundUnplaceable : BlockBehavior
    {
        public BlockBehaviorGroundUnplaceable(Block block) : base(block)
        {
        }

        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref EnumHandling handling, ref string failureCode)
        {
            if (blockSel.Face == API.MathTools.BlockFacing.UP)
            {
                handling = EnumHandling.PreventSubsequent;
                failureCode = "__ignore__";
                return false;
            }

            return base.TryPlaceBlock(world, byPlayer, itemstack, blockSel, ref handling, ref failureCode);
        }

        public override bool CanPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref EnumHandling handling, ref string failureCode)
        {
            if (blockSel.Face == API.MathTools.BlockFacing.UP)
            {
                handling = EnumHandling.PreventSubsequent;
                failureCode = "__ignore__";
                return false;
            }

            return true;
        }
    }
}
