using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class ItemHoneyComb : Item
    {
        public bool CanSqueezeInto(Block block, BlockPos pos)
        {
            if (block is BlockBucket || block?.Attributes?["contentItem2BlockCodes"]?["honeyportion"].Exists == true) return true;

            if (pos != null)
            {
                var beg = api.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityGroundStorage;
                if (beg != null)
                {
                    ItemSlot squeezeIntoSlot = beg.Inventory.FirstOrDefault(slot => slot.Itemstack?.Block != null && CanSqueezeInto(slot.Itemstack.Block, null));
                    return squeezeIntoSlot != null;
                }
            }

            return false;
        }

        WorldInteraction[] interactions;

        public override void OnLoaded(ICoreAPI api)
        {
            if (api.Side != EnumAppSide.Client) return;
            ICoreClientAPI capi = api as ICoreClientAPI;

            interactions = ObjectCacheUtil.GetOrCreate(api, "honeyCombInteractions", () =>
            {
                List<ItemStack> stacks = new List<ItemStack>();

                foreach (Block block in api.World.Blocks)
                {
                    if (block.Code == null) continue;

                    if (CanSqueezeInto(block, null))
                    {
                        stacks.Add(new ItemStack(block));
                    }
                }

                return new WorldInteraction[]
                {
                    new WorldInteraction()
                    {
                        ActionLangCode = "heldhelp-squeeze",
                        HotKeyCode = "sneak",
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = stacks.ToArray()
                    }
                };
            });
        }



        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            if (blockSel == null || !byEntity.Controls.Sneak) return;

            Block block = byEntity.World.BlockAccessor.GetBlock(blockSel.Position);

            if (CanSqueezeInto(block, blockSel.Position))
            {
                handling = EnumHandHandling.PreventDefault;
                if (api.World.Side == EnumAppSide.Client)
                {
                    byEntity.World.PlaySoundAt(new AssetLocation("sounds/player/squeezehoneycomb"), byEntity, null, true, 16, 0.5f);
                }
            }
        }

        public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (blockSel == null || !byEntity.Controls.Sneak) return false;

            if (byEntity.World is IClientWorldAccessor)
            {
                ModelTransform tf = new ModelTransform();
                tf.EnsureDefaultValues();
                
                tf.Translation.Set(Math.Min(0.6f, secondsUsed * 2), 0, 0); //-Math.Min(1.1f / 3, secondsUsed * 4 / 3f)
                tf.Rotation.Y = Math.Min(20, secondsUsed * 90 * 2f);

                if (secondsUsed > 0.4f)
                {
                    tf.Translation.X += (float)Math.Sin(secondsUsed * 30) / 10;
                }

                byEntity.Controls.UsingHeldItemTransformBefore = tf;
            }

            return secondsUsed < 2f;
        }

        public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (blockSel == null) return;
            if (secondsUsed < 1.9f) return;

            IWorldAccessor world = byEntity.World;

            Block block = byEntity.World.BlockAccessor.GetBlock(blockSel.Position);
            if (!CanSqueezeInto(block, blockSel.Position)) return;

            BlockBucket blockbucket = block as BlockBucket;
            if (blockbucket != null)
            {
                if (blockbucket.TryPutContent(world, blockSel.Position, new ItemStack(world.GetItem(new AssetLocation("honeyportion"))), 1) == 0) return;
            }
            else
            {
                AssetLocation loc = null;

                if (block.Attributes?["contentItem2BlockCodes"].Exists == true)
                {
                    loc = new AssetLocation(block.Attributes?["contentItem2BlockCodes"]["honeyportion"].AsString());
                }

                if (loc == null)
                {
                    var beg = api.World.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityGroundStorage;
                    if (beg != null)
                    {
                        ItemSlot squeezeIntoSlot = beg.Inventory.FirstOrDefault(gslot => gslot.Itemstack?.Block != null && CanSqueezeInto(gslot.Itemstack.Block, null));
                        if (squeezeIntoSlot != null)
                        {
                            loc = new AssetLocation(squeezeIntoSlot.Itemstack.ItemAttributes?["contentItem2BlockCodes"]["honeyportion"].AsString());
                            squeezeIntoSlot.Itemstack = new ItemStack(world.GetBlock(loc));
                            beg.MarkDirty(true);
                        }
                    }
                }
                else
                {
                    world.BlockAccessor.SetBlock(world.GetBlock(loc).BlockId, blockSel.Position);
                }
            }

            slot.TakeOut(1);
            slot.MarkDirty();

            IPlayer byPlayer = null;
            if (byEntity is EntityPlayer) byPlayer = world.PlayerByUid(((EntityPlayer)byEntity).PlayerUID);
            ItemStack stack = new ItemStack(world.GetItem(new AssetLocation("beeswax")));
            if (byPlayer?.InventoryManager.TryGiveItemstack(stack) == false)
            {
                byEntity.World.SpawnItemEntity(stack, byEntity.SidedPos.XYZ);
            }
        }


        public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
        {
            return interactions.Append(base.GetHeldInteractionHelp(inSlot));
        }

    }
}
