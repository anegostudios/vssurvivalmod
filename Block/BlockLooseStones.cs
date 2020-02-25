using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class BlockLooseStones : Block
    {
        public override bool TryPlaceBlockForWorldGen(IBlockAccessor blockAccessor, BlockPos pos, BlockFacing onBlockFace, LCGRandom worldGenRand)
        {
            if (!HasSolidGround(blockAccessor, pos))
            {
                return false;
            }

            int blockId = BlockId;

            if (worldGenRand.NextDouble() <= 0.20)
            {
                blockId = blockAccessor.GetBlock(CodeWithPath("looseflints-" + LastCodePart())).BlockId;
            }

            Block block = blockAccessor.GetBlock(pos);
            if (block.IsReplacableBy(this) && !block.IsLiquid())
            {
                blockAccessor.SetBlock(blockId, pos);
                return true;
            }

            return false;
        }

        internal virtual bool HasSolidGround(IBlockAccessor blockAccessor, BlockPos pos)
        {
            Block block = blockAccessor.GetBlock(pos.DownCopy());
            return block.SideSolid[BlockFacing.UP.Index];
        }

        
        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            return new WorldInteraction[] {
                new WorldInteraction()
                {
                    ActionLangCode = "blockhelp-knappingsurface-knap",
                    HotKeyCode = "sneak",
                    Itemstacks = GetDrops(world, selection.Position, forPlayer),
                    MouseButton = EnumMouseButton.Right,
                }
            }.Append(base.GetPlacedBlockInteractionHelp(world, selection, forPlayer));
        }
    }
}
