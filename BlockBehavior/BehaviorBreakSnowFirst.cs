using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent
{
    /// <summary>
    /// A simple block behavior that breaks a snow cover first, before breaking the block.
    /// Uses the code "BreakSnowFirst". This behavior has no properties.
    /// </summary>
    /// <example><code lang="json">
    ///"behaviorsByType": {
	///	"*-snow": [ { "name": "BreakSnowFirst" } ]
	///}
    /// </code></example>
    [DocumentAsJson]
    public class BlockBehaviorBreakSnowFirst : BlockBehavior
    {
        public BlockBehaviorBreakSnowFirst(Block block) : base(block)
        {
        }

        public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, ref EnumHandling handled)
        {
            // Simply clear the snow if breaking a snowy block
            if (block.snowLevel > 0 && block.notSnowCovered != null)
            {
                if (byPlayer != null && byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative)
                {
                    world.BlockAccessor.SetBlock(block.notSnowCovered.Id, pos);
                    if (world.Side == EnumAppSide.Server) world.PlaySoundAt(new AssetLocation("block/snow"), pos, 0.5, byPlayer);
                    handled = EnumHandling.PreventSubsequent;
                    return;
                }
            }
        }

    }
}
