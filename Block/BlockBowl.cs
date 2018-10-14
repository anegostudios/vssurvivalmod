using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class BlockBowl : Block
    {
        public string BowlContents()
        {
            string part = LastCodePart();
            if (part == "raw" || part == "burned") return null;
            return part;
        }

        public override void OnHeldInteractStart(IItemSlot slot, IEntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandHandling handHandling)
        {
            if (blockSel == null)
            {
                base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, ref handHandling);
                return;
            }

            BlockBucket blockbucket = byEntity.World.BlockAccessor.GetBlock(blockSel.Position) as BlockBucket;
            string contents = BowlContents();

            if (blockbucket != null)
            {
                if (contents == null)
                {
                    ItemStack stack = blockbucket.GetContent(byEntity.World, blockSel.Position);
                    if (stack?.Collectible.Code.Path == "honeyportion")
                    {
                        InsertHoney(slot, byEntity);
                        blockbucket.TryTakeContent(byEntity.World, blockSel.Position, 1);
                    }

                }
                else
                {
                    ItemStack stack = blockbucket.GetContent(byEntity.World, blockSel.Position);
                    if (stack == null || stack.Collectible.Code.Path == "honeyportion")
                    {
                        
                        Item honeyitem = byEntity.World.GetItem(new AssetLocation("honeyportion"));
                        if (blockbucket.TryAddContent(byEntity.World, blockSel.Position, new ItemStack(honeyitem), 1) > 0)
                        {
                            TakeoutHoney(slot, byEntity);
                        }
                    }
                }

                handHandling = EnumHandHandling.PreventDefaultAction;
                return;
            }

            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, ref handHandling);
        }

        public override bool OnHeldInteractStep(float secondsUsed, IItemSlot slot, IEntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (blockSel != null && (byEntity.World.BlockAccessor.GetBlock(blockSel.Position) as BlockBucket) != null)
            {
                return false;
            }

            return base.OnHeldInteractStep(secondsUsed, slot, byEntity, blockSel, entitySel);
        }


        private void TakeoutHoney(IItemSlot itemslot, IEntityAgent byEntity)
        {
            Block emptyBowl = byEntity.World.GetBlock(new AssetLocation("bowl-burned"));
            ItemStack emptyStack = new ItemStack(emptyBowl);

            if (itemslot.Itemstack.StackSize <= 1)
            {
                itemslot.Itemstack = emptyStack;
            }
            else
            {
                IPlayer player = (byEntity as EntityPlayer)?.Player;

                itemslot.TakeOut(1);
                if (!player.InventoryManager.TryGiveItemstack(emptyStack, true))
                {
                    byEntity.World.SpawnItemEntity(emptyStack, byEntity.LocalPos.XYZ);
                }
            }

            itemslot.MarkDirty();
        }


        private void InsertHoney(IItemSlot itemslot, IEntityAgent byEntity)
        {
            Block emptyBowl = byEntity.World.GetBlock(new AssetLocation("bowl-honey"));
            ItemStack emptyStack = new ItemStack(emptyBowl);

            if (itemslot.Itemstack.StackSize <= 1)
            {
                itemslot.Itemstack = emptyStack;
            }
            else
            {
                IPlayer player = (byEntity as EntityPlayer)?.Player;

                itemslot.TakeOut(1);
                if (!player.InventoryManager.TryGiveItemstack(emptyStack, true))
                {
                    byEntity.World.SpawnItemEntity(emptyStack, byEntity.LocalPos.XYZ);
                }
            }

            itemslot.MarkDirty();
        }


        
    }
}
