using System;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace Vintagestory.GameContent
{
    public class ItemSlotWatertight : ItemSlotSurvival
    {
        public float capacityLitres;

        public ItemSlotWatertight(InventoryBase inventory, float capacityLitres = 6) : base(inventory)
        {
            this.capacityLitres = capacityLitres;
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
            BlockLiquidContainerBase liqCntBlock = sourceSlot.Itemstack?.Block as BlockLiquidContainerBase;
            
            if (liqCntBlock != null)
            {
                ItemStack contentStack = liqCntBlock.GetContent(sourceSlot.Itemstack);
                var liqProps = BlockLiquidContainerBase.GetContainableProps(contentStack);

                bool stackable = !Empty && itemstack.Equals(world, contentStack, GlobalConstants.IgnoredStackAttributes);

                if ((Empty || stackable) && contentStack != null)
                {
                    ItemStack bucketStack = sourceSlot.Itemstack;

                    float toMoveLitres = (op?.ActingPlayer?.Entity.Controls.ShiftKey ?? false) ? liqCntBlock.CapacityLitres : liqCntBlock.TransferSizeLitres;
                    float curDestLitres = StackSize / liqProps.ItemsPerLitre;
                    float curSrcLitres = contentStack.StackSize / liqProps.ItemsPerLitre;

                    toMoveLitres = Math.Min(toMoveLitres, curSrcLitres);

                    toMoveLitres *= bucketStack.StackSize;
                    toMoveLitres = Math.Min(toMoveLitres, capacityLitres - curDestLitres);

                    if (toMoveLitres > 0)
                    {
                        int moveQuantity = (int)(liqProps.ItemsPerLitre * toMoveLitres);
                        ItemStack takenContentStack = liqCntBlock.TryTakeContent(bucketStack, moveQuantity / bucketStack.StackSize);

                        takenContentStack.StackSize *= bucketStack.StackSize;
                        takenContentStack.StackSize += StackSize;                      
                        this.itemstack = takenContentStack;
                        MarkDirty();
                        op.MovedQuantity = moveQuantity;
                    }
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
            BlockLiquidContainerBase liqCntBlock = sourceSlot.Itemstack?.Block as BlockLiquidContainerBase;

            if (liqCntBlock != null)
            {
                if (Empty) return;

                ItemStack contentStack = liqCntBlock.GetContent(sourceSlot.Itemstack);

                float toMoveLitres = op.ShiftDown ? liqCntBlock.CapacityLitres : liqCntBlock.TransferSizeLitres;
                var srcProps = BlockLiquidContainerBase.GetContainableProps(Itemstack);
                float availableLitres = StackSize / (srcProps?.ItemsPerLitre ?? 1);

                toMoveLitres *= sourceSlot.Itemstack.StackSize;
                toMoveLitres = Math.Min(toMoveLitres, availableLitres);

                if (contentStack == null)
                {
                    int moved = liqCntBlock.TryPutLiquid(sourceSlot.Itemstack, Itemstack, toMoveLitres / sourceSlot.Itemstack.StackSize);
                    TakeOut(moved * sourceSlot.Itemstack.StackSize);
                    MarkDirty();
                } else
                {
                    if (itemstack.Equals(world, contentStack, GlobalConstants.IgnoredStackAttributes))
                    {
                        int moved = liqCntBlock.TryPutLiquid(sourceSlot.Itemstack, liqCntBlock.GetContent(sourceSlot.Itemstack), toMoveLitres / sourceSlot.Itemstack.StackSize);
                        TakeOut(moved * sourceSlot.Itemstack.StackSize);
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
