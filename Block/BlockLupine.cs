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
        Random rand;
        Block[] uncommonVariants;
        Block[] rareVariants;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            rand = new Random(api.World.Seed);

            uncommonVariants = new Block[] { api.World.GetBlock(CodeWithParts("white")), api.World.GetBlock(CodeWithParts("red")) };
            rareVariants = new Block[] { api.World.GetBlock(CodeWithParts("orange")) };
        }

        public override bool TryPlaceBlockForWorldGen(IBlockAccessor blockAccessor, BlockPos pos, BlockFacing onBlockFace)
        {
            bool placed = base.TryPlaceBlockForWorldGen(blockAccessor, pos, onBlockFace);
            if (!placed) return false;

            double rnd = rand.NextDouble();
            if (rnd < 1/300.0)
            {
                GenRareColorPatch(blockAccessor, pos, uncommonVariants[rand.Next(rareVariants.Length)]);
            } else if (rnd < 1/120.0)
            {
                GenRareColorPatch(blockAccessor, pos, uncommonVariants[rand.Next(uncommonVariants.Length)]);
            }

            return true;
        }


        private void GenRareColorPatch(IBlockAccessor blockAccessor, BlockPos pos, Block block)
        {
            int cnt = 2 + rand.Next(6);
            int tries = 30;
            BlockPos npos = pos.Copy();
            
            while (cnt > 0 && tries-- > 0)
            {
                npos.Set(pos).Add(rand.Next(5) - 2, 0, rand.Next(5) - 2);
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
