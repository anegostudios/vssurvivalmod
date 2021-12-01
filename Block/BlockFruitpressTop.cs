using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockFruitPressTop : Block
    {
        public override bool DoParticalSelection(IWorldAccessor world, BlockPos pos)
        {
            return true;
        }

        public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
        {
            var block = world.BlockAccessor.GetBlock(pos.DownCopy()) as BlockFruitPress;
            if (block != null) block.OnBlockBroken(world, pos.DownCopy(), byPlayer, dropQuantityMultiplier);

            base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
        }

        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            var block = world.BlockAccessor.GetBlock(pos.DownCopy()) as BlockFruitPress;
            if (block != null) return block.OnPickBlock(world, pos.DownCopy());
            return base.OnPickBlock(world, pos);
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            var be = world.BlockAccessor.GetBlockEntity(blockSel.Position.DownCopy()) as BlockEntityFruitPress;
            if (be != null) return be.OnBlockInteractStart(byPlayer, blockSel, blockSel.SelectionBoxIndex == 1 ? EnumFruitPressSection.Screw : EnumFruitPressSection.MashContainer);

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }

        public override bool OnBlockInteractStep(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            var be = world.BlockAccessor.GetBlockEntity(blockSel.Position.DownCopy()) as BlockEntityFruitPress;
            if (be != null) return be.OnBlockInteractStep(secondsUsed, byPlayer, blockSel.SelectionBoxIndex == 1 ? EnumFruitPressSection.Screw : EnumFruitPressSection.MashContainer);

            return base.OnBlockInteractStep(secondsUsed, world, byPlayer, blockSel);
        }

        public override void OnBlockInteractStop(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            var be = world.BlockAccessor.GetBlockEntity(blockSel.Position.DownCopy()) as BlockEntityFruitPress;
            if (be != null) be.OnBlockInteractStop(secondsUsed, byPlayer);

            base.OnBlockInteractStop(secondsUsed, world, byPlayer, blockSel);
        }

        public override bool OnBlockInteractCancel(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, EnumItemUseCancelReason cancelReason)
        {
            var be = world.BlockAccessor.GetBlockEntity(blockSel.Position.DownCopy()) as BlockEntityFruitPress;
            if (be != null) return be.OnBlockInteractCancel(secondsUsed, byPlayer);

            return base.OnBlockInteractCancel(secondsUsed, world, byPlayer, blockSel, cancelReason);
        }


        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            return base.GetPlacedBlockInteractionHelp(world, selection, forPlayer);
        }

        public override string GetPlacedBlockInfo(IWorldAccessor world, BlockPos pos, IPlayer forPlayer)
        {
            var block = world.BlockAccessor.GetBlock(pos.DownCopy()) as BlockFruitPress;
            if (block != null) return block.GetPlacedBlockInfo(world, pos.DownCopy(), forPlayer);

            return base.GetPlacedBlockInfo(world, pos, forPlayer);
        }
    }
}
