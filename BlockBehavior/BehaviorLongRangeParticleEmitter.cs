using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockBehaviorNoParticles : BlockBehavior
    {
        public BlockBehaviorNoParticles(Block block) : base(block)
        {
        }

        public override bool ShouldReceiveClientParticleTicks(IWorldAccessor world, IPlayer byPlayer, BlockPos pos, ref EnumHandling handling)
        {
            return false;
        }
    }
}
