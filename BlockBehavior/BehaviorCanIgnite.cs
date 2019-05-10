using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockBehaviorCanIgnite : BlockBehavior
    {
        public BlockBehaviorCanIgnite(Block block) : base(block)
        {
        }

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling, ref EnumHandling blockHandling)
        {
            if (blockSel == null) return;
            Block block = byEntity.World.BlockAccessor.GetBlock(blockSel.Position);

            IPlayer byPlayer = (byEntity as EntityPlayer)?.Player;
            if (!byEntity.World.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak))
            {
                return;
            }


            if (!byEntity.Controls.Sneak)
            {
                EnumHandling igniteHandled = EnumHandling.PassThrough;
                bool handledResult = block.OnTryIgniteBlock(byEntity, blockSel.Position, 0, ref igniteHandled);

                if (igniteHandled == EnumHandling.PassThrough) return;
            }
            


            Block freeBlock = byEntity.World.BlockAccessor.GetBlock(blockSel.Position.AddCopy(blockSel.Face));
            if (freeBlock.BlockId != 0) return;

            blockHandling = EnumHandling.PreventDefault;
            handHandling = EnumHandHandling.PreventDefault;

            
            byEntity.World.PlaySoundAt(new AssetLocation("sounds/torch-ignite"), byEntity, byPlayer, false, 16);
        }

        public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandling handling)
        {
            if (blockSel == null) return false;

            IPlayer byPlayer = (byEntity as EntityPlayer)?.Player;
            if (!byEntity.World.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak))
            {
                return false;
            }


            Block block = byEntity.World.BlockAccessor.GetBlock(blockSel.Position);

            EnumHandling igniteHandled = EnumHandling.PassThrough;
            bool handledResult = block.OnTryIgniteBlock(byEntity, blockSel.Position, secondsUsed, ref igniteHandled);

            if (igniteHandled == EnumHandling.PassThrough && !byEntity.Controls.Sneak) return false;

            handling = EnumHandling.PreventDefault;

            if (byEntity.World is IClientWorldAccessor)
            {
                ModelTransform tf = new ModelTransform();
                tf.EnsureDefaultValues();

                tf.Translation.Set(0, -Math.Min(1.1f / 3, secondsUsed * 4 / 3f), -Math.Min(1.1f, secondsUsed * 4));
                tf.Rotation.X = -Math.Min(85, secondsUsed * 90 * 4f);
                byEntity.Controls.UsingHeldItemTransformBefore = tf;


                if (igniteHandled == EnumHandling.PassThrough && secondsUsed > 0.25f && (int)(30 * secondsUsed) % 2 == 1)
                { 
                    Vec3d pos = BlockEntityFire.RandomBlockPos(byEntity.World.BlockAccessor, blockSel.Position, block, blockSel.Face);

                    Block blockFire = byEntity.World.GetBlock(new AssetLocation("fire"));

                    AdvancedParticleProperties props = blockFire.ParticleProperties[blockFire.ParticleProperties.Length - 1];
                    props.basePos = pos;
                    props.Quantity.avg = 1;

                    byEntity.World.SpawnParticles(props, byPlayer);

                    props.Quantity.avg = 0;
                }
            }

            return igniteHandled != EnumHandling.PassThrough ? handledResult : secondsUsed <= 3.1;
        }

        public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandling handling)
        {
            if (blockSel == null) return;

            EnumHandling handled = EnumHandling.PassThrough;
            Block block = byEntity.World.BlockAccessor.GetBlock(blockSel.Position);
            block.OnTryIgniteBlockOver(byEntity, blockSel.Position, secondsUsed, ref handled);

            if (handled != EnumHandling.PassThrough)
            {
                return;
            }

            handling = EnumHandling.PreventDefault;

            if (secondsUsed >= 3 && blockSel != null)
            {
                BlockPos bpos = blockSel.Position.AddCopy(blockSel.Face);
                block = byEntity.World.BlockAccessor.GetBlock(bpos);

                if (block.BlockId == 0)
                {
                    byEntity.World.BlockAccessor.SetBlock(byEntity.World.GetBlock(new AssetLocation("fire")).BlockId, bpos);

                    BlockEntityFire befire = byEntity.World.BlockAccessor.GetBlockEntity(bpos) as BlockEntityFire;
                    if (befire != null) befire.Init(blockSel.Face);
                }
            }
        }
    }
}
