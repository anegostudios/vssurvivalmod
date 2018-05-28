using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockBehaviorIgniteable : BlockBehavior
    {
        public BlockBehaviorIgniteable(Block block) : base(block)
        {
        }

        public virtual void Ignite(IWorldAccessor world, BlockPos pos)
        {
            if (block.LastCodePart() == "lit") return;
            Block litblock = world.GetBlock(block.CodeWithParts("lit"));
            if (litblock == null) return;

            world.BlockAccessor.ExchangeBlock(litblock.BlockId, pos);
           // world.Logger.Notification("light");
        }


        public void Extinguish(IWorldAccessor world, BlockPos pos)
        {
            if (block.LastCodePart() == "extinct") return;
            Block litblock = world.GetBlock(block.CodeWithParts("extinct"));
            if (litblock == null) return;

            world.BlockAccessor.ExchangeBlock(litblock.BlockId, pos);
            //world.Logger.Notification("exti");
        }
    }
}
