using System;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockTinted : Block
    {

        public override int GetRandomColor(ICoreClientAPI capi, BlockPos pos, BlockFacing facing)
        {
            BakedCompositeTexture tex = Textures?.First().Value?.Baked;
            int color = capi.BlockTextureAtlas.GetRandomColor(tex.TextureSubId);
            color = capi.World.ApplyColorMapOnRgba(ClimateColorMap, SeasonColorMap, color, pos.X, pos.Y, pos.Z);

            return color;
        }

        public override int GetColor(ICoreClientAPI capi, BlockPos pos)
        {
            return capi.World.ApplyColorMapOnRgba(ClimateColorMap, SeasonColorMap, base.GetColorWithoutTint(capi, pos), pos.X, pos.Y, pos.Z, false);
        }
    }
}
