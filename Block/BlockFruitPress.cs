using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockFruitPress : Block
    {
        public override bool DoParticalSelection(IWorldAccessor world, BlockPos pos)
        {
            return true;
        }

        public override void OnBlockPlaced(IWorldAccessor world, BlockPos pos, ItemStack byItemStack = null)
        {
            base.OnBlockPlaced(world, pos, byItemStack);

            Block toPlaceBlock = world.GetBlock(new AssetLocation("fruitpresstop-" + Variant["orientation"]));
            world.BlockAccessor.SetBlock(toPlaceBlock.BlockId, pos.UpCopy());
        }

        public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
        {
            Block upBlock = api.World.BlockAccessor.GetBlock(pos.UpCopy());
            if (upBlock.Code.Path == "fruitpresstop-" + Variant["orientation"])
            {
                world.BlockAccessor.SetBlock(0, pos.UpCopy());
            }

            base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
        }

        public override bool CanPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref string failureCode)
        {
            if (!base.CanPlaceBlock(world, byPlayer, blockSel, ref failureCode)) return false;

            BlockSelection bs = blockSel.Clone();
            bs.Position = blockSel.Position.UpCopy();
            if (!base.CanPlaceBlock(world, byPlayer, bs, ref failureCode)) return false;

            return true;
        }


        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            var be = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityFruitPress;
            if (be != null) return be.OnBlockInteractStart(byPlayer, blockSel, blockSel.SelectionBoxIndex == 1 ? EnumFruitPressSection.MashContainer : EnumFruitPressSection.Ground);

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }

        public override bool OnBlockInteractStep(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            var be = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityFruitPress;
            if (be != null) return be.OnBlockInteractStep(secondsUsed, byPlayer, blockSel.SelectionBoxIndex == 1 ? EnumFruitPressSection.MashContainer : EnumFruitPressSection.Ground);

            return base.OnBlockInteractStep(secondsUsed, world, byPlayer, blockSel);
        }

        public override void OnBlockInteractStop(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            var be = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityFruitPress;
            if (be != null) be.OnBlockInteractStop(secondsUsed, byPlayer);

            base.OnBlockInteractStop(secondsUsed, world, byPlayer, blockSel);
        }

        public override bool OnBlockInteractCancel(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, EnumItemUseCancelReason cancelReason)
        {
            var be = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityFruitPress;
            if (be != null) return be.OnBlockInteractCancel(secondsUsed, byPlayer);

            return base.OnBlockInteractCancel(secondsUsed, world, byPlayer, blockSel, cancelReason);
        }

        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            return base.GetPlacedBlockInteractionHelp(world, selection, forPlayer);
        }
    }
}
