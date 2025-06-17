using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent
{
    public class BlockFruitPress : Block
    {
        Cuboidf[] particleCollBoxes;
        WorldInteraction[] interactions;

        public bool RightMouseDown = false;

        public override void OnLoaded(ICoreAPI api)
        {
            particleCollBoxes = new Cuboidf[] { CollisionBoxes[0].Clone() };
            particleCollBoxes[0].Y1 = 0.6875f;

            base.OnLoaded(api);

            if (api.Side == EnumAppSide.Client)
            {
                (api as ICoreClientAPI).Input.InWorldAction += Input_InWorldAction;
            }

            // This needs to happen a frame later because accessing blc.CapacityLitres requires the OnLoaded() method to be called on the other blocks.
            api.Event.EnqueueMainThreadTask(() =>
            {
                interactions = ObjectCacheUtil.GetOrCreate(api, "fruitPressInteractions", () =>
                {
                    List<ItemStack> fillableContainers = new List<ItemStack>();

                    foreach (CollectibleObject obj in api.World.Collectibles)
                    {
                        //else if (handStack.Collectible is BlockLiquidContainerBase blockLiqCont && blockLiqCont.AllowHeldLiquidTransfer && blockLiqCont.IsTopOpened && blockLiqCont.CapacityLitres < 20 && BucketSlot.Empty)
                        if (obj is BlockLiquidContainerBase blc && blc.IsTopOpened && blc.AllowHeldLiquidTransfer && blc.CapacityLitres < 20)
                        {
                            fillableContainers.Add(new ItemStack(obj));
                        }
                    }

                    return new WorldInteraction[]
                    {

                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-fruitpress-putremovebucket",
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = fillableContainers.ToArray(),
                        ShouldApply = (wi, bs, es) =>
                        {
                            return bs.SelectionBoxIndex == (int)EnumFruitPressSection.Ground;
                        }
                    }
                    };
                });
            }, "initFruitPressInteractions");
        }

        private void Input_InWorldAction(EnumEntityAction action, bool on, ref EnumHandling handled)
        {
            if (action == EnumEntityAction.RightMouseDown && !on)
            {
                RightMouseDown = false;
            }
        }

        public override Cuboidf[] GetParticleCollisionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            
            return particleCollBoxes;
        }

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
            if (be != null)
            {
                var handled = be.OnBlockInteractStart(byPlayer, blockSel, blockSel.SelectionBoxIndex == 1 ? EnumFruitPressSection.MashContainer : EnumFruitPressSection.Ground, !RightMouseDown);
                RightMouseDown = true;
                return handled;
            }

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
            return interactions.Append(base.GetPlacedBlockInteractionHelp(world, selection, forPlayer));
        }

        public override string GetPlacedBlockInfo(IWorldAccessor world, BlockPos pos, IPlayer forPlayer)
        {
            return base.GetPlacedBlockInfo(world, pos, forPlayer);
        }


    }
}
