using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockCharcoalPile : BlockLayeredSlowDig
    {
        public override bool OnFallOnto(IWorldAccessor world, BlockPos pos, Block block, TreeAttribute blockEntityAttributes)
        {
            Block nBlock = (BlockCharcoalPile)world.BlockAccessor.GetMostSolidBlock(pos);
            if (block is BlockCharcoalPile && nBlock is BlockCharcoalPile)
            {

                Block uBlock = block;
                while (((BlockCharcoalPile)nBlock).CountLayers() < 8 && uBlock != null)
                {
                    nBlock = ((BlockCharcoalPile)nBlock).GetNextLayer(world);
                    uBlock = ((BlockCharcoalPile)uBlock).GetPrevLayer(world);
                }

                world.BlockAccessor.SetBlock(nBlock.BlockId, pos);
                world.BlockAccessor.TriggerNeighbourBlockUpdate(pos);
                if (uBlock != null)
                {
                    world.BlockAccessor.SetBlock(uBlock.BlockId, pos.UpCopy());
                    world.BlockAccessor.TriggerNeighbourBlockUpdate(pos.UpCopy());
                }
                return true;
            }

            return base.OnFallOnto(world, pos, block, blockEntityAttributes);
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
