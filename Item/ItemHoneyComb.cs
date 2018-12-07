using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class ItemHoneyComb : Item
    {
        public bool CanSqueezeInto(Block block)
        {
            return block.Code.Path == "bowl-burned" || block is BlockBucket;
        }

        public override void OnHeldInteractStart(IItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandHandling handling)
        {
            if (blockSel == null) return;

            Block block = byEntity.World.BlockAccessor.GetBlock(blockSel.Position);

            if (byEntity.World is IClientWorldAccessor && CanSqueezeInto(block))
            {
                byEntity.World.PlaySoundAt(new AssetLocation("sounds/player/squeezehoneycomb"), byEntity);
            }

            if (CanSqueezeInto(block))
            {
                handling = EnumHandHandling.PreventDefault;
            }
        }

        public override bool OnHeldInteractStep(float secondsUsed, IItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (blockSel == null) return false;

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

        public override void OnHeldInteractStop(float secondsUsed, IItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (blockSel == null) return;
            if (secondsUsed < 1.9f) return;

            IWorldAccessor world = byEntity.World;

            Block block = byEntity.World.BlockAccessor.GetBlock(blockSel.Position);
            if (!CanSqueezeInto(block)) return;

            BlockBucket blockbucket = block as BlockBucket;
            if (blockbucket != null)
            {
                if (blockbucket.TryAddContent(world, blockSel.Position, new ItemStack(world.GetItem(new AssetLocation("honeyportion"))), 1) == 0) return;
            }
            else
            {
                world.BlockAccessor.SetBlock(world.GetBlock(new AssetLocation("bowl-honey")).BlockId, blockSel.Position);
            }

            slot.TakeOut(1);
            slot.MarkDirty();

            IPlayer byPlayer = null;
            if (byEntity is EntityPlayer) byPlayer = world.PlayerByUid(((EntityPlayer)byEntity).PlayerUID);
            byPlayer?.InventoryManager.TryGiveItemstack(new ItemStack(world.GetItem(new AssetLocation("beeswax"))));
        }

    }
}
