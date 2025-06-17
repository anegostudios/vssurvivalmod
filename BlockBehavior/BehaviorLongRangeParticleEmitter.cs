using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

#nullable disable

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
