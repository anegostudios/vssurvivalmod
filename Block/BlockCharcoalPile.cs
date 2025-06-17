﻿using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent
{
    public class BlockCharcoalPile : BlockLayeredSlowDig
    {
        public override bool CanAcceptFallOnto(IWorldAccessor world, BlockPos pos, Block fallingBlock, TreeAttribute blockEntityAttributes)
        {
            if (fallingBlock is BlockCharcoalPile)
            {
                BlockCharcoalPile ourBlock = world.BlockAccessor.GetMostSolidBlock(pos) as BlockCharcoalPile;
                return ourBlock != null && ourBlock.CountLayers() < 8;
            }

            return false;
        }

        public override bool OnFallOnto(IWorldAccessor world, BlockPos pos, Block block, TreeAttribute blockEntityAttributes)
        {
            BlockCharcoalPile nBlock = world.BlockAccessor.GetMostSolidBlock(pos) as BlockCharcoalPile;
            BlockCharcoalPile uBlock = block as BlockCharcoalPile;
            if (uBlock != null && nBlock?.CountLayers() < 8)
            {
                while (nBlock.CountLayers() < 8 && uBlock != null)
                {
                    nBlock = nBlock.GetNextLayer(world) as BlockCharcoalPile;
                    uBlock = uBlock.GetPrevLayer(world) as BlockCharcoalPile;
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

        public override void OnNeighbourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos)
        {
            BlockPos upos = pos.UpCopy();
            world.BlockAccessor.GetBlock(upos).OnNeighbourBlockChange(world, upos, pos.Copy());

            base.OnNeighbourBlockChange(world, pos, neibpos);
        }

        public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1f)
        {
            Block block = world.BlockAccessor.GetBlock(pos);

            BlockPos abovePos = pos.UpCopy();
            Block aboveBlock = null;

            while (abovePos.Y < world.BlockAccessor.MapSizeY)
            {
                aboveBlock = world.BlockAccessor.GetBlock(abovePos);
                if (aboveBlock.FirstCodePart() != block.FirstCodePart()) break;

                abovePos.Up();
            }

            if (abovePos == pos.UpCopy() || aboveBlock == null || byPlayer?.WorldData.CurrentGameMode == EnumGameMode.Creative)
            {
                base.OnBlockBroken(world, pos, byPlayer);
                world.BlockAccessor.TriggerNeighbourBlockUpdate(pos);
                return;
            }
            else if (aboveBlock.FirstCodePart() == block.FirstCodePart()) aboveBlock.OnBlockBroken(world, abovePos, byPlayer);
            else
            {
                BlockPos topPos = abovePos.DownCopy();
                Block topBlock = world.BlockAccessor.GetBlock(topPos);
                topBlock.OnBlockBroken(world, topPos, byPlayer);
            }
        }

        public override float RandomSoundPitch(IWorldAccessor world)
        {
            return (float)world.Rand.NextDouble() * 0.24f + 0.88f;
        }
    }
}
