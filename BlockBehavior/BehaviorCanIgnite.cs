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

        public override void OnHeldInteractStart(IItemSlot slot, IEntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandHandling handHandling, ref EnumHandling blockHandling)
        {
            if (blockSel == null) return;
            Block block = byEntity.World.BlockAccessor.GetBlock(blockSel.Position);

            if (!byEntity.Controls.Sneak)
            {
                EnumHandling igniteHandled = EnumHandling.NotHandled;
                bool handledResult = block.OnTryIgniteBlock(byEntity, blockSel.Position, 0, ref igniteHandled);

                if (igniteHandled == EnumHandling.NotHandled) return;
            }
            


            Block freeBlock = byEntity.World.BlockAccessor.GetBlock(blockSel.Position.AddCopy(blockSel.Face));
            if (freeBlock.BlockId != 0) return;

            blockHandling = EnumHandling.PreventDefault;
            handHandling = EnumHandHandling.PreventDefault;

            IPlayer byPlayer = (byEntity as EntityPlayer)?.Player;
            byEntity.World.PlaySoundAt(new AssetLocation("sounds/torch-ignite"), byEntity, byPlayer, false, 16);
        }

        public override bool OnHeldInteractStep(float secondsUsed, IItemSlot slot, IEntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandling handling)
        {
            if (blockSel == null) return false;            

            Block block = byEntity.World.BlockAccessor.GetBlock(blockSel.Position);

            EnumHandling igniteHandled = EnumHandling.NotHandled;
            bool handledResult = block.OnTryIgniteBlock(byEntity, blockSel.Position, secondsUsed, ref igniteHandled);

            if (igniteHandled == EnumHandling.NotHandled && !byEntity.Controls.Sneak) return false;

            handling = EnumHandling.PreventDefault;

            if (byEntity.World is IClientWorldAccessor)
            {
                ModelTransform tf = new ModelTransform();
                tf.EnsureDefaultValues();

                tf.Translation.Set(0, -Math.Min(1.1f / 3, secondsUsed * 4 / 3f), -Math.Min(1.1f, secondsUsed * 4));
                tf.Rotation.X = -Math.Min(85, secondsUsed * 90 * 4f);
                byEntity.Controls.UsingHeldItemTransformBefore = tf;


                if (igniteHandled == EnumHandling.NotHandled && secondsUsed > 0.25f && (int)(30 * secondsUsed) % 2 == 1)
                {
                    
                    Vec3d pos = BlockEntityFire.RandomBlockPos(byEntity.World.BlockAccessor, blockSel.Position, block, blockSel.Face);

                    Block blockFire = byEntity.World.GetBlock(new AssetLocation("fire"));

                    AdvancedParticleProperties props = blockFire.ParticleProperties[blockFire.ParticleProperties.Length - 1];
                    props.basePos = pos;
                    props.Quantity.avg = 1;

                    IPlayer byPlayer = null;
                    if (byEntity is IEntityPlayer) byPlayer = byEntity.World.PlayerByUid(((IEntityPlayer)byEntity).PlayerUID);

                    byEntity.World.SpawnParticles(props, byPlayer);

                    props.Quantity.avg = 0;
                }
            }

            return igniteHandled != EnumHandling.NotHandled ? handledResult : secondsUsed <= 3.1;
        }

        public override void OnHeldInteractStop(float secondsUsed, IItemSlot slot, IEntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandling handling)
        {
            if (blockSel == null) return;

            EnumHandling handled = EnumHandling.NotHandled;
            Block block = byEntity.World.BlockAccessor.GetBlock(blockSel.Position);
            block.OnTryIgniteBlockOver(byEntity, blockSel.Position, secondsUsed, ref handled);

            if (handled != EnumHandling.NotHandled)
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
