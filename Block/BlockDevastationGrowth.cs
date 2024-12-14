using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.ServerMods;

namespace Vintagestory.GameContent;

public class BlockDevastationGrowth : Block
{
    public override bool TryPlaceBlockForWorldGen(IBlockAccessor blockAccessor, BlockPos pos, BlockFacing onBlockFace, IRandom worldGenRand, BlockPatchAttributes attributes = null)
    {
        var belowBlockId = blockAccessor.GetBlock(pos.X, pos.Y-1, pos.Z, BlockLayersAccess.Solid).Id;
        if (!GenDevastationLayer.DevastationBlockIds.Contains(belowBlockId)) return false;
        // do not spawn ontop of other plants
        if (blockAccessor.GetBlock(pos.DownCopy(), BlockLayersAccess.Solid) is BlockDevastationGrowth) return false;

        return base.TryPlaceBlockForWorldGen(blockAccessor, pos, onBlockFace, worldGenRand, attributes);
    }
}
