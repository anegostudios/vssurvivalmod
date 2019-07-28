using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace Vintagestory.GameContent
{
    public class ItemSlotWatertight : ItemSlotSurvival
    {

        public ItemSlotWatertight(InventoryBase inventory) : base(inventory)
        {
        }

        public override bool CanTake()
        {
            bool isLiquid = !Empty && itemstack.Collectible.IsLiquid();
            if (isLiquid) return false;

            return base.CanTake();
        }

        protected override void ActivateSlotLeftClick(ItemSlot sourceSlot, ref ItemStackMoveOperation op)
        {
            IWorldAccessor world = inventory.Api.World;
            BlockBucket bucketblock = sourceSlot.Itemstack?.Block as BlockBucket;
            
            if (bucketblock != null)
            {
                ItemStack bucketContents = bucketblock.GetContent(world, sourceSlot.Itemstack);
                bool stackable = !Empty && itemstack.Equals(world, bucketContents, GlobalConstants.IgnoredStackAttributes);

                if ((Empty || stackable) && bucketContents != null)
                {
                    ItemStack bucketStack = sourceSlot.Itemstack;
                    ItemStack takenContent = bucketblock.TryTakeContent(world, bucketStack, op.ActingPlayer?.Entity?.Controls.Sneak == true ? 10 : 1);
                    sourceSlot.Itemstack = bucketStack;
                    takenContent.StackSize += StackSize;
                    this.itemstack = takenContent;
                    MarkDirty();
                    return;
                }

                return;
            }

            string contentItemCode = sourceSlot.Itemstack?.ItemAttributes?["contentItemCode"].AsString();
            if (contentItemCode != null)
            {
                ItemStack contentStack = new ItemStack(world.GetItem(AssetLocation.Create(contentItemCode, sourceSlot.Itemstack.Collectible.Code.Domain)));
                bool stackable = !Empty && itemstack.Equals(world, contentStack, GlobalConstants.IgnoredStackAttributes);

                if ((Empty || stackable) && contentStack != null)
                {
                    if (stackable) this.itemstack.StackSize++;
                    else this.itemstack = contentStack;

                    MarkDirty();
                    ItemStack bowlStack = new ItemStack(world.GetBlock(AssetLocation.Create(sourceSlot.Itemstack.ItemAttributes["emptiedBlockCode"].AsString(), sourceSlot.Itemstack.Collectible.Code.Domain)));
                    if (sourceSlot.StackSize == 1)
                    {
                        sourceSlot.Itemstack = bowlStack;
                    } else
                    {
                        sourceSlot.Itemstack.StackSize--;
                        if (!op.ActingPlayer.InventoryManager.TryGiveItemstack(bowlStack))
                        {
                            world.SpawnItemEntity(bowlStack, op.ActingPlayer.Entity.Pos.XYZ);
                        }
                    }
                    sourceSlot.MarkDirty();
                }

                return;
            }

            if (sourceSlot.Itemstack?.ItemAttributes?["contentItem2BlockCodes"].Exists == true) return;

            base.ActivateSlotLeftClick(sourceSlot, ref op);
        }

        protected override void ActivateSlotRightClick(ItemSlot sourceSlot, ref ItemStackMoveOperation op)
        {
            IWorldAccessor world = inventory.Api.World;
            BlockBucket bucketblock = sourceSlot.Itemstack?.Block as BlockBucket;

            if (bucketblock != null)
            {
                if (Empty) return;

                ItemStack bucketContents = bucketblock.GetContent(world, sourceSlot.Itemstack);

                if (bucketContents == null)
                {
                    TakeOut(bucketblock.TryPutContent(world, sourceSlot.Itemstack, Itemstack, 1));
                    MarkDirty();
                } else
                {
                    if (itemstack.Equals(world, bucketContents, GlobalConstants.IgnoredStackAttributes))
                    {
                        TakeOut(bucketblock.TryPutContent(world, sourceSlot.Itemstack, bucketblock.GetContent(world, sourceSlot.Itemstack), 1));
                        MarkDirty();
                        return;
                    }
                }                

                return;
            }

            
            if (itemstack != null && sourceSlot.Itemstack?.ItemAttributes?["contentItem2BlockCodes"].Exists == true)
            {
                string outBlockCode = sourceSlot.Itemstack.ItemAttributes["contentItem2BlockCodes"][itemstack.Collectible.Code.ToShortString()].AsString();

                if (outBlockCode != null)
                {
                    ItemStack outBlockStack = new ItemStack(world.GetBlock(AssetLocation.Create(outBlockCode, sourceSlot.Itemstack.Collectible.Code.Domain)));

                    if (sourceSlot.StackSize == 1)
                    {
                        sourceSlot.Itemstack = outBlockStack;
                    }
                    else
                    {
                        sourceSlot.Itemstack.StackSize--;
                        if (!op.ActingPlayer.InventoryManager.TryGiveItemstack(outBlockStack))
                        {
                            world.SpawnItemEntity(outBlockStack, op.ActingPlayer.Entity.Pos.XYZ);
                        }
                    }

                    sourceSlot.MarkDirty();
                    TakeOut(1);
                }

                return;
            }

            if (sourceSlot.Itemstack?.ItemAttributes?["contentItem2BlockCodes"].Exists == true || sourceSlot.Itemstack?.ItemAttributes?["contentItemCode"].AsString() != null) return;

            base.ActivateSlotRightClick(sourceSlot, ref op);
        }

        



    }
}
