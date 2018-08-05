using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockFlowerPot : Block
    {
        public Block GetPottedPlant(IWorldAccessor world)
        {
            string name = Code.Path.Substring(Code.Path.LastIndexOf("-") + 1);

            if (name == "empty") return null;

            Block block = world.BlockAccessor.GetBlock(CodeWithPath("flower-" + name));
            if (block != null) return block;

            block = world.BlockAccessor.GetBlock(CodeWithPath("sapling-" + name));
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

        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1f)
        {
            Block block = world.BlockAccessor.GetBlock(CodeWithParts("empty"));
            return new ItemStack[] { new ItemStack(block) };
        }

        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            return base.OnPickBlock(world, pos);
            //Block block = world.BlockAccessor.GetBlock(CodeWithParts("empty"));
            //return new ItemStack(block);
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
            Block block = world.BlockAccessor.GetBlock(CodeWithParts(type));
            if (block == null)
            {
                type = heldItem.Block.LastCodePart(1);
                block = world.BlockAccessor.GetBlock(CodeWithParts(type));
            }
            return block;
        }
    }
}
