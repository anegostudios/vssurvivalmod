using System;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class ItemWoodenClub : Item
    {
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
                    byPlayer.Entity.World.PlaySoundAt(new AssetLocation("sounds/player/strike"), byPlayer, byPlayer, true, 16, 0.2f);
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
            if (byEntity.World.Side == EnumAppSide.Client)
            {
                IClientWorldAccessor world = byEntity.World as IClientWorldAccessor;
                ModelTransform tf = new ModelTransform();
                tf.EnsureDefaultValues();

                tf.Rotation.X = Math.Min(60, secondsPassed * 360);
                if (secondsPassed > 0.3)
                {
                    tf.Translation.Z -= Math.Min(1.5f, 36f * (secondsPassed - 0.3f));
                    tf.Rotation.X -= Math.Max(-40, secondsPassed * 500);
                }


                byEntity.Controls.UsingHeldItemTransformAfter = tf;

                if (secondsPassed > 0.43f && byEntity.Attributes.GetInt("didattack") == 0)
                {
                    world.TryAttackEntity(entitySel);
                    byEntity.Attributes.SetInt("didattack", 1);
                    world.ShakeCamera(0.25f);
                }
            }

            return secondsPassed < 0.9f;
        }

        public override void OnHeldAttackStop(float secondsPassed, IItemSlot slot, IEntityAgent byEntity, BlockSelection blockSelection, EntitySelection entitySel)
        {

        }
    }
}
