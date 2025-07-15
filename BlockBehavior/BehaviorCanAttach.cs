using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent
{
    /// <summary>
    /// Allows some other blocks, such as torches, to be attached onto specific sides of this block.
    /// Uses the code "CanAttach".
    /// </summary>
    /// <example>
    /// <code lang="json">
    ///"behaviors": [
	///	{
	///		"name": "CanAttach",
	///		"properties": { "sides": [ "up", "down" ] }
	///	}
	///]
    /// </code></example>
    [DocumentAsJson]
    public class BlockBehaviorCanAttach : BlockBehavior
    {
        /// <summary>
        /// The specified sides of this block that a block can be attached onto. Valid options are up, down, north, east, south, west.
        /// Non-specified sides will use default 'CanAttachBlockAt' logic.
        /// </summary>
        [DocumentAsJson("Required")]
        string[] sides;

        public BlockBehaviorCanAttach(Block block) : base(block)
        {
        }

        public override void Initialize(JsonObject properties)
        {
            sides = properties["sides"].AsArray<string>(System.Array.Empty<string>());

            base.Initialize(properties);
        }

        public override bool CanAttachBlockAt(IBlockAccessor world, Block block, BlockPos pos, BlockFacing blockFace, ref EnumHandling handling, Cuboidi attachmentArea = null)
        {
            if (sides.Contains(blockFace.Code))
            {
                handling = EnumHandling.PreventDefault;
                return true;
            }

            handling = EnumHandling.PassThrough;
            return false;
        }
    }
}
