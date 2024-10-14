using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockSeashell : Block
    {
        static string[] colors = new string[] { "latte", "plain", "darkpurple", "cinnamon" };
        static string[] rarecolors = new string[] { "seafoam", "turquoise" };

        static string[] types = new string[] { "scallop", "sundial", "turritella", "clam", "conch", "seastar", "volute" };
        static Dictionary<string, string> tmpDict = new Dictionary<string, string>();

        public override bool TryPlaceBlockForWorldGenUnderwater(IBlockAccessor blockAccessor, BlockPos pos, BlockFacing onBlockFace, IRandom worldgenRandom, int minWaterDepth, int maxWaterDepth, BlockPatchAttributes attributes = null)
        {
            var depth = 1;
            Block block;
            while (depth < maxWaterDepth)
            {
                pos.Down();
                block = blockAccessor.GetBlock(pos);
                if (block is BlockWaterPlant) return false;
                if (block is BlockSeashell) return false;
                if(!block.IsLiquid()) break;
                depth++;
            }

            if (depth >= maxWaterDepth) return false;

            pos.Up();

            if (blockAccessor.GetBlock(pos).IsReplacableBy(this))
            {
                blockAccessor.SetBlock(BlockId, pos);
                return true;
            }

            return false;
        }

        public override bool TryPlaceBlockForWorldGen(IBlockAccessor blockAccessor, BlockPos pos, BlockFacing onBlockFace, IRandom worldGenRand, BlockPatchAttributes attributes = null)
        {
            if (!HasBeachyGround(blockAccessor, pos))
            {
                return false;
            }


            tmpDict["type"] = types[worldGenRand.NextInt(types.Length)];

            if (worldGenRand.NextInt(100) < 8)
            {
                tmpDict["color"] = rarecolors[worldGenRand.NextInt(rarecolors.Length)];
            } else
            {
                tmpDict["color"] = colors[worldGenRand.NextInt(colors.Length)];
            }


            Block block = blockAccessor.GetBlock(CodeWithVariants(tmpDict));
            if (block == null) return false;

            if (blockAccessor.GetBlock(pos).IsReplacableBy(this))
            {
                blockAccessor.SetBlock(block.BlockId, pos);
                return true;
            }

            return false;
        }

        internal virtual bool HasBeachyGround(IBlockAccessor blockAccessor, BlockPos pos)
        {
            Block blockBelow = blockAccessor.GetBlockBelow(pos);
            return blockBelow.SideSolid[BlockFacing.UP.Index] && (blockBelow.BlockMaterial == EnumBlockMaterial.Sand || blockBelow.BlockMaterial == EnumBlockMaterial.Gravel);
        }

    }
}
