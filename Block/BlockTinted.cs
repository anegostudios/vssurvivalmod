using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockFoliageTinted : Block
    {

        public override int TextureSubIdForRandomBlockPixel(IWorldAccessor world, BlockPos pos, BlockFacing facing, ref int tintIndex)
        {
            tintIndex = 1;
            return base.TextureSubIdForRandomBlockPixel(world, pos, facing, ref tintIndex);
        }

        public override int GetBlockColor(ICoreClientAPI capi, BlockPos pos)
        {
            return capi.ApplyColorTint(1, base.GetBlockColor(capi, pos), pos.X, pos.Y, pos.Z);
        }
    }
}
