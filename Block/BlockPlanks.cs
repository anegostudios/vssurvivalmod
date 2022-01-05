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
    public class BlockPlanks : Block
    {
        public override bool DoPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ItemStack byItemStack)
        {
            if (blockSel.Face.IsVertical && Variant["orientation"] == "hor")
            {
                return world.GetBlock(CodeWithVariant("orientation", "ver")).DoPlaceBlock(world, byPlayer, blockSel, byItemStack);
            }

            return base.DoPlaceBlock(world, byPlayer, blockSel, byItemStack);
        }
    }
}
