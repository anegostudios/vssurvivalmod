using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace Vintagestory.API.Common
{
    public class ItemSlotBarrelInput : ItemSlot
    {
        public ItemSlotBarrelInput(InventoryBase inventory) : base(inventory)
        {

        }

        public override void ActivateSlot(ItemSlot sourceSlot, ref ItemStackMoveOperation op)
        {
            base.ActivateSlot(sourceSlot, ref op);
        }

        public override void OnItemSlotModified(ItemStack stack)
        {
            base.OnItemSlotModified(stack);

            if (itemstack == null) return;

            ItemSlotLiquidOnly liquidSlot = inventory[1] as ItemSlotLiquidOnly;

            bool stackable = !liquidSlot.Empty && liquidSlot.Itemstack.Equals(inventory.Api.World, itemstack, GlobalConstants.IgnoredStackAttributes);
            if (stackable)
            {
                int remaining = liquidSlot.Itemstack.Collectible.MaxStackSize - liquidSlot.Itemstack.StackSize;

                WaterTightContainableProps props = BlockLiquidContainerBase.GetContainableProps(liquidSlot.Itemstack);
                if (props != null)
                {
                    int max = (int)(liquidSlot.CapacityLitres * props.ItemsPerLitre);
                    int maxOverride = props.MaxStackSize;  // allows 64 rot to be placed in barrel
                    remaining = Math.Max(max, maxOverride) - liquidSlot.Itemstack.StackSize;
                }

                int moved = GameMath.Clamp(itemstack.StackSize, 0, remaining);
                liquidSlot.Itemstack.StackSize += moved;

                itemstack.StackSize -= moved;
                if (itemstack.StackSize <= 0) itemstack = null;

                liquidSlot.MarkDirty();
                MarkDirty();
                return;
            }

            if (itemstack.Collectible.Attributes?.IsTrue("barrelMoveToLiquidSlot") == true || itemstack.Collectible.Attributes?["waterTightContainerProps"].Exists == true)
            {
                if (stackable)
                {
                    int remainingspace = itemstack.Collectible.MaxStackSize - liquidSlot.StackSize;
                    int movableq = Math.Min(itemstack.StackSize, remainingspace);

                    liquidSlot.Itemstack.StackSize += movableq;
                    this.itemstack.StackSize -= movableq;
                    if (StackSize <= 0) itemstack = null;

                    MarkDirty();
                    liquidSlot.MarkDirty();
                    return;
                }
                if (liquidSlot.Empty)
                {
                    liquidSlot.Itemstack = this.itemstack.Clone();
                    this.itemstack = null;
                    MarkDirty();
                    liquidSlot.MarkDirty();
                }
            }

            return;
        }

        protected override void ActivateSlotRightClick(ItemSlot sourceSlot, ref ItemStackMoveOperation op)
        {
            ItemSlotLiquidOnly liquidSlot = inventory[1] as ItemSlotLiquidOnly;
            IWorldAccessor world = inventory.Api.World;

            if (sourceSlot?.Itemstack?.Collectible is ILiquidSink sink && !liquidSlot.Empty && sink.AllowHeldLiquidTransfer)
            {
                ItemStack liqSlotStack = liquidSlot.Itemstack;
                var curTargetLiquidStack = sink.GetContent(sourceSlot.Itemstack);

                bool liquidstackable = curTargetLiquidStack==null || liqSlotStack.Equals(world, curTargetLiquidStack, GlobalConstants.IgnoredStackAttributes);

                if (liquidstackable)
                {
                    var lprops = BlockLiquidContainerBase.GetContainableProps(liqSlotStack);

                    float curSourceLitres = liqSlotStack.StackSize / lprops.ItemsPerLitre;
                    float curTargetLitres = sink.GetCurrentLitres(sourceSlot.Itemstack);

                    float toMoveLitres = op.CtrlDown ? sink.TransferSizeLitres : (sink.CapacityLitres - curTargetLitres);

                    toMoveLitres *= sourceSlot.StackSize;
                    toMoveLitres = Math.Min(curSourceLitres, toMoveLitres);

                    if (toMoveLitres > 0)
                    {
                        op.MovedQuantity = sink.TryPutLiquid(sourceSlot.Itemstack, liqSlotStack, toMoveLitres / sourceSlot.StackSize);

                        liquidSlot.Itemstack.StackSize -= op.MovedQuantity * sourceSlot.StackSize;
                        if (liquidSlot.Itemstack.StackSize <= 0) liquidSlot.Itemstack = null;
                        liquidSlot.MarkDirty();
                        sourceSlot.MarkDirty();

                        var pos = op.ActingPlayer?.Entity?.Pos;
                        if (pos != null) op.World.PlaySoundAt(lprops.PourSound, pos.X, pos.Y, pos.Z);
                    }
                }

                return;
            }

            base.ActivateSlotRightClick(sourceSlot, ref op);
        }

        protected override void ActivateSlotLeftClick(ItemSlot sourceSlot, ref ItemStackMoveOperation op)
        {
            if (sourceSlot.Empty)
            {
                base.ActivateSlotLeftClick(sourceSlot, ref op);
                return;
            }

            IWorldAccessor world = inventory.Api.World;

            if (sourceSlot.Itemstack.Collectible is ILiquidSource source && source.AllowHeldLiquidTransfer)
            {
                ItemSlotLiquidOnly liquidSlot = inventory[1] as ItemSlotLiquidOnly;
                
                ItemStack bucketContents = source.GetContent(sourceSlot.Itemstack);
                bool stackable = !liquidSlot.Empty && liquidSlot.Itemstack.Equals(world, bucketContents, GlobalConstants.IgnoredStackAttributes);

                if ((liquidSlot.Empty || stackable) && bucketContents != null)
                {
                    ItemStack bucketStack = sourceSlot.Itemstack;

                    var lprops = BlockLiquidContainerBase.GetContainableProps(bucketContents);

                    float toMoveLitres = op.CtrlDown ? source.TransferSizeLitres : source.CapacityLitres;
                    float curSourceLitres = bucketContents.StackSize / lprops.ItemsPerLitre * bucketStack.StackSize;
                    float curDestLitres = liquidSlot.StackSize / lprops.ItemsPerLitre;

                    // Cap by source amount
                    toMoveLitres = Math.Min(toMoveLitres, curSourceLitres);
                    // Cap by target capacity
                    toMoveLitres = Math.Min(toMoveLitres, liquidSlot.CapacityLitres - curDestLitres);

                    if (toMoveLitres > 0)
                    {
                        int moveQuantity = (int)(toMoveLitres * lprops.ItemsPerLitre);
                        ItemStack takenContentStack = source.TryTakeContent(bucketStack, moveQuantity / bucketStack.StackSize);

                        takenContentStack.StackSize *= bucketStack.StackSize;
                        takenContentStack.StackSize += liquidSlot.StackSize;
                        
                        liquidSlot.Itemstack = takenContentStack;
                        liquidSlot.MarkDirty();
                        op.MovedQuantity = moveQuantity;

                        var pos = op.ActingPlayer?.Entity?.Pos;
                        if (pos != null) op.World.PlaySoundAt(lprops.FillSound, pos.X, pos.Y, pos.Z);
                    }

                    return;
                }

                return;
            }



            string contentItemCode = sourceSlot.Itemstack?.ItemAttributes?["contentItemCode"].AsString();
            if (contentItemCode != null)
            {
                ItemSlot liquidSlot = inventory[1];

                ItemStack contentStack = new ItemStack(world.GetItem(new AssetLocation(contentItemCode)));
                bool stackable = !liquidSlot.Empty && liquidSlot.Itemstack.Equals(world, contentStack, GlobalConstants.IgnoredStackAttributes);

                if ((liquidSlot.Empty || stackable) && contentStack != null)
                {
                    if (stackable) liquidSlot.Itemstack.StackSize++;
                    else liquidSlot.Itemstack = contentStack;

                    liquidSlot.MarkDirty();

                    ItemStack bowlStack = new ItemStack(world.GetBlock(new AssetLocation(sourceSlot.Itemstack.ItemAttributes["emptiedBlockCode"].AsString())));
                    if (sourceSlot.StackSize == 1)
                    {
                        sourceSlot.Itemstack = bowlStack;
                    }
                    else
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

            base.ActivateSlotLeftClick(sourceSlot, ref op);

        }


    }

}
