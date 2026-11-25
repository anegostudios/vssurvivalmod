using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class ItemHoneyComb : Item
    {
        public float ContainedHoneyLitres = 0.2f;

        [Obsolete("Use the overload that takes an actor for claim validation.", true)]
        public bool CanSqueezeInto(Block block, BlockSelection? blockSel)
        {
            if (blockSel == null) return block is BlockLiquidContainerTopOpened;
            else return CanSqueezeInto(null, block, blockSel);
        }
        public bool CanSqueezeInto(EntityAgent? byEntity, Block block, BlockSelection blockSel)
        {
            var byPlayer = (byEntity as EntityPlayer)?.Player;

            if (block is BlockLiquidContainerTopOpened blockLiquidContainer)
            {
                if (blockLiquidContainer.IsFull(blockSel.Position)) return false;

                return byPlayer == null || byEntity!.World.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.Use);
            }

            if (block is BlockBarrel barrel && api.World.BlockAccessor.GetBlockEntity(blockSel.Position) is BlockEntityBarrel beb)
            {
                if (beb.Sealed || barrel.IsFull(blockSel.Position)) return false;

                return byPlayer == null || byEntity!.World.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.Use);
            }

            if (api.World.BlockAccessor.GetBlockEntity(blockSel.Position) is BlockEntityGroundStorage beg)
            {
                var squeezeIntoSlot = beg.GetSlotAt(blockSel);
                if (squeezeIntoSlot?.Itemstack?.Block is BlockLiquidContainerTopOpened itemLiquidContainer)
                {
                    if (itemLiquidContainer.IsFull(squeezeIntoSlot.Itemstack)) return false;

                    return byPlayer == null || byEntity!.World.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.Use);
                }
            }

            return false;
        }

        WorldInteraction[]? interactions;

        public override void OnLoaded(ICoreAPI api)
        {
            if (api.Side != EnumAppSide.Client) return;

            interactions = ObjectCacheUtil.GetOrCreate(api, "honeyCombInteractions", () =>
            {
                List<ItemStack> stacks = new List<ItemStack>();

                foreach (Block block in api.World.Blocks)
                {
                    if (block.Code == null) continue;

                    if (block is BlockBarrel)
                    {
                        stacks.Add(new ItemStack(block)); // Reliant on CanSqueezeInto allowing barrels. We check if barrel is sealed with world position
                    }


                    if (block is BlockLiquidContainerTopOpened)
                    {
                        stacks.Add(new ItemStack(block));
                    }
                }

                return new WorldInteraction[]
                {
                    new WorldInteraction()
                    {
                        ActionLangCode = "heldhelp-squeeze",
                        HotKeyCode = "shift",
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = stacks.ToArray()
                    }
                };
            });
        }



        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            if (blockSel?.Block != null && CanSqueezeInto(byEntity, blockSel.Block, blockSel) && byEntity.Controls.ShiftKey)
            {
                handling = EnumHandHandling.PreventDefault;
                if (api.World.Side == EnumAppSide.Client)
                {
                    byEntity.World.PlaySoundAt(new AssetLocation("sounds/player/squeezehoneycomb"), byEntity, null, true, 16, 0.5f);
                }
            }
            else
            {
                base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
            }
        }

        public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (blockSel?.Block != null && CanSqueezeInto(byEntity, blockSel.Block, blockSel))
            {
                if (!byEntity.Controls.ShiftKey) return false;
                if (byEntity.World is IClientWorldAccessor)
                {
                    byEntity.StartAnimation("squeezehoneycomb");
                }

                return secondsUsed < 2f;
            }

            return base.OnHeldInteractStep(secondsUsed, slot, byEntity, blockSel, entitySel);
        }

        public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity,
            BlockSelection blockSel, EntitySelection entitySel)
        {
            byEntity.StopAnimation("squeezehoneycomb");

            if (blockSel != null)
            {
                Block block = byEntity.World.BlockAccessor.GetBlock(blockSel.Position);
                if (CanSqueezeInto(byEntity, block, blockSel))
                {
                    if (secondsUsed < 1.9f) return;

                    IWorldAccessor world = byEntity.World;

                    ItemStack honeyStack = new ItemStack(world.GetItem(new AssetLocation("honeyportion")), 99999);

                    BlockLiquidContainerTopOpened? blockCnt = block as BlockLiquidContainerTopOpened;
                    if (blockCnt != null)
                    {
                        if (blockCnt.TryPutLiquid(blockSel.Position, honeyStack, ContainedHoneyLitres) == 0) return;
                    }
                    else

                    if (block is BlockBarrel blockBarrel && api.World.BlockAccessor.GetBlockEntity(blockSel.Position) is BlockEntityBarrel beb)
                    {
                        if (beb.Sealed) return;
                        if (blockBarrel.TryPutLiquid(blockSel.Position, honeyStack, ContainedHoneyLitres) == 0) return;
                    }
                    else
                    {
                        if (api.World.BlockAccessor.GetBlockEntity(blockSel.Position) is BlockEntityGroundStorage beg)
                        {
                            ItemSlot squeezeIntoSlot = beg.GetSlotAt(blockSel);
                            if (squeezeIntoSlot != null && squeezeIntoSlot.Itemstack?.Block != null) {
                                blockCnt = squeezeIntoSlot.Itemstack.Block as BlockLiquidContainerTopOpened;
                                blockCnt?.TryPutLiquid(squeezeIntoSlot.Itemstack, honeyStack, ContainedHoneyLitres);
                                beg.MarkDirty(true);
                            }
                        }
                    }

                    slot.TakeOut(1);
                    slot.MarkDirty();

                    var byPlayer = (byEntity as EntityPlayer)?.Player;
                    ItemStack stack = new ItemStack(world.GetItem(new AssetLocation("beeswax")));
                    if (byPlayer?.InventoryManager.TryGiveItemstack(stack) == false)
                    {
                        byEntity.World.SpawnItemEntity(stack, byEntity.Pos.XYZ);
                    }

                    return;
                }
            }
            base.OnHeldInteractStop(secondsUsed, slot, byEntity, blockSel, entitySel);
        }

        public override bool OnHeldInteractCancel(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, EnumItemUseCancelReason cancelReason)
        {
            byEntity.StopAnimation("squeezehoneycomb");
            return base.OnHeldInteractCancel(secondsUsed, slot, byEntity, blockSel, entitySel, cancelReason);
        }


        public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
        {
            return interactions.Append(base.GetHeldInteractionHelp(inSlot));
        }

    }
}
