using System;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class ItemKnife : Item
    {
        public override void OnHeldInteractStart(IItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandHandling handling)
        {
            EntityBehaviorHarvestable bh;
            if (byEntity.Controls.Sneak && entitySel != null && !entitySel.Entity.Alive && (bh = entitySel.Entity.GetBehavior<EntityBehaviorHarvestable>()) != null)
            {
                if (!bh.IsHarvested)
                {
                    byEntity.World.PlaySoundAt(new AssetLocation("sounds/player/scrape"), entitySel.Entity, (byEntity as EntityPlayer)?.Player, false, 12);
                    handling = EnumHandHandling.PreventDefault;
                    return;
                }
            }

            handling = EnumHandHandling.NotHandled;
        }

        public override bool OnHeldInteractStep(float secondsUsed, IItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            EntityBehaviorHarvestable bh;
            if (entitySel != null && (bh = entitySel.Entity.GetBehavior<EntityBehaviorHarvestable>()) != null)
            {
                if (bh.IsHarvested) return false;

                if (byEntity.World.Side == EnumAppSide.Client)
                {
                    ModelTransform tf = new ModelTransform();
                    tf.EnsureDefaultValues();

                    tf.Translation.Set(0, 0, Math.Min(0.6f, secondsUsed * 2));
                    tf.Rotation.Y = Math.Min(20, secondsUsed * 90 * 2f);

                    if (secondsUsed > 0.4f)
                    {
                        tf.Translation.X += (float)Math.Cos(secondsUsed * 15) / 10;
                        tf.Translation.Z += (float)Math.Sin(secondsUsed * 5) / 30;
                    }

                    byEntity.Controls.UsingHeldItemTransformBefore = tf;
                }

                

                return secondsUsed < bh.HarvestDuration;
            }

            return false;
        }

        public override void OnHeldInteractStop(float secondsUsed, IItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            EntityBehaviorHarvestable bh;
            if (entitySel != null && (bh = entitySel.Entity.GetBehavior<EntityBehaviorHarvestable>()) != null && secondsUsed >= bh.HarvestDuration - 0.1f)
            {
                if (byEntity.World.Side == EnumAppSide.Server)
                {
                    bh.SetHarvested((byEntity as EntityPlayer)?.Player);
                }
            }
        }


        public override void OnHeldAttackStart(IItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandHandling handling)
        {
            
        }


        public override bool OnHeldAttackCancel(float secondsPassed, IItemSlot slot, EntityAgent byEntity, BlockSelection blockSelection, EntitySelection entitySel, EnumItemUseCancelReason cancelReason)
        {
            return false;
        }


        public override bool OnHeldAttackStep(float secondsPassed, IItemSlot slot, EntityAgent byEntity, BlockSelection blockSelection, EntitySelection entitySel)
        {
            return false;
        }


        public override void OnHeldAttackStop(float secondsPassed, IItemSlot slot, EntityAgent byEntity, BlockSelection blockSelection, EntitySelection entitySel)
        {
            
        }
    }
}
