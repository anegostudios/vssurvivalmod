using Vintagestory.API;
using Vintagestory.API.Common;

#nullable disable

namespace Vintagestory.GameContent
{
    /// <summary>
    /// Forces a block to only allow to be placed when the player is sneaking.
    /// Uses the code "SneakPlacing". This behavior has no properties.
    /// </summary>
    /// <example><code lang="json">
    ///"behaviors": [
	///	{
	///		"name": "SneakPlacing"
	///	}
	///]
    /// </code></example>
    [DocumentAsJson]
    public class BlockBehaviorSneakPlacing : BlockBehavior
    {
        public BlockBehaviorSneakPlacing(Block block) : base(block)
        {
        }

        public override bool CanPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref EnumHandling handling, ref string failureCode)
        {
            if (!byPlayer.Entity.Controls.ShiftKey)
            {
                handling = EnumHandling.PreventDefault;
                failureCode = "onlywhensneaking";
                return false;
            }

            return base.CanPlaceBlock(world, byPlayer, blockSel, ref handling, ref failureCode);
        }
        
    }
}
