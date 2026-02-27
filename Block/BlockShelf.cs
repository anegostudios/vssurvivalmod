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

                    if (BlockEntityShelf.GetShelvableLayout(new ItemStack(obj)) != null)
                    {
                        if (obj is BlockPie pieBlock)
                        {
                            var stack = new ItemStack(obj);

                            stack.Attributes.SetInt("pieSize", 4);
                            stack.Attributes.SetString("topCrustType", "square");
                            stack.Attributes.SetInt("bakeLevel", pieBlock.Variant["state"] switch { "raw" => 0, "partbaked" => 1, "perfect" => 2, "charred" => 3, _ => 0 });

                            ItemStack doughStack = new(api.World.GetItem("dough-spelt"), 2);
                            ItemStack fillingStack = new(api.World.GetItem("fruit-redapple"), 2);
                            pieBlock.SetContents(stack, [doughStack, fillingStack, fillingStack, fillingStack, fillingStack, doughStack]);
                            stack.Attributes.SetFloat("quantityServings", 1);
                            shelvableStacklist.Add(stack);
                        }
                        else shelvableStacklist.Add(new ItemStack(obj));
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

                            if (usableItemStacklist.All(stack => beshelf?.CanUse(stack, bs) == false)) return [.. usableItemStacklist.Where(stack => beshelf?.CanPlace(stack, bs, out bool canTake) == true)];
                            else return null;
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

                            if (usableItemStacklist.Any(stack => beshelf?.CanUse(stack, bs) == true)) return [.. usableItemStacklist.Where(stack => beshelf?.CanPlace(stack, bs, out bool canTake) == true)];
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
                            beshelf?.CanPlace(null, bs, out canTake);
                            return canTake;
                        }
                    }
                };
            });
        }

        public override bool DoPartialSelection(IWorldAccessor world, BlockPos pos)
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

            var worldInteractions = base.GetPlacedBlockInteractionHelp(world, selection, forPlayer);
            
            var resp = world.Claims.TestAccess(forPlayer, selection.Position, EnumBlockAccessFlags.Use);
            if(resp == EnumWorldAccessResponse.Granted) return [.. worldInteractions, ..interactions!];

            return worldInteractions;
        }
    }
}
