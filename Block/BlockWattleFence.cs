using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent;

public class BlockWattleFence : BlockFence
{
    private int daubUpgradeAmount;

    private List<ItemStack> daubStacks;
    private WorldInteraction[] interactions;

    public override void OnLoaded(ICoreAPI api)
    {
        base.OnLoaded(api);
        daubStacks = new List<ItemStack>();

        daubUpgradeAmount = Attributes["daubUpgradeAmount"].AsInt(2);

        foreach (CollectibleObject obj in api.World.Collectibles)
        {
            if (obj.Code.Path.StartsWithFast("daubraw"))
            {
                daubStacks.Add(new ItemStack(obj, daubUpgradeAmount));
            }
        }

        if (api.Side == EnumAppSide.Client)
        {
            interactions = new WorldInteraction[]
            {
                new()
                {
                    ActionLangCode = "blockhelp-clayform-adddaub",
                    Itemstacks = daubStacks.ToArray(),
                    MouseButton = EnumMouseButton.Right
                }
            };
        }
    }

    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        if (base.OnBlockInteractStart(world, byPlayer, blockSel))
        {
            return true;
        }

        var activeHotbarSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
        if (!activeHotbarSlot.Empty && activeHotbarSlot.Itemstack?.Collectible?.Code?.Path.StartsWithFast("daubraw") == true)
        {
            if (activeHotbarSlot.StackSize >= daubUpgradeAmount &&
                world.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak))
            {
                var color = activeHotbarSlot.Itemstack.Collectible.Variant["color"];
                var block = world.GetBlock(new AssetLocation("daub-" + color + "-wattle"));
                if (byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative)
                {
                    activeHotbarSlot.TakeOut(daubUpgradeAmount);
                }
                world.BlockAccessor.SetBlock(block.Id, blockSel.Position);
                return true;
            }
        }

        return false;
    }

    public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
    {
        return interactions;
    }
}
