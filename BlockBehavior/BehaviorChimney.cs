using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public interface ISmokeEmitter
    {
        bool EmitsSmoke(BlockPos pos);
    }

    public class BlockBehaviorChimney : BlockBehavior
    {
        public BlockBehaviorChimney(Block block) : base(block)
        {
        }

        public override bool ShouldReceiveClientParticleTicks(IWorldAccessor world, IPlayer byPlayer, BlockPos pos, ref EnumHandling handling)
        {
            handling = EnumHandling.PreventDefault;
            BlockPos tmpPos = new BlockPos();
            for (int i = 1; i <= 5; i++)
            {
                tmpPos.Set(pos.X, pos.Y - i, pos.Z);
                var block = world.BlockAccessor.GetBlock(tmpPos);
                if (block.Id == 0) continue;

                if (block.SideIsSolid(tmpPos, BlockFacing.UP.Index)) return false;

                var ise = block.GetInterface<ISmokeEmitter>(world, pos);
                if (ise != null)
                {
                    return ise.EmitsSmoke(tmpPos);
                }
            }

            return false;
        }
    }
}
