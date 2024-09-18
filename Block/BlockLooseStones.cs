using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class BlockLooseStones : BlockLooseRock
    {
        protected override void generate(IBlockAccessor blockAccessor, Block block, BlockPos pos, IRandom worldGenRand)
        {
            if (worldGenRand.NextDouble() <= 0.20)
            {
                block = blockAccessor.GetBlock(block.CodeWithPath("looseflints-" + block.Variant["rock"] + "-" + block.Variant["cover"]));
            }

            blockAccessor.SetBlock(block.Id, pos);
        }


        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            ItemStack[] stacks = GetDrops(world, selection.Position, forPlayer);

            bool knappable = stacks != null && stacks.Length > 0 && stacks[0].Collectible.Attributes["knappable"].AsBool(false);
            if (!knappable)
            {
                return base.GetPlacedBlockInteractionHelp(world, selection, forPlayer);
            }


            return new WorldInteraction[] {
                new WorldInteraction()
                {
                    ActionLangCode = "blockhelp-knappingsurface-knap",
                    HotKeyCode = "shift",
                    Itemstacks = stacks,
                    MouseButton = EnumMouseButton.Right,
                }
            }.Append(base.GetPlacedBlockInteractionHelp(world, selection, forPlayer));
        }
    }
}
