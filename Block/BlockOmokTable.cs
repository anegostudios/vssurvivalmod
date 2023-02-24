using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockOmokTable : Block
    {
        Cuboidf[] seleBoxes;


        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            int size = 15;

            seleBoxes = new Cuboidf[size*size];

            for (int dx = 0; dx < size; dx++)
            {
                for (int dz = 0; dz < size; dz++)
                {
                    seleBoxes[dz * size + dx] = new Cuboidf()
                    {
                        X1 = (1.5f + dx) / 16f,
                        Y1 = 1 / 16f,
                        Z1 = (1.5f + dz) / 16f,
                        X2 = (2.5f + dx) / 16f,
                        Y2 = 2 / 16f,
                        Z2 = (2.5f + dz) / 16f,
                    };
                }
            }
        }

        public override bool DoParticalSelection(IWorldAccessor world, BlockPos pos)
        {
            return true;
        }

        public override Cuboidf[] GetSelectionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            if (seleBoxes == null) return base.GetSelectionBoxes(blockAccessor, pos);
            return seleBoxes;
        }


        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            BlockEntityOmokTable bedc = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityOmokTable;
            if (bedc != null) return bedc.OnInteract(byPlayer, blockSel);

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }
    }
}
