using System;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class ItemSword : Item
    {
        public override void OnHeldInteractStart(IItemSlot slot, IEntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandHandling handling)
        {
            if (blockSel == null)
            {
                handling = EnumHandHandling.PreventDefault;
                return;
            }

            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, ref handling);
        }
        public override void OnHeldAttackStart(IItemSlot slot, IEntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandHandling handling)
        {
            if (blockSel != null) return;

            byEntity.Attributes.SetInt("didattack", 0);

            byEntity.World.RegisterCallback((dt) =>
            {
                IPlayer byPlayer = (byEntity as EntityPlayer).Player;
                if (byPlayer == null) return;

                if (byEntity.Controls.HandUse == EnumHandInteract.HeldItemAttack)
                {
                    byPlayer.Entity.World.PlaySoundAt(new AssetLocation("sounds/player/strike"), byPlayer, byPlayer);
                }
            }, 464);

            handling = EnumHandHandling.PreventDefault;
        }

        public override bool OnHeldAttackCancel(float secondsPassed, IItemSlot slot, IEntityAgent byEntity, BlockSelection blockSelection, EntitySelection entitySel, EnumItemUseCancelReason cancelReason)
        {
            return false;
        }

        public override bool OnHeldAttackStep(float secondsPassed, IItemSlot slot, IEntityAgent byEntity, BlockSelection blockSelection, EntitySelection entitySel)
        {
            float backwards = -Math.Min(0.35f, 2 * secondsPassed);
            float stab = Math.Min(1.2f, 20 * Math.Max(0, secondsPassed - 0.35f)); // + Math.Max(0, 5*(secondsPassed - 0.5f));

            if (byEntity.World.Side == EnumAppSide.Client)
            {
                IClientWorldAccessor world = byEntity.World as IClientWorldAccessor;
                ModelTransform tf = new ModelTransform();
                tf.EnsureDefaultValues();

                float sum = stab + backwards;
                float ztranslation = Math.Min(0.2f, 1.5f * secondsPassed);
                float easeout = Math.Max(0, 10 * (secondsPassed - 1));

                if (secondsPassed > 0.4f) sum = Math.Max(0, sum - easeout);
                ztranslation = Math.Max(0, ztranslation - easeout);

                tf.Translation.Set(sum * 0.8f, 2.5f * sum / 3, -ztranslation);
                tf.Rotation.Set(sum * 10, sum * 2, sum * 25);

                byEntity.Controls.UsingHeldItemTransformBefore = tf;

                if (stab > 1.15f && byEntity.Attributes.GetInt("didattack") == 0)
                {
                    world.TryAttackEntity(entitySel);
                    byEntity.Attributes.SetInt("didattack", 1);
                    world.ShakeCamera(0.25f);
                }
            }



            return secondsPassed < 1.2f;
        }

        public override void OnHeldAttackStop(float secondsPassed, IItemSlot slot, IEntityAgent byEntity, BlockSelection blockSelection, EntitySelection entitySel)
        {

        }
    }
}
