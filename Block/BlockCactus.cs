using System;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent
{
    public class BlockCactus : BlockPlant
    {

        public override bool CanPlantStay(IBlockAccessor blockAccessor, BlockPos pos)
        {
            Block block = blockAccessor.GetBlock(pos.DownCopy());
            return block.Fertility > 0 || block is BlockCactus;
        }

        // - Grows up to 3 blocks tall
        // - Each broken segment can be planted (but not placed on top of)
        // - Slowly regrows
        // - Rarely a flower on the 4th block

        Random rand = new Random();
        Block[][] blocksByHeight;
        Block topFlowering;
        Block topRipe;

        public override bool TryPlaceBlockForWorldGen(IBlockAccessor blockAccessor, BlockPos pos, BlockFacing onBlockFace, IRandom worldGenRand, BlockPatchAttributes attributes = null)
        {
            if (!CanPlantStay(blockAccessor, pos)) return false;

            if (blocksByHeight == null)
            {
                topFlowering = blockAccessor.GetBlock(CodeWithParts("topflowering"));
                topRipe = blockAccessor.GetBlock(CodeWithParts("topripe"));

                blocksByHeight = new Block[][]
                {
                    new Block[]
                    {
                        blockAccessor.GetBlock(CodeWithParts("topempty"))
                    },
                    new Block[]
                    {
                        blockAccessor.GetBlock(CodeWithParts("segment")),
                        blockAccessor.GetBlock(CodeWithParts("topempty"))

                    },
                    new Block[]
                    {
                        blockAccessor.GetBlock(CodeWithParts("segment")),
                        blockAccessor.GetBlock(CodeWithParts("branchysegment")),
                        blockAccessor.GetBlock(CodeWithParts("topempty"))
                    }
                };
            }



            int height = rand.Next(3);

            Block[] blocks = blocksByHeight[height];

            if (blocks.Length == 3)
            {
                if (rand.Next(6) == 0) blocks[2] = topRipe;
                else if (rand.Next(10) == 0) blocks[2] = topFlowering;
                else blocks[2] = blocksByHeight[0][0];
            }

            for (int i = 0; i < blocks.Length; i++)
            {
                if (blockAccessor.GetBlock(pos).IsReplacableBy(blocks[i]))
                {
                    blockAccessor.SetBlock(blocks[i].BlockId, pos);
                    pos = pos.Up();
                } else
                {
                    break;
                }
            }



            return true;
        }

    }
}
