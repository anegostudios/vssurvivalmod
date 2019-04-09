using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockCrystal : Block
    {
        Block[] FacingBlocks;
        Random rand;

        public override void OnLoaded(ICoreAPI api)
        {
            rand = new Random(api.World.Seed + 131);

            FacingBlocks = new Block[6];
            for (int i = 0; i < 6; i++)
            {
                FacingBlocks[i] = api.World.GetBlock(CodeWithPart(BlockFacing.ALLFACES[i].Code, 2));
            }
        }

        public Block FacingCrystal(IBlockAccessor blockAccessor, BlockFacing facing)
        {
            return blockAccessor.GetBlock(CodeWithPart(facing.Code));
        }



        public override bool TryPlaceBlockForWorldGen(IBlockAccessor blockAccessor, BlockPos pos, BlockFacing onBlockFace)
        {
            return false;
            Block block = blockAccessor.GetBlock(pos);
            if (block.LastCodePart(1) == "quartz" && blockAccessor.GetBlock(pos.X, pos.Y + 1, pos.Z).LastCodePart(1) == "quartz")
            {
                GenerateGeode(block, blockAccessor, pos);
            }            
            return true;
        }

        private void GenerateGeode(Block quartzblock, IBlockAccessor blockAccessor, BlockPos pos)
        {
            float rx = 3;
            float ry = 3;
            float rz = 3;

            float xRadSq = rx * rx;
            float yRadSq = ry * ry;
            float zRadSq = rz * rz;

            int cnt = 0;

            for (float dx = -rx; dx <= rx; dx++)
            {
                float xdistrel = (dx * dx) / xRadSq;

                for (float dy = -ry; dy <= ry; dy++)
                {
                    float ydistrel = (dy * dy) / yRadSq;

                    for (float dz = -rz; dz <= rz; dz++)
                    {
                        float zdistrel = (dz * dz) / zRadSq;

                        if (xdistrel + ydistrel + zdistrel > 1) continue;

                        blockAccessor.SetBlock(0, pos);
                        cnt++;
                    }
                }
            }

            

            for (float dx = -rx; dx < rx; dx++)
            {
                float xdist = (dx * dx) / (rx * rx);

                for (float dy = -ry; dy < ry; dy++)
                {
                    float ydist = (dy * dy) / (ry * ry);

                    for (float dz = -rz; dz < rz; dz++)
                    {
                        float zdist = (dz * dz) / (rz * rz);
                        if (xdist + ydist + zdist > 1) continue;

                        TryPlaceInsideGeode(blockAccessor, pos);
                    }
                }
            }
        }

        void TryPlaceInsideGeode(IBlockAccessor blockAccessor, BlockPos pos)
        {
            for (int i = 0; i < 6; i++)
            {
                BlockFacing facing = BlockFacing.ALLFACES[5 - i];

                if (blockAccessor.GetBlock(pos.UpCopy()).SideSolid[facing.Index])
                {
                    blockAccessor.SetBlock(FacingBlocks[facing.Index].BlockId, pos);
                    return;
                }
            }
        }


    }
}
