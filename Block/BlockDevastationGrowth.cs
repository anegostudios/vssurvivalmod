using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.ServerMods;

#nullable disable

namespace Vintagestory.GameContent;

public class BlockDevastationGrowth : Block
{
    public override bool TryPlaceBlockForWorldGen(IBlockAccessor blockAccessor, BlockPos pos, BlockFacing onBlockFace, IRandom worldGenRand, BlockPatchAttributes attributes = null)
    {
        var belowBlockId = blockAccessor.GetBlockBelow(pos, 1, BlockLayersAccess.Solid).Id;
        if (!GenDevastationLayer.DevastationBlockIds.Contains(belowBlockId)) return false;
        // do not spawn ontop of other plants
        if (blockAccessor.GetBlock(pos.DownCopy(), BlockLayersAccess.Solid) is BlockDevastationGrowth) return false;

        return base.TryPlaceBlockForWorldGen(blockAccessor, pos, onBlockFace, worldGenRand, attributes);
    }
}
