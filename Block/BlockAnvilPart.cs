using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class BlockAnvilPart : Block
    {
        public Block? AnvilBlock
        {
            get
            {
                string? metal = Variant["metal"];
                string? type = Variant["type"];

                string? anvilCode = Attributes?["anvilBlock"].AsString(type == null ? $"anvil-{metal}" : $"anvil-{type}-{metal}");
                Block? block = api.World.GetBlock(AssetLocation.Create(anvilCode, Code.Domain));
                return block ?? api.World.GetBlock(AssetLocation.Create($"anvil-singlehorn-{metal}", Code.Domain));
            }
        }

        public override Cuboidf[] GetCollisionBoxes(IBlockAccessor world, BlockPos pos)
        {
            var beap = world.GetBlockEntity<BlockEntityAnvilPart>(pos);
            if (beap?.Inventory[2]?.Empty == true) return [CollisionBoxes[0]];

            return base.GetCollisionBoxes(world, pos);
        }

        public override Cuboidf[] GetSelectionBoxes(IBlockAccessor world, BlockPos pos)
        {
            var beap = world.GetBlockEntity<BlockEntityAnvilPart>(pos);
            if (beap?.Inventory[2]?.Empty == true) return [CollisionBoxes[0]];

            return base.GetSelectionBoxes(world, pos);
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            BlockEntityAnvilPart? beap = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityAnvilPart;
            beap?.OnInteract(byPlayer);
            return true;
        }

        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            if (world.Api is not ICoreClientAPI capi) return base.GetPlacedBlockInteractionHelp(world, selection, forPlayer);

            WorldInteraction[] interactions = ObjectCacheUtil.GetOrCreate(api, "anvilPartInteractions-", () =>
            {
                capi.CollectibleTagRegistry.TryCreateTagSetAndLogIssues(out var fluxTag, "flux");

                ItemStack[] hammerStacks = ObjectCacheUtil.GetToolStacks(api, EnumTool.Hammer);
                var topPartBlock = capi.World.GetBlock(CodeWithVariant("part", "top"));
                ItemStack[] topPartStack = topPartBlock != null ? [new(topPartBlock)] : [];
                List<ItemStack> fluxStacks = [];

                foreach (CollectibleObject obj in api.World.Collectibles)
                {
                    ItemStack newStack = new ItemStack(obj);

                    if (obj.Attributes?.IsTrue("isFlux") == true || obj.GetTags(newStack).Overlaps(fluxTag))
                    {
                        fluxStacks.Add(newStack);
                    }
                }

                return new WorldInteraction[]
                {
                    new() {
                        ActionLangCode = "blockhelp-anvilpart-add-flux",
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = fluxStacks.ToArray(),
                        GetMatchingStacks = (wi, bs, _) => {
                            var part = GetBlockEntity<BlockEntityAnvilPart>(bs.Position);
                            return part?.Inventory[1]?.Empty == true ? wi.Itemstacks : null;
                        }
                    },
                    new() {
                        ActionLangCode = "blockhelp-anvilpart-add-toppart",
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = topPartStack,
                        GetMatchingStacks = (wi, bs, _) => {
                            var inv = GetBlockEntity<BlockEntityAnvilPart>(bs.Position)?.Inventory;
                            return inv?[1]?.Empty == false && inv[2]?.Empty == true ? wi.Itemstacks : null;
                        }
                    },
                    new() {
                        ActionLangCode = "blockhelp-anvilpart-use-hammer",
                        MouseButton = EnumMouseButton.Left,
                        Itemstacks = hammerStacks,
                        GetMatchingStacks = (wi, bs, _) => {
                            var inv = GetBlockEntity<BlockEntityAnvilPart>(bs.Position)?.Inventory;
                            return inv?[1]?.Empty == false && inv[2]?.Empty == false ? wi.Itemstacks : null;
                        }
                    }
                };
            });

            return interactions.Append(base.GetPlacedBlockInteractionHelp(world, selection, forPlayer));
        }
    }
}
