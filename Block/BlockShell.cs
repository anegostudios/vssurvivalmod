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

        public override bool TryPlaceBlockForWorldGen(IBlockAccessor blockAccessor, BlockPos pos, BlockFacing onBlockFace, LCGRandom worldGenRand)
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
            Block block = blockAccessor.GetBlock(pos.X, pos.Y - 1, pos.Z);
            return block.SideSolid[BlockFacing.UP.Index] && (block.BlockMaterial == EnumBlockMaterial.Sand || block.BlockMaterial == EnumBlockMaterial.Gravel);
        }

    }
}
