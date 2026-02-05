using System;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent
{
    public class BlockSnowLayer : BlockLayered
    {
        int height;
        bool canMelt;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            height = Variant["height"].ToInt();
            snowLevel = height;

            notSnowCovered = api.World.GetBlock(0);
            snowCovered1 = api.World.GetBlock(CodeWithVariant("height", "1"));
            snowCovered2 = api.World.GetBlock(CodeWithVariant("height", "2"));
            snowCovered3 = api.World.GetBlock(CodeWithVariant("height", "3"));

            canMelt = api.World.Config.GetBool("snowAccum", true);
        }

        public override bool ShouldReceiveServerGameTicks(IWorldAccessor world, BlockPos pos, Random offThreadRandom, out object extra)
        {
            extra = null;
            if (!canMelt) return false;
            ClimateCondition conds = world.BlockAccessor.GetClimateAt(pos, EnumGetClimateMode.NowValues);
            return conds != null && offThreadRandom.NextDouble() < GameMath.Clamp((conds.Temperature - 0.5f) / (15f - 10f * conds.Rainfall), 0, 1);
        }

        public override void OnServerGameTick(IWorldAccessor world, BlockPos pos, object extra = null)
        {
            if (height == 1)
            {
                world.BlockAccessor.SetBlock(0, pos);
                return;
            }

            Block block = world.GetBlock(CodeWithVariant("height", ""+(height - 1)));
            world.BlockAccessor.SetBlock(block.Id, pos);
        }

        public override Block GetSnowCoveredVariant(BlockPos pos, float snowLevel)
        {
            if (snowLevel == 0) return api.World.Blocks[0];
            if ((int)snowLevel < 8)
            {
                return api.World.GetBlock(CodeWithVariant("height", ""+(int)snowLevel));
            }
            if ((int)snowLevel >= 8) return api.World.GetBlock("snowblock");

            return null;
        }



        public override bool CanAcceptFallOnto(IWorldAccessor world, BlockPos pos, Block fallingBlock, TreeAttribute blockEntityAttributes)
        {
            if (fallingBlock is BlockSnowLayer || fallingBlock is BlockSnow)
            {
                BlockSnowLayer ourBlock = world.BlockAccessor.GetMostSolidBlock(pos) as BlockSnowLayer;
                return ourBlock != null && ourBlock.height < 8;
            }

            return false;
        }

        public override bool OnFallOnto(IWorldAccessor world, BlockPos pos, Block block, TreeAttribute blockEntityAttributes)
        {
            var selfHeight = height;
            var fallingBlockHeight = block.GetSnowLevel(pos);

            var nBlock = world.BlockAccessor.GetMostSolidBlock(pos);
            var uBlock = block;
            if (uBlock != null && nBlock?.GetSnowLevel(pos) < 8)
            {
                while (nBlock.GetSnowLevel(pos) < 8 && uBlock != null)
                {
                    nBlock = nBlock.GetSnowCoveredVariant(pos, nBlock.GetSnowLevel(pos) + 1);
                    uBlock = uBlock.GetSnowCoveredVariant(pos, uBlock.GetSnowLevel(pos) - 1);
                }

                int downId = 0;
                while (downId == 0)
                {
                    downId = world.BlockAccessor.GetMostSolidBlock(pos.Down()).BlockId;
                    if (downId != 0) pos.Up();
                }

                world.BlockAccessor.SetBlock(nBlock.BlockId, pos);

                if (uBlock != null)
                {
                    BlockPos upos = pos.UpCopy();
                    Block aboveBlock = world.BlockAccessor.GetMostSolidBlock(upos);
                    if (aboveBlock.BlockId == 0) world.BlockAccessor.SetBlock(uBlock.BlockId, upos);
                    else aboveBlock.OnFallOnto(world, pos, uBlock, blockEntityAttributes);
                }

                world.BlockAccessor.TriggerNeighbourBlockUpdate(pos);

                return true;
            }

            return base.OnFallOnto(world, pos, block, blockEntityAttributes);
        }

    }
}
