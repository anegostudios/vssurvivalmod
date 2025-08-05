using System;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent
{

    public class BlockLooseGears : Block
    {
        Block[] Variants = new Block[5];

        Random rand;
        public override void OnLoaded(ICoreAPI api)
        {
            rand = new Random();
            base.OnLoaded(api);

            Variants[0] = api.World.GetBlock(new AssetLocation("loosegears-1")); // 51.61%
            Variants[1] = api.World.GetBlock(new AssetLocation("loosegears-2")); // 25.81%
            Variants[2] = api.World.GetBlock(new AssetLocation("loosegears-3")); // 12.90%
            Variants[3] = api.World.GetBlock(new AssetLocation("loosegears-4")); // 6.45%
            Variants[4] = api.World.GetBlock(new AssetLocation("loosegears-5")); // 3.23%
        }

        public override bool TryPlaceBlockForWorldGen(IBlockAccessor blockAccessor, BlockPos pos, BlockFacing onBlockFace, IRandom worldGenRand, BlockPatchAttributes attributes = null)
        {
            Block block = blockAccessor.GetBlock(pos);
            if (block.Id != 0) return false;

            for (int i = 1; i < 5; i++)
            {
                block = blockAccessor.GetBlockBelow(pos, i, BlockLayersAccess.Solid);

                if (block.SideSolid[BlockFacing.UP.Index])
                {
                    blockAccessor.SetBlock(rndGearBlock().BlockId, pos.DownCopy(i-1));
                    return true;
                }

                if (block.Id != 0) return false;
            }

            return false;
        }


        private Block rndGearBlock()
        {
            double val = rand.Next();
            if (val < 0.0323) return Variants[4];
            if (val < 0.0323 + 0.0645) return Variants[3];
            if (val < 0.0968 + 0.1290) return Variants[2];
            if (val < 0.2258 + 0.2581) return Variants[1];

            return Variants[0];
        }
    }
}
