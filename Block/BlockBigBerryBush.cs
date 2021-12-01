using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockBerryBush : BlockPlant
    {
        public override bool CanPlantStay(IBlockAccessor blockAccessor, BlockPos pos)
        {
            Block belowBlock = blockAccessor.GetBlock(pos.DownCopy());
            if (belowBlock.Fertility > 0) return true;
            if (!(belowBlock is BlockBerryBush)) return false;

            Block belowbelowBlock = blockAccessor.GetBlock(pos.DownCopy(2));
            return belowbelowBlock.Fertility > 0 && this.Attributes?.IsTrue("stackable") == true && belowBlock.Attributes?.IsTrue("stackable") == true;
        }


        public override int GetRandomColor(ICoreClientAPI capi, BlockPos pos, BlockFacing facing, int rndIndex = -1)
        {
            if (Textures == null || Textures.Count == 0) return 0;
            BakedCompositeTexture tex = Textures?.First().Value?.Baked;
            if (tex == null) return 0;

            int color = capi.BlockTextureAtlas.GetRandomColor(tex.TextureSubId, rndIndex);
            color = capi.World.ApplyColorMapOnRgba("climatePlantTint", SeasonColorMap, color, pos.X, pos.Y, pos.Z);
            return color;
        }


        public override int GetColor(ICoreClientAPI capi, BlockPos pos)
        {
            int color = base.GetColorWithoutTint(capi, pos);

            return capi.World.ApplyColorMapOnRgba("climatePlantTint", SeasonColorMap, color, pos.X, pos.Y, pos.Z, false);
        }
    }
}
