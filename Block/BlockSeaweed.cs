using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockSeaweed : BlockWaterPlant
    {
        Random random = new Random();
        Block[] blocks;

        public override bool CanPlantStay(IBlockAccessor blockAccessor, BlockPos pos)
        {
            Block block = blockAccessor.GetBlock(pos.X, pos.Y - 1, pos.Z);
            return (block.Fertility > 0) || (block is BlockSeaweed && block.Variant["part"] == "section");
        }

        public override void OnJsonTesselation(ref MeshData sourceMesh, ref int[] lightRgbsByCorner, BlockPos pos, Block[] chunkExtBlocks, int extIndex3d)
        {
            int windData =
                ((api.World.BlockAccessor.GetBlock(pos.X, pos.Y - 1, pos.Z) is BlockSeaweed) ? 1 : 0)
                + ((api.World.BlockAccessor.GetBlock(pos.X, pos.Y - 2, pos.Z) is BlockSeaweed) ? 1 : 0)
                + ((api.World.BlockAccessor.GetBlock(pos.X, pos.Y - 3, pos.Z) is BlockSeaweed) ? 1 : 0)
            ;

            for (int i = 0; i < sourceMesh.FlagsCount; i++)
            {
                sourceMesh.Flags[i] = (sourceMesh.Flags[i] & VertexFlags.ClearWindDataBitsMask) | windData;
            }
        }

        public override bool TryPlaceBlockForWorldGen(IBlockAccessor blockAccessor, BlockPos pos, BlockFacing onBlockFace, LCGRandom worldGenRand)
        {
            BlockPos belowPos = pos.DownCopy();

            Block block = blockAccessor.GetBlock(belowPos);
            if (block.LiquidCode != "water") return false;

            int depth = 1;
            while (depth < 10)
            {
                belowPos.Down();
                block = blockAccessor.GetBlock(belowPos);

                if (block.Fertility > 0)
                {
                    belowPos.Up();
                    PlaceSeaweed(blockAccessor, belowPos, depth);
                    return true;
                } else
                {
                    if (!block.IsLiquid()) return false;
                }

                depth++;
            }

            return false;

        }


        private void PlaceSeaweed(IBlockAccessor blockAccessor, BlockPos pos, int depth)
        {
            int height = Math.Min(depth-1,  1 + random.Next(3) + random.Next(3));

            if (blocks == null)
            {
                blocks = new Block[]
                {
                    blockAccessor.GetBlock(CodeWithParts("section")),
                    blockAccessor.GetBlock(CodeWithParts("top")),
                };
            }

            while (height-- > 0)
            {
                blockAccessor.SetBlock(height == 0 ? blocks[1].BlockId : blocks[0].BlockId, pos);
                pos.Up();
            }
        }
    }
}
