using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent
{

    public class BlockBehaviorCanAttach : BlockBehavior
    {
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
