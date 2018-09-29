using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockBloomery : Block
    {

        public override bool OnTryIgniteBlock(IEntityAgent byEntity, BlockPos pos, float secondsIgniting, ref EnumHandling handling)
        {
            BlockEntityBloomery beb = byEntity.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityBloomery;
            

            bool ok = (beb == null || beb.IsBurning || !beb.CanBurn()) ? false : secondsIgniting < 2f;
            if (ok) handling = EnumHandling.PreventDefault;
            return ok;
        }

        public override void OnTryIgniteBlockOver(IEntityAgent byEntity, BlockPos pos, float secondsIgniting, ref EnumHandling handling)
        {
            if (secondsIgniting < 1.95f) return;

            handling = EnumHandling.PreventDefault;

            BlockEntityBloomery beb = byEntity.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityBloomery;
            beb?.TryIgnite();
        }


        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            ItemStack hotbarstack = byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack;

            if (hotbarstack != null && hotbarstack.Class == EnumItemClass.Block && hotbarstack.Collectible.Code.Path.StartsWith("bloomerychimney"))
            {
                Block aboveBlock = world.BlockAccessor.GetBlock(blockSel.Position.UpCopy());
                if (aboveBlock.IsReplacableBy(hotbarstack.Block))
                {
                    hotbarstack.Block.DoPlaceBlock(world, blockSel.Position.UpCopy(), BlockFacing.UP, hotbarstack);
                    world.PlaySoundAt(Sounds?.Place, blockSel.Position.X, blockSel.Position.Y + 1, blockSel.Position.Z, byPlayer, true, 16, 1);

                    byPlayer.InventoryManager.ActiveHotbarSlot.TakeOut(1);
                }


                
                return true;
            }

            BlockEntityBloomery beb = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityBloomery;
            if (beb != null)
            {
                
                if (hotbarstack == null) return false;
                return beb.TryAdd(byPlayer.InventoryManager.ActiveHotbarSlot);
                
            }

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }

        public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
        {
            Block aboveBlock = world.BlockAccessor.GetBlock(pos.UpCopy());
            if (aboveBlock.Code.Path == "bloomerychimney")
            {
                aboveBlock.OnBlockBroken(world, pos.UpCopy(), byPlayer, dropQuantityMultiplier);
            }

            base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
        }

        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1f)
        {
            List<ItemStack> todrop = new List<ItemStack>();

            for (int i = 0; i < Drops.Length; i++)
            {
                if (Drops[i].Tool != null && (byPlayer == null || Drops[i].Tool != byPlayer.InventoryManager.ActiveTool)) continue;

                ItemStack stack = Drops[i].GetNextItemStack(dropQuantityMultiplier);
                if (stack == null) continue;

                todrop.Add(stack);
                if (Drops[i].LastDrop) break;
            }

            return todrop.ToArray();
        }

    }
}
