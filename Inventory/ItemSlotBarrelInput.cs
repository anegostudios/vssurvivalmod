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

                WaterTightContainableProps props = BlockLiquidContainerBase.GetInContainerProps(liquidSlot.Itemstack);
                if (props != null)
                {
                    int max = (int)(liquidSlot.CapacityLitres * props.ItemsPerLitre);
                    int maxOverride = props.MaxStackSize;  //allows 64 rot to be placed in barrel
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

        protected override void ActivateSlotLeftClick(ItemSlot sourceSlot, ref ItemStackMoveOperation op)
        {
            if (sourceSlot.Empty)
            {
                base.ActivateSlotLeftClick(sourceSlot, ref op);
                return;
            }

            IWorldAccessor world = inventory.Api.World;

            if (sourceSlot.Itemstack.Collectible is ILiquidSource)
            {
                ItemSlot liquidSlot = inventory[1];
                ILiquidSource source = sourceSlot.Itemstack.Collectible as ILiquidSource;
                
                ItemStack bucketContents = source.GetContent(world, sourceSlot.Itemstack);
                bool stackable = !liquidSlot.Empty && liquidSlot.Itemstack.Equals(world, bucketContents, GlobalConstants.IgnoredStackAttributes);

                if ((liquidSlot.Empty || stackable) && bucketContents != null)
                {
                    ItemStack bucketStack = sourceSlot.Itemstack;
                    ItemStack takenContent = source.TryTakeContent(world, bucketStack, 1);
                    sourceSlot.Itemstack = bucketStack;
                    takenContent.StackSize += liquidSlot.StackSize;
                    liquidSlot.Itemstack = takenContent;
                    liquidSlot.MarkDirty();

                    op.MovedQuantity = 1;

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
