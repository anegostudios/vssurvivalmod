using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class BlockFlowerPot : Block
    {
        WorldInteraction[] interactions = new WorldInteraction[0];

        public override void OnLoaded(ICoreAPI api)
        {
            List<ItemStack> stacks = new List<ItemStack>();

            if (Variant["contents"] != "empty")
            {
                return;
            }

            foreach (var block in api.World.Blocks)
            {
                if (block.Code == null || block.IsMissing) continue;

                if (block is BlockFlowerPot)
                {
                    string name = block.Variant["contents"];

                    Block plantBlock = api.World.BlockAccessor.GetBlock(CodeWithPath("flower-" + name));
                    if (plantBlock != null)
                    {
                        stacks.Add(new ItemStack(plantBlock));
                        continue;
                    }

                    plantBlock = api.World.BlockAccessor.GetBlock(CodeWithPath("sapling-" + name));
                    if (plantBlock != null)
                    {
                        stacks.Add(new ItemStack(plantBlock));
                        continue;
                    }

                    plantBlock = api.World.BlockAccessor.GetBlock(CodeWithPath("mushroom-" + name + "-normal"));
                    if (plantBlock != null)
                    {
                        stacks.Add(new ItemStack(plantBlock));
                        continue;
                    }

                    plantBlock = api.World.BlockAccessor.GetBlock(CodeWithPath("flower-" + LastCodePart(0) + "-" + LastCodePart(1)));
                    if (plantBlock != null)
                    {
                        stacks.Add(new ItemStack(plantBlock));
                        continue;
                    }
                }
            }

            interactions = new WorldInteraction[]
            {
                new WorldInteraction()
                {
                    ActionLangCode = "blockhelp-flowerpot-plant",
                    MouseButton = EnumMouseButton.Right,
                    Itemstacks = stacks.ToArray()
                }
            };
        }

        public Block GetPottedPlant(IWorldAccessor world)
        {
            string name = Code.Path.Substring(Code.Path.LastIndexOf("-") + 1);

            if (name == "empty") return null;

            Block block = world.BlockAccessor.GetBlock(new AssetLocation(Attributes["contentBlockCode"].AsString()));

            return block;
        }

        public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1f)
        {
            base.OnBlockBroken(world, pos, byPlayer);

            Block block = GetPottedPlant(world);
            if (block != null)
            {
                world.SpawnItemEntity(new ItemStack(block), pos.ToVec3d().Add(0.5, 0.5, 0.5));
            }
        }

        public override BlockDropItemStack[] GetDropsForHandbook(ItemStack handbookStack, IPlayer forPlayer)
        {
            return GetHandbookDropsFromBreakDrops(handbookStack, forPlayer);
        }

        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1f)
        {
            Block block = world.BlockAccessor.GetBlock(CodeWithVariant("contents", "empty"));
            if (block == null)
            {
                block = world.BlockAccessor.GetBlock(new AssetLocation(Code.Domain, FirstCodePart() + "-empty"));
            }

            return new ItemStack[] { new ItemStack(block) };
        }

        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            return base.OnPickBlock(world, pos);
        }


        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            Block block = GetPottedPlant(world);
            if (block != null) return false;

            IItemStack heldItem = byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack;

            if (heldItem != null && heldItem.Class == EnumItemClass.Block)
            {
                block = GetBlockToPlant(world, heldItem);
                if (block != null && this != block)
                {
                    world.PlaySoundAt(block.Sounds?.Place, blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z, byPlayer);
                    world.BlockAccessor.SetBlock(block.BlockId, blockSel.Position);

                    byPlayer.InventoryManager.ActiveHotbarSlot.TakeOut(1);
                    byPlayer.InventoryManager.ActiveHotbarSlot.MarkDirty();

                    return true;
                }
            }
            return false;
        }

        private Block GetBlockToPlant(IWorldAccessor world, IItemStack heldItem)
        {
            string type = heldItem.Block.LastCodePart(0);
            Block block = world.BlockAccessor.GetBlock(CodeWithVariant("contents", type));
            if (block == null)
            {
                type = heldItem.Block.LastCodePart(1);
                block = world.BlockAccessor.GetBlock(CodeWithVariant("contents", type));
            }

            if (block == null)
            {
                type = heldItem.Block.LastCodePart(1) + "-" + heldItem.Block.LastCodePart(0);
                block = world.BlockAccessor.GetBlock(CodeWithVariant("contents", type));
            }

            if (block != null)
            {
                string firstcode = heldItem.Block.FirstCodePart();
                if (firstcode != "sapling" && firstcode != "mushroom" && firstcode != "flower") return null;
            }

            return block;
        }


        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            return interactions.Append(base.GetPlacedBlockInteractionHelp(world, selection, forPlayer));
        }
    }
}
