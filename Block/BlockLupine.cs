using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockLupine : BlockPlant
    {
        Block[] uncommonVariants;
        Block[] rareVariants;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);


            uncommonVariants = new Block[] { api.World.GetBlock(CodeWithParts("white")), api.World.GetBlock(CodeWithParts("red")) };
            rareVariants = new Block[] { api.World.GetBlock(CodeWithParts("orange")) };
        }

        public override bool TryPlaceBlockForWorldGen(IBlockAccessor blockAccessor, BlockPos pos, BlockFacing onBlockFace, Random worldGenRand)
        {
            bool placed = base.TryPlaceBlockForWorldGen(blockAccessor, pos, onBlockFace, worldGenRand);
            if (!placed) return false;

            double rnd = worldGenRand.NextDouble();
            if (rnd < 1/300.0)
            {
                GenRareColorPatch(blockAccessor, pos, uncommonVariants[worldGenRand.Next(rareVariants.Length)], worldGenRand);
            } else if (rnd < 1/120.0)
            {
                GenRareColorPatch(blockAccessor, pos, uncommonVariants[worldGenRand.Next(uncommonVariants.Length)], worldGenRand);
            }

            return true;
        }


        private void GenRareColorPatch(IBlockAccessor blockAccessor, BlockPos pos, Block block, Random worldGenRand)
        {
            int cnt = 2 + worldGenRand.Next(6);
            int tries = 30;
            BlockPos npos = pos.Copy();
            
            while (cnt > 0 && tries-- > 0)
            {
                npos.Set(pos).Add(worldGenRand.Next(5) - 2, 0, worldGenRand.Next(5) - 2);
                npos.Y = blockAccessor.GetTerrainMapheightAt(npos) + 1;

                Block nblock = blockAccessor.GetBlock(npos);

                if ((nblock.IsReplacableBy(block) || nblock is BlockLupine) && CanPlantStay(blockAccessor, npos))
                {
                    blockAccessor.SetBlock(block.BlockId, npos);
                    cnt--;
                }
            }
        }
    }
}
