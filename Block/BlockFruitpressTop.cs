using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent;

public class BlockFruitPressTop : Block
{
    private WorldInteraction[] interactions = null!;

    public override void OnLoaded(ICoreAPI api)
    {
        base.OnLoaded(api);

        PlacedPriorityInteract = true;

        interactions = ObjectCacheUtil.GetOrCreate(api, "fruitPressInteractionsTop", () =>
        {
            List<ItemStack> juiceableStacks = new();

            foreach (CollectibleObject obj in api.World.Collectibles)
            {
                if (obj.Attributes?["juiceableProperties"].Exists == true)
                {
                    juiceableStacks.Add(new ItemStack(obj));
                }
            }

            ItemStack[] jStacks = juiceableStacks.ToArray();

            return new WorldInteraction[]
            {
                    new()
                    {
                        ActionLangCode = "blockhelp-fruitpress-press",
                        MouseButton = EnumMouseButton.Right,
                        ShouldApply = (wi, bs, es) =>
                        {
                            return api.World.BlockAccessor.GetBlockEntity(bs.Position.DownCopy()) is BlockEntityFruitPress bePress && bs.SelectionBoxIndex == 1 && bePress.CanScrew;
                        }
                    },
                    new()
                    {
                        ActionLangCode = "blockhelp-fruitpress-release",
                        HotKeyCode = "ctrl",
                        MouseButton = EnumMouseButton.Right,
                        ShouldApply = (wi, bs, es) =>
                        {
                            return api.World.BlockAccessor.GetBlockEntity(bs.Position.DownCopy()) is BlockEntityFruitPress bePress && bs.SelectionBoxIndex == 1;
                        }
                    },
                    new()
                    {
                        ActionLangCode = "blockhelp-fruitpress-fillremove",
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = jStacks,
                        GetMatchingStacks = (wi, bs, es) =>
                        {
                            if (api.World.BlockAccessor.GetBlockEntity(bs.Position.DownCopy()) is BlockEntityFruitPress bePress && bs.SelectionBoxIndex == 0 && bePress.CanFillRemoveItems) return jStacks;
                            else return null;
                        }
                    },
                    new()
                    {
                        ActionLangCode = "blockhelp-fruitpress-fillsingle",
                        HotKeyCode = "shift",
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = jStacks,
                        GetMatchingStacks = (wi, bs, es) =>
                        {
                            if (api.World.BlockAccessor.GetBlockEntity(bs.Position.DownCopy()) is BlockEntityFruitPress bePress && bs.SelectionBoxIndex == 0 && bePress.CanFillRemoveItems) return jStacks;
                            else return null;
                        }
                    },
                    new()
                    {
                        ActionLangCode = "blockhelp-fruitpress-fillstack",
                        HotKeyCode = "ctrl",
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = jStacks,
                        GetMatchingStacks = (wi, bs, es) =>
                        {
                            if (api.World.BlockAccessor.GetBlockEntity(bs.Position.DownCopy()) is BlockEntityFruitPress bePress && bs.SelectionBoxIndex == 0 && bePress.CanFillRemoveItems) return jStacks;
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
        if (world.BlockAccessor.GetBlock(pos.DownCopy()) is BlockFruitPress block) block.OnBlockBroken(world, pos.DownCopy(), byPlayer, dropQuantityMultiplier);
    }

    public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
    {
        if (world.BlockAccessor.GetBlock(pos.DownCopy()) is BlockFruitPress block) return block.OnPickBlock(world, pos.DownCopy());
        return base.OnPickBlock(world, pos);
    }

    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        if (world.BlockAccessor.GetBlock(blockSel.Position.DownCopy()) is not BlockFruitPress) return false;

        if (world.BlockAccessor.GetBlockEntity(blockSel.Position.DownCopy()) is BlockEntityFruitPress be)
        {
            if (world.Side == EnumAppSide.Server) be.HandleInteraction(true, blockSel.SelectionBoxIndex == 1 ? EnumFruitPressSection.Screw : EnumFruitPressSection.MashContainer, (IServerPlayer)byPlayer);
            return true;
        }

        return base.OnBlockInteractStart(world, byPlayer, blockSel);
    }

    public override bool OnBlockInteractStep(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        return base.OnBlockInteractStep(secondsUsed, world, byPlayer, blockSel) || true;
    }

    public override void OnBlockInteractStop(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        if (world.BlockAccessor.GetBlockEntity(blockSel.Position.DownCopy()) is BlockEntityFruitPress be)
        {
            if (world.Side == EnumAppSide.Server) be.HandleInteraction(false, blockSel.SelectionBoxIndex == 1 ? EnumFruitPressSection.Screw : EnumFruitPressSection.MashContainer, (IServerPlayer)byPlayer);
        }

        base.OnBlockInteractStop(secondsUsed, world, byPlayer, blockSel);
    }

    public override bool OnBlockInteractCancel(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, EnumItemUseCancelReason cancelReason)
    {
        if (world.BlockAccessor.GetBlockEntity(blockSel.Position.DownCopy()) is BlockEntityFruitPress be)
        {
            if (world.Side == EnumAppSide.Server) be.HandleInteraction(false, blockSel.SelectionBoxIndex == 1 ? EnumFruitPressSection.Screw : EnumFruitPressSection.MashContainer, (IServerPlayer)byPlayer);
            return true;
        }

        return base.OnBlockInteractCancel(secondsUsed, world, byPlayer, blockSel, cancelReason);
    }

    public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
    {
        return interactions.Append(base.GetPlacedBlockInteractionHelp(world, selection, forPlayer));
    }

    public override string GetPlacedBlockInfo(IWorldAccessor world, BlockPos pos, IPlayer forPlayer)
    {
        if (world.BlockAccessor.GetBlock(pos.DownCopy()) is BlockFruitPress block) return block.GetPlacedBlockInfo(world, pos.DownCopy(), forPlayer);

        return base.GetPlacedBlockInfo(world, pos, forPlayer);
    }
}