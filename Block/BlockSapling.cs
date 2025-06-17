using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent
{
    /// <summary>
    /// A small class which is only (for now) needed so as to have the correct selection box for a BESapling which has not yet sprouted
    /// (can maybe do other things in future like change its windwave in planters, handle deer grazing on it)
    /// </summary>
    public class BlockSapling : BlockPlant
    {


        public override Cuboidf[] GetSelectionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            BlockEntitySapling bes = blockAccessor.GetBlockEntity(pos) as BlockEntitySapling;
            if (bes != null && bes.stage == EnumTreeGrowthStage.Seed && bes.plantedFromSeed)
            {
                return new Cuboidf[] { new Cuboidf(0.2f, 0f, 0.2f, 0.8f, 0.1875f, 0.8f) };    // a low broad selection box to match the dirt pile, othewise the regular selection box looks like there is an invisible sapling here
            }

            return base.GetSelectionBoxes(blockAccessor, pos);
        }
    }
}
