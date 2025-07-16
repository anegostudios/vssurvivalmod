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
}
