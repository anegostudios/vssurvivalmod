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
        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
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

        public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
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

                

                return secondsUsed < bh.HarvestDuration + 0.15f;
            }

            return false;
        }

        public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (entitySel == null) return;

            EntityBehaviorHarvestable bh = entitySel.Entity.GetBehavior<EntityBehaviorHarvestable>();

            //byEntity.World.Logger.Debug("{0} knife interact stop, seconds used {1} / {2}", byEntity.World.Side, secondsUsed, bh?.HarvestDuration);

            if (bh != null && secondsUsed >= bh.HarvestDuration - 0.1f)
            {
                bh.SetHarvested((byEntity as EntityPlayer)?.Player);
                slot?.Itemstack?.Collectible.DamageItem(byEntity.World, byEntity, slot, 3);
            }
        }


        public override void OnHeldAttackStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandHandling handling)
        {
            
        }


        public override bool OnHeldAttackCancel(float secondsPassed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSelection, EntitySelection entitySel, EnumItemUseCancelReason cancelReason)
        {
            return false;
        }


        public override bool OnHeldAttackStep(float secondsPassed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSelection, EntitySelection entitySel)
        {
            return false;
        }


        public override void OnHeldAttackStop(float secondsPassed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSelection, EntitySelection entitySel)
        {
            
        }
    }
}
