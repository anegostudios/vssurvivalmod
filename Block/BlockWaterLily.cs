using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
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
            return block.IsLiquid() && block.LiquidLevel == 7 && block.LiquidCode == "water" && upblock.Id==0;
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

        public override bool TryPlaceBlockForWorldGen(IBlockAccessor blockAccessor, BlockPos pos, BlockFacing onBlockFace, IRandom worldGenRand, BlockPatchAttributes attributes = null)
        {
            // Don't spawn in 3 deep water
            if (blockAccessor.GetBlock(pos.X, pos.Y - 4, pos.Z, BlockLayersAccess.Fluid).Id != 0) return false;
            // do not spawn ontop of other plants
            if (blockAccessor.GetBlock(pos.DownCopy(), BlockLayersAccess.Solid) is BlockPlant) return false;

            return base.TryPlaceBlockForWorldGen(blockAccessor, pos, onBlockFace, worldGenRand, attributes);
        }
    }

    public class BlockWaterLilyGiant : BlockWaterLily
    {
        public override bool TryPlaceBlockForWorldGen(IBlockAccessor blockAccessor, BlockPos pos, BlockFacing onBlockFace, IRandom worldGenRand, BlockPatchAttributes attributes = null)
        {
            // Don't spawn in 3 deep water
            if (blockAccessor.GetBlock(pos.X, pos.Y - 4, pos.Z, BlockLayersAccess.Fluid).Id != 0) return false;

            var canPlace = true;
            var tmpPos = pos.Copy();
            for (int x = -2; x < 3; x++)
            {
                for (int z = -2; z < 3; z++)
                {
                    tmpPos.Set(pos.X + x, pos.Y, pos.Z + z);
                    var block = blockAccessor.GetBlock(tmpPos, BlockLayersAccess.Solid);
                    var block2 = blockAccessor.GetBlock(tmpPos.Down(), BlockLayersAccess.Solid);
                    if (block?.Id != 0 || block2?.Id != 0)
                    {
                        canPlace = false;
                    }
                }
            }
            if (!canPlace) return false;
            if (!CanPlantStay(blockAccessor, pos)) return false;

            var block3 = blockAccessor.GetBlock(pos);

            if (block3.IsReplacableBy(this))
            {
                if (block3.EntityClass != null)
                {
                    blockAccessor.RemoveBlockEntity(pos);
                }

                blockAccessor.SetBlock(BlockId, pos);

                if (EntityClass != null)
                {
                    blockAccessor.SpawnBlockEntity(EntityClass, pos);
                }

                return true;
            }

            return false;
        }
    }
}
