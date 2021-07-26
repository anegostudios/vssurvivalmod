using System;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Client.Tesselation;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockLeavesNarrow : BlockLeaves
    {
        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            string woodType = Variant["wood"];
            return new ItemStack(world.GetBlock(CodeWithParts("placed", woodType, "5")));
        }
    }
}
