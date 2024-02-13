using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Vintagestory.GameContent
{
    public class BEBehaviorRockRubbleFromAttributes : BEBehaviorShapeFromAttributes, IMaterialExchangeable
    {
        public BEBehaviorRockRubbleFromAttributes(BlockEntity blockentity) : base(blockentity)
        {
        }

        public bool ExchangeWith(ItemSlot fromSlot, ItemSlot toSlot)
        {
            string toRock = toSlot.Itemstack.Collectible.Variant["rock"];
            if (fromSlot.Itemstack.Collectible.Code == this.Block.Code)
            {
                Type = typeWithRockType(toRock);
                Blockentity.MarkDirty(true);
                return true;
            }

            return false;
        }

        public override void OnPlacementBySchematic(ICoreServerAPI api, IBlockAccessor blockAccessor, BlockPos pos, Dictionary<int, Dictionary<int, int>> replaceBlocks, int centerrockblockid, Block layerBlock, bool resolveImports)
        {
            string toRock = api.World.Blocks[centerrockblockid].Variant["rock"];
            if (toRock != null && Type != null)
            {
                Type = typeWithRockType(toRock);
            }
        }

        string typeWithRockType(string rock)
        {
            var parts = Type.Split('-');
            if (parts.Length < 3) return Type;

            return parts[0] + '-' + parts[1] + '-' + rock;
        }
    }
}
