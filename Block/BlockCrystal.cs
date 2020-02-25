using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockCrystal : Block
    {
        Block[] FacingBlocks;
        Random rand;

        public override void OnLoaded(ICoreAPI api)
        {
            rand = new Random(api.World.Seed + 131);

            FacingBlocks = new Block[6];
            for (int i = 0; i < 6; i++)
            {
                FacingBlocks[i] = api.World.GetBlock(CodeWithPart(BlockFacing.ALLFACES[i].Code, 2));
            }
        }

        public Block FacingCrystal(IBlockAccessor blockAccessor, BlockFacing facing)
        {
            return blockAccessor.GetBlock(CodeWithPart(facing.Code));
        }

        public override double ExplosionDropChance(IWorldAccessor world, BlockPos pos, EnumBlastType blastType)
        {
            return 0.2;
        }

    }
}
