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
            Block block = blockAccessor.GetBlock(pos.DownCopy());
            return block.IsLiquid() && block.LiquidLevel == 7 && block.LiquidCode.Contains("water");
        }
    }
}
