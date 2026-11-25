using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

#nullable disable

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


        /// <summary>
        /// Called while the given entity holds an item over this block.
        /// </summary>
        /// <param name="byEntity"></param>
        /// <param name="pos"></param>
        /// <param name="secondsIgniting"></param>
        /// <returns>true when this block is ignitable</returns>
        EnumIgniteState OnTryIgniteStack(EntityAgent byEntity, BlockPos pos, ItemSlot slot, float secondsIgniting);
    }

    /// <summary>
    /// Used to alternate a block between "lit" and "extinct" variant states when ignited or extinguished.
    /// Uses the "Ignitable" code. This behavior has no properties.
    /// </summary>
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
