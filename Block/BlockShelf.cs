using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class BlockShelf : Block
    {
        WorldInteraction[]? interactions;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            interactions = ObjectCacheUtil.GetOrCreate(api, "shelfInteractions", () =>
            {
                List<ItemStack> usableItemStacklist = new List<ItemStack>();
                List<ItemStack> shelvableStacklist = new List<ItemStack>();

                foreach (var obj in api.World.Collectibles)
                {
                    if (obj?.Attributes?["mealContainer"]?.AsBool() == true || obj is IContainedInteractable or IBlockMealContainer ||
                        obj?.Attributes?["canSealCrock"]?.AsBool() == true)
                    {
                        usableItemStacklist.Add(new ItemStack(obj));
                    }

                    if (obj?.Attributes?["shelvable"]?.AsBool(false) ?? false)
                    {
                        shelvableStacklist.Add(new ItemStack(obj));
                    }
                }

                var sstacks = shelvableStacklist.ToArray();

                return new WorldInteraction[]
                {
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-shelf-use",
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = sstacks,
                        GetMatchingStacks = (wi, bs, es) =>
                        {
                            var beshelf = api.World.BlockAccessor.GetBlockEntity(bs.Position) as BlockEntityShelf;

                            return usableItemStacklist.Where(stack => beshelf?.CanUse(stack, bs) == true)?.ToArray();
                        }
                    },
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-shelf-place",
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = sstacks,
                        GetMatchingStacks = (wi, bs, es) =>
                        {
                            var beshelf = api.World.BlockAccessor.GetBlockEntity(bs.Position) as BlockEntityShelf;

                            if (usableItemStacklist.Any(stack => beshelf?.CanUse(stack, bs) == true) || beshelf?.CanPlace(bs, out bool canTake) == false) return null;
                            else return sstacks;
                        }
                    },
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-shelf-place",
                        HotKeyCode = "shift",
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = sstacks,
                        GetMatchingStacks = (wi, bs, es) =>
                        {
                            var beshelf = api.World.BlockAccessor.GetBlockEntity(bs.Position) as BlockEntityShelf;

                            if (usableItemStacklist.Any(stack => beshelf?.CanUse(stack, bs) == true) && beshelf?.CanPlace(bs, out bool canTake) == true) return sstacks;
                            else return null;
                        }
                    },
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-shelf-take",
                        MouseButton = EnumMouseButton.Right,
                        RequireFreeHand = true,
                        ShouldApply = (wi, bs, es) =>
                        {
                            var beshelf = api.World.BlockAccessor.GetBlockEntity(bs.Position) as BlockEntityShelf;

                            bool canTake = false;
                            beshelf?.CanPlace(bs, out canTake);
                            return canTake;
                        }
                    }
                };
            });
        }

        public override bool DoParticalSelection(IWorldAccessor world, BlockPos pos)
        {
            return true;
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (world.BlockAccessor.GetBlockEntity(blockSel.Position) is BlockEntityShelf beshelf) return beshelf.OnInteract(byPlayer, blockSel);

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }

        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            return base.GetPlacedBlockInteractionHelp(world, selection, forPlayer).Append(interactions);
        }
    }
}
