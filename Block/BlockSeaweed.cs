using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockSeaweed : BlockWaterPlant
    {
        public override string RemapToLiquidsLayer { get { return "water-still-7"; } }

        protected Block[] blocks;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            blocks = new Block[]
            {
                api.World.BlockAccessor.GetBlock(CodeWithParts("section")),
                api.World.BlockAccessor.GetBlock(CodeWithParts("top")),
            };
        }

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
                + ((api.World.BlockAccessor.GetBlock(pos.X, pos.Y - 4, pos.Z) is BlockSeaweed) ? 1 : 0)
            ;

            for (int i = 0; i < sourceMesh.FlagsCount; i++)
            {
                float y = sourceMesh.xyz[i * 3 + 1];
                VertexFlags.ReplaceWindData(ref sourceMesh.Flags[i], windData + (y > 0 ? 1 : 0));
            }
        }

        public override bool TryPlaceBlockForWorldGenUnderwater(IBlockAccessor blockAccessor, BlockPos pos, BlockFacing onBlockFace, IRandom worldGenRand, int minWaterDepth, int maxWaterDepth, BlockPatchAttributes attributes = null)
        {
            var height = attributes?.Height ?? NatFloat.createGauss(3, 3);

            BlockPos belowPos = pos.DownCopy();

            Block block;

            int depth = 1;
            while (depth < maxWaterDepth)
            {
                belowPos.Down();
                block = blockAccessor.GetBlock(belowPos);
                if (block is BlockWaterPlant) return false;
                if (block.Fertility > 0)
                {
                    PlaceSeaweed(blockAccessor, belowPos, depth, worldGenRand, height);
                    return true;
                }
                if (!block.IsLiquid()) return false;   // Prevent placing seaweed over seaweed (for example might result on a 3-deep plant placed on top of a 5-deep plant's existing position, giving a plant with 2 tops at positions 3 and 5)

                depth++;
            }

            return false;

        }


        internal void PlaceSeaweed(IBlockAccessor blockAccessor, BlockPos pos, int depth, IRandom random, NatFloat heightNatFloat)
        {
            var height = Math.Min(depth, (int)heightNatFloat.nextFloat(1f, random));
            while (height-- > 1)
            {
                pos.Up();
                blockAccessor.SetBlock(blocks[0].BlockId, pos);   // section
            }
            pos.Up();

            if (blocks[1] == null)
            {
                // spawn section if there is no top, (seegrass)
                blockAccessor.SetBlock(blocks[0].BlockId, pos);   // top
            }
            else
            {
                blockAccessor.SetBlock(blocks[1].BlockId, pos);   // top
            }
        }
    }
}
