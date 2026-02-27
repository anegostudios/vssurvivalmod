using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent
{
    public class BlockBehaviorCuttableTallGrass : BlockBehavior
    {
        public BlockBehaviorCuttableTallGrass(Block block) : base(block)
        {
        }

        public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier, ref EnumHandling handling)
        {
            if (byPlayer?.InventoryManager.ActiveTool == EnumTool.Knife && block.Variant["tallgrass"] != null && block.Variant["tallgrass"] != "eaten")
            {
                block.SpawnDropsAndRemoveBlock(world, pos, byPlayer, dropQuantityMultiplier);
                world.BlockAccessor.SetBlock(world.GetBlock(block.CodeWithVariant("tallgrass", "eaten")).Id, pos);
                handling = EnumHandling.PreventSubsequent;
            }

        }
    }
}
