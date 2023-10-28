using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class BlockFruitPressTop : Block
    {
        WorldInteraction[] interactions;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            interactions = ObjectCacheUtil.GetOrCreate(api, "fruitPressInteractionsTop", () =>
            {
                List<ItemStack> juiceableStacks = new List<ItemStack>();

                foreach (CollectibleObject obj in api.World.Collectibles)
                {
                    if (obj.Attributes?["juiceableProperties"].Exists == true)
                    {
                        juiceableStacks.Add(new ItemStack(obj));
                    }
                }

                var jstacks = juiceableStacks.ToArray();

                return new WorldInteraction[]
                {
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-fruitpress-press",
                        MouseButton = EnumMouseButton.Right,
                        ShouldApply = (wi, bs, es) =>
                        {
                            var bePress = api.World.BlockAccessor.GetBlockEntity(bs.Position.DownCopy()) as BlockEntityFruitPress;

                            return bePress != null && bs.SelectionBoxIndex == 1 && bePress.CanScrew;
                        }
                    },
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-fruitpress-release",
                        MouseButton = EnumMouseButton.Right,
                        ShouldApply = (wi, bs, es) =>
                        {
                            var bePress = api.World.BlockAccessor.GetBlockEntity(bs.Position.DownCopy()) as BlockEntityFruitPress;

                            return bePress != null && bs.SelectionBoxIndex == 1 && bePress.CanUnscrew;
                        }
                    },
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-fruitpress-fillremove",
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = jstacks,
                        GetMatchingStacks = (wi, bs, es) =>
                        {
                            var bePress = api.World.BlockAccessor.GetBlockEntity(bs.Position.DownCopy()) as BlockEntityFruitPress;

                            if (bePress != null && bs.SelectionBoxIndex == 0 && bePress.CanFillRemoveItems) return jstacks;
                            else return null;
                        }
                    }
                };
            });
        }

        public override bool DoParticalSelection(IWorldAccessor world, BlockPos pos)
        {
            return true;
        }

        public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
        {
            var block = world.BlockAccessor.GetBlock(pos.DownCopy()) as BlockFruitPress;
            if (block != null) block.OnBlockBroken(world, pos.DownCopy(), byPlayer, dropQuantityMultiplier);

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
            var block = world.BlockAccessor.GetBlock(blockSel.Position.DownCopy()) as BlockFruitPress;

            if (be != null)
            {
                var handled = be.OnBlockInteractStart(byPlayer, blockSel, blockSel.SelectionBoxIndex == 1 ? EnumFruitPressSection.Screw : EnumFruitPressSection.MashContainer, !block.RightMouseDown);
                block.RightMouseDown = true;
                return handled;
            }

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
            return interactions.Append(base.GetPlacedBlockInteractionHelp(world, selection, forPlayer));
        }



        public override string GetPlacedBlockInfo(IWorldAccessor world, BlockPos pos, IPlayer forPlayer)
        {
            var block = world.BlockAccessor.GetBlock(pos.DownCopy()) as BlockFruitPress;
            if (block != null) return block.GetPlacedBlockInfo(world, pos.DownCopy(), forPlayer);

            return base.GetPlacedBlockInfo(world, pos, forPlayer);
        }
    }
}
