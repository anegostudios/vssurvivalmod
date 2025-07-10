﻿using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent
{
    /// <summary>
    /// Used to rotate a block that has a 'top' face, such as a wooden log. Requires a "rotation" variant group with "ud", "ns", "we" states.
    /// Uses the "pillar" code.
    /// </summary>
    /// <example><code lang="json">
    ///"behaviors": [
	///	{
	///		"name": "Pillar"
	///	}
	///]
    /// </code>
    /// <code lang="json">
    ///"variantgroups": [
	///	{
	///		"code": "rotation",
	///		"states": [ "ud", "ns", "we" ]
	///	}
	///]
    /// </code></example>
    [DocumentAsJson]
    public class BlockBehaviorPillar : BlockBehavior
    {
        /// <summary>
        /// Swaps placement between horizontal and vertical.
        /// </summary>
        [DocumentAsJson("Optional", "False")]
        bool invertedPlacement;

        public BlockBehaviorPillar(Block block) : base(block)
        {
        }

        public override void Initialize(JsonObject properties)
        {
            base.Initialize(properties);

            invertedPlacement = properties["invertedPlacement"].AsBool(false);
        }

        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref EnumHandling handling, ref string failureCode)
        {
            handling = EnumHandling.PreventDefault;

            string rotation = null;
            switch (blockSel.Face.Axis)
            {
                case EnumAxis.X: rotation = "we"; break;
                case EnumAxis.Y: rotation = "ud"; break;
                case EnumAxis.Z: rotation = "ns"; break;
            }

            if (invertedPlacement)
            {
                BlockFacing[] horVer = Block.SuggestedHVOrientation(byPlayer, blockSel);

                if (blockSel.Face.IsVertical)
                {
                    rotation = horVer[0].Axis == EnumAxis.X ? "we" : "ns";
                } else
                {
                    rotation = "ud";
                }
            }

            Block orientedBlock = world.BlockAccessor.GetBlock(block.CodeWithParts(rotation));

            if (orientedBlock.CanPlaceBlock(world, byPlayer, blockSel, ref failureCode))
            {
                orientedBlock.DoPlaceBlock(world, byPlayer, blockSel, itemstack);
                return true;
            }

            return false;
        }

        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos, ref EnumHandling handling)
        {
            handling = EnumHandling.PreventDefault;
            return new ItemStack(world.GetBlock(block.CodeWithParts("ud")));
        }

        public override AssetLocation GetRotatedBlockCode(int angle, ref EnumHandling handled)
        {
            handled = EnumHandling.PreventDefault;

            if (block.LastCodePart() == "ud") return block.Code;

            string[] angles = { "ns", "we" };
            if (angle < 0) angle += 360;
            int index = angle / 90;
            if (block.LastCodePart() == "we") index++;

            return block.CodeWithParts(angles[index % 2]);
        }
    }
}
