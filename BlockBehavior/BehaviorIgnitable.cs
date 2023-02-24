using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public enum EnumIgniteState
    {
        NotIgnitable,
        NotIgnitablePreventDefault,
        Ignitable,
        IgniteNow
    }

    public interface IIgnitable
    {
        /// <summary>
        /// Called while the given entity attempts to ignite this block
        /// </summary>
        /// <param name="byEntity"></param>
        /// <param name="pos"></param>
        /// <param name="secondsIgniting"></param>
        /// <returns>true when this block is ignitable</returns>
        EnumIgniteState OnTryIgniteBlock(EntityAgent byEntity, BlockPos pos, float secondsIgniting);

        /// <summary>
        /// Called after the given entity has attempted to ignite this block
        /// </summary>
        /// <param name="byEntity"></param>
        /// <param name="pos"></param>
        /// <param name="secondsIgniting"></param>
        /// <param name="handling"></param>
        void OnTryIgniteBlockOver(EntityAgent byEntity, BlockPos pos, float secondsIgniting, ref EnumHandling handling);
    }


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
