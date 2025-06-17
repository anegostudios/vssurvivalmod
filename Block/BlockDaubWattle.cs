using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent;

public class BlockDaubWattle : Block
{
    private int daubUpgradeAmount;
    private WorldInteraction[] interactions;

    public override void OnLoaded(ICoreAPI api)
    {
        base.OnLoaded(api);

        daubUpgradeAmount = Attributes["daubUpgradeAmount"].AsInt(2);

        var assetLocation = new AssetLocation("daubraw-" + Variant["color"]);
        var collectible = api.World.GetItem(assetLocation);

        if (api.Side == EnumAppSide.Client)
        {
            interactions = new WorldInteraction[]
            {
                new()
                {
                    ActionLangCode = "blockhelp-clayform-adddaub",
                    Itemstacks = new ItemStack[]{new (collectible, daubUpgradeAmount)},
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
            var type = Variant["type"];
            var color = activeHotbarSlot.Itemstack.Collectible.Variant["color"];
            if (!string.Equals(type, "normal") &&
                string.Equals(color, Variant["color"]) &&
                activeHotbarSlot.StackSize >= daubUpgradeAmount &&
                world.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak))
            {
                var newType = type switch
                {
                    "wattle" => "-cracked",
                    "cracked" => "-normal",
                    _ => "-wattle" // fallback to base type if something goes wrong
                };
                var block = world.GetBlock(new AssetLocation("daub-" + color + newType));
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
