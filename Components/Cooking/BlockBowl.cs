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
        public string BowlContentItemCode()
        {
            return Attributes["contentItemCode"].AsString();
        }
        
        public Block ContentBlockForContents(string contents)
        {
            if (Attributes["contentItem2BlockCodes"][contents]?.Exists != true) return null;

            return api.World.GetBlock(new AssetLocation(Attributes["contentItem2BlockCodes"][contents].AsString()));
        }

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling)
        {
            if (blockSel == null)
            {
                base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handHandling);
                return;
            }

            BlockBucket blockbucket = byEntity.World.BlockAccessor.GetBlock(blockSel.Position) as BlockBucket;
            string contents = BowlContentItemCode();

            if (blockbucket != null)
            {
                if (contents == null)
                {
                    ItemStack stack = blockbucket.GetContent(byEntity.World, blockSel.Position);
                    if (stack != null && ContentBlockForContents(stack.Collectible.Code.Path) !=null)
                    {
                        InsertIntoBowl(slot, byEntity, stack.Collectible.Code.Path);
                        blockbucket.TryTakeContent(byEntity.World, blockSel.Position, 1);
                    }
                }
                else
                {
                    ItemStack stack = blockbucket.GetContent(byEntity.World, blockSel.Position);
                    if (stack == null || stack.Collectible.Code.Equals(new AssetLocation(BowlContentItemCode())))
                    {
                        Item contentItem = byEntity.World.GetItem(new AssetLocation(BowlContentItemCode()));
                        if (blockbucket.TryAddContent(byEntity.World, blockSel.Position, new ItemStack(contentItem), 1) > 0)
                        {
                            EmptyOutBowl(slot, byEntity);
                        }
                    }
                }

                handHandling = EnumHandHandling.PreventDefaultAction;
                return;
            }

            handHandling = EnumHandHandling.PreventDefault;

            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handHandling);
        }

        public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (blockSel != null && (byEntity.World.BlockAccessor.GetBlock(blockSel.Position) as BlockBucket) != null)
            {
                return false;
            }

            return base.OnHeldInteractStep(secondsUsed, slot, byEntity, blockSel, entitySel);
        }


        private void EmptyOutBowl(ItemSlot itemslot, EntityAgent byEntity)
        {
            Block emptyBowl = byEntity.World.GetBlock(new AssetLocation(Attributes["emptiedBlockCode"].AsString()));
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


        private void InsertIntoBowl(ItemSlot itemslot, EntityAgent byEntity, string contents)
        {
            Block filledBowl = ContentBlockForContents(contents);
            ItemStack stack = new ItemStack(filledBowl);

            if (itemslot.Itemstack.StackSize <= 1)
            {
                itemslot.Itemstack = stack;
            }
            else
            {
                IPlayer player = (byEntity as EntityPlayer)?.Player;

                itemslot.TakeOut(1);
                if (!player.InventoryManager.TryGiveItemstack(stack, true))
                {
                    byEntity.World.SpawnItemEntity(stack, byEntity.LocalPos.XYZ);
                }
            }

            itemslot.MarkDirty();
        }


        
    }
}
