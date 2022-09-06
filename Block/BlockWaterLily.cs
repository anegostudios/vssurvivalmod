using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockWaterLily : BlockPlant
    {
        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
        {
            if (CanPlantStay(world.BlockAccessor, blockSel.Position.UpCopy()))
            {
                blockSel = blockSel.Clone();
                blockSel.Position = blockSel.Position.Up();
                return base.TryPlaceBlock(world, byPlayer, itemstack, blockSel, ref failureCode);
            }

            failureCode = "requirefullwater";

            return false;
        }

        public override bool CanPlantStay(IBlockAccessor blockAccessor, BlockPos pos)
        {
            Block block = blockAccessor.GetBlock(pos.DownCopy(), BlockLayersAccess.Fluid);
            Block upblock = blockAccessor.GetBlock(pos, BlockLayersAccess.Fluid);
            return block.IsLiquid() && block.LiquidLevel == 7 && block.LiquidCode.Contains("water") && upblock.Id==0;
        }

        public override int GetColor(ICoreClientAPI capi, BlockPos pos)
        {
            int color = GetColorWithoutTint(capi, pos);
            return capi.World.ApplyColorMapOnRgba("climatePlantTint", "seasonalFoliage", color, pos.X, pos.Y, pos.Z);
        }

        public override int GetRandomColor(ICoreClientAPI capi, BlockPos pos, BlockFacing facing, int rndIndex = -1)
        {
            CompositeTexture tex = Textures.First().Value;
            if (tex?.Baked == null) return 0;

            int color = capi.BlockTextureAtlas.GetRandomColor(tex.Baked.TextureSubId, rndIndex);
            return capi.World.ApplyColorMapOnRgba("climatePlantTint", "seasonalFoliage", color, pos.X, pos.Y, pos.Z); ;
        }
    }
}
