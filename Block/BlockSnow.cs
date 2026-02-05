using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent
{
    public class BlockSnow : Block
    {
        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            snowLevel = 8;
        }

        Cuboidf[] fullBox = new Cuboidf[] { new Cuboidf(0,0,0,1,1,1) };
        public override Cuboidf[] GetCollisionBoxes(IBlockAccessor world, BlockPos pos)
        {
            if (world.GetBlockAbove(pos) is BlockLayered)
            {
                return fullBox;
            }

            return base.GetCollisionBoxes(world, pos);
        }

        /// <summary>
        /// No need to render the face of lake / glacier ice adjoining solid snow blocks; and this will help with z-fighting in arctic landscapes
        /// </summary>
        public override bool ShouldMergeFace(int facingIndex, Block neighbourIce, int intraChunkIndex3d)
        {
            return true;
        }

        public override Block GetSnowCoveredVariant(BlockPos pos, float snowLevel)
        {
            if (snowLevel <= 0) return api.World.Blocks[0];
            if ((int)snowLevel >= 8) return this;
            return api.World.GetBlock("snowlayer-" + (int)snowLevel);
        }
    }
}
