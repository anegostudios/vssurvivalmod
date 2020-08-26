using System;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockLakeIce : Block
    {
        public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
        {
            base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
            Block waterBlock = world.GetBlock(new AssetLocation("water-still-7"));
            world.BlockAccessor.SetBlock(waterBlock.Id, pos);
            world.BlockAccessor.MarkBlockDirty(pos);
        }

        public override bool ShouldReceiveServerGameTicks(IWorldAccessor world, BlockPos pos, Random offThreadRandom, out object extra)
        {
            extra = null;
            ClimateCondition conds = world.BlockAccessor.GetClimateAt(pos, EnumGetClimateMode.NowValues);

            float chance = GameMath.Clamp((conds.Temperature - 0.5f) / 15f, 0, 1);
            return offThreadRandom.NextDouble() < chance;
        }

        public override void OnServerGameTick(IWorldAccessor world, BlockPos pos, object extra = null)
        {
            Block waterBlock = world.GetBlock(new AssetLocation("water-still-7"));
            world.BlockAccessor.SetBlock(waterBlock.Id, pos);
        }
    }
}
