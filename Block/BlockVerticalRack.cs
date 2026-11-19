using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent
{
    public class BlockMoldRack : Block
    {
        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            // Todo: Add interaction help
        }

        public override bool DoPartialSelection(IWorldAccessor world, BlockPos pos)
        {
            return true;
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            BlockEntityMoldRack beshelf = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityMoldRack;
            if (beshelf != null) return beshelf.OnInteract(byPlayer, blockSel);

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }

        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            BlockEntityMoldRack be = GetBlockEntity<BlockEntityMoldRack>(selection);
            if (be == null) return base.GetPlacedBlockInteractionHelp(world, selection, forPlayer);

            WorldInteraction[] interactions = ObjectCacheUtil.GetOrCreate(world.Api, "verticalRackInteractions", () =>
            {
                List<ItemStack> displayStacklist = new List<ItemStack>();

                int count = 5;
                string currentCodePart = "";

                foreach (var hstack in ObjectCacheUtil.TryGet<ItemStack[]>(world.Api, "handbookallstacks"))
                {
                    if (hstack.ItemAttributes == null || !hstack.ItemAttributes.IsTrue("moldrackable")) continue;

                    if (hstack.Collectible.Code.FirstPathPart() != currentCodePart)
                    {
                        currentCodePart = hstack.Collectible.Code.FirstPathPart();
                        count = 0;
                    }

                    if (count >= 5) continue;

                    displayStacklist.Add(hstack);
                }

                displayStacklist.Shuffle(world.Api.World.Rand);

                return new WorldInteraction[] {
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-verticalrack-place",
                        HotKeyCode = null,
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = displayStacklist.ToArray()
                    },
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-verticalrack-take",
                        HotKeyCode = null,
                        MouseButton = EnumMouseButton.Right,
                    }
                };
            });

            return interactions.Append(base.GetPlacedBlockInteractionHelp(world, selection, forPlayer));
        }
    }
}
