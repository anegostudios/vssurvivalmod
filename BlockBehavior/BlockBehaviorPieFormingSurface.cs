using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class PieFormingSurfaceBackwardsCompatSystem : ModSystem
    {
        public override void AssetsFinalize(ICoreAPI api)
        {
            foreach (Block block in api.World.Blocks)
            {
                // Allow the legacy attribute to apply this behavior
                if (block.Attributes?.IsTrue("pieFormingSurface") == true && !block.BlockBehaviors.Contains(bh => bh is BlockBehaviorPieFormingSurface))
                {
                    block.BlockBehaviors.Append(new BlockBehaviorPieFormingSurface(block));
                    block.CollectibleBehaviors.Append(new BlockBehaviorPieFormingSurface(block));
                }
            }

            base.AssetsFinalize(api);
        }
    }

    /// <summary>
    /// Specifies that this block works as a pie forming surface. Does not have any properties.
    /// </summary>
    /// <example><code lang="json">
    ///"behaviors": [
	///	{ "name": "PieFormingSurface" }
	///]
    /// </code></example>
    public class BlockBehaviorPieFormingSurface(Block block) : BlockBehavior(block)
    {
        static WorldInteraction[] interactions = null!;

        ICoreAPI api = null!;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            this.api = api;

            block.InteractionHelpYOffset += 0.25f;

            if (api.Side != EnumAppSide.Client || interactions != null) return;

            List<ItemStack> doughStacks = [];

            foreach (CollectibleObject obj in api.World.Collectibles)
            {
                if (InPieProperties.ReadFrom(obj) is not InPieProperties pieProps) continue;

                if (pieProps.PartType == EnumPiePartType.Crust)
                {
                    doughStacks.Add(new ItemStack(obj, pieProps.PortionSize));
                }
            }

            interactions = [
                new()
                {
                    ActionLangCode = "heldhelp-makepie",
                    Itemstacks = [.. doughStacks],
                    HotKeyCode = "shift",
                    MouseButton = EnumMouseButton.Right,
                }
            ];
        }

        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer, ref EnumHandling handling)
        {
            WorldInteraction[] wi = base.GetPlacedBlockInteractionHelp(world, selection, forPlayer, ref handling);

            BlockPos abovePos = selection.Position.UpCopy();

            Block placeBlock = world.BlockAccessor.GetBlock(abovePos);
            if (placeBlock.Replaceable >= 6000)
            {
                return wi.Append(interactions);
            }

            return wi;
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref EnumHandling handling)
        {
            if (blockSel != null && byPlayer.Entity.Controls.ShiftKey)
            {
                if (StackCanPlacePie(byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack))
                {
                    (api.World.GetBlock(new AssetLocation("pie-raw")) as BlockPie)?.TryPlacePie(byPlayer.Entity, blockSel);
                    handling = EnumHandling.PreventDefault;
                    return true;
                }

                return false;
            }

            return base.OnBlockInteractStart(world, byPlayer, blockSel, ref handling);
        }

        public static bool StackCanPlacePie(ItemStack? stack)
        {
            if (stack == null)
            {
                return false;
            }

            if (stack.Collectible.GetCollectibleInterface<ILiquidSource>() != null)
            {
                return false;
            }

            if (InPieProperties.ReadFrom(stack) is not InPieProperties pieProps)
            {
                return false;
            }

            float totalPortions = stack.StackSize / pieProps.ItemsPerPortion();
            if (totalPortions < 1)
            {
                return false;
            }

            return true;
        }
    }
}
