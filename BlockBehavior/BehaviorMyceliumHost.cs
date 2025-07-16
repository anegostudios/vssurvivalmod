using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent
{
    /// <summary>
    /// Allows a block to have mushrooms naturally spawn on it.
    /// Uses the code "MyceliumHost", and has no properties.
    /// </summary>
    /// <example><code lang="json">
    ///"behaviors": [
	///	{
	///		"name": "MyceliumHost"
	///	}
	///]
    /// </code></example>
    [DocumentAsJson]
    public class BlockBehaviorMyceliumHost : BlockBehavior
    {
        
        public BlockBehaviorMyceliumHost(Block block) : base(block)
        {
        }

        public override void OnBlockRemoved(IWorldAccessor world, BlockPos pos, ref EnumHandling handling)
        {
            world.BlockAccessor.RemoveBlockEntity(pos);
        }

    }
}
