using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BehaviorMyceliumHost : BlockBehavior
    {
        

        public BehaviorMyceliumHost(Block block) : base(block)
        {
        }

        public override void OnBlockRemoved(IWorldAccessor world, BlockPos pos, ref EnumHandling handling)
        {
            world.BlockAccessor.RemoveBlockEntity(pos);
        }

    }
}
