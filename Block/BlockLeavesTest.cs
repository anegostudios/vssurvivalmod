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
    public class BlockLeavesTest : BlockLeaves
    {
        string[] tints = { "needles", "darkneedles", "larch", "birch", "maple", "crimsonking", "foliage", "tropical" };

        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
        {
            BlockPos pos = blockSel.Position;
            string tint = tints[pos.X / 2 % 8];
            int z = (pos.Z / 2 % 3);
            string climate = z > 1 ? "crimson" : z == 1 ? "dark" : "light";
            Block orientedBlock = world.BlockAccessor.GetBlock(CodeWithParts(climate, tint));
            if (orientedBlock == null) orientedBlock = world.BlockAccessor.GetBlock(CodeWithParts(tint));

            if (orientedBlock.CanPlaceBlock(world, byPlayer, blockSel, ref failureCode))
            {
                orientedBlock.DoPlaceBlock(world, byPlayer, blockSel, itemstack);
                return true;
            }

            return false;
        }

        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            return new ItemStack(this);
        }
    }
}
