using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace Vintagestory.GameContent
{
    public class ItemSword : Item
    {
        protected AssetLocation strikeSound = new AssetLocation("sounds/player/strike");

        public static float getHitDamageAtFrame(EntityAgent byEntity, string animCode)
        {
            if (byEntity.Properties.Client.AnimationsByMetaCode.TryGetValue(animCode, out var animdata))
            {
                if (animdata.Attributes?["damageAtFrame"].Exists == true)
                {
                    return animdata.Attributes["damageAtFrame"].AsFloat(-1) / animdata.AnimationSpeed;
                }

            }
            return -1;
        }
        public static float getSoundAtFrame(EntityAgent byEntity, string animCode)
        {
            if (byEntity.Properties.Client.AnimationsByMetaCode.TryGetValue(animCode, out var animdata))
            {
                if (animdata.Attributes?["soundAtFrame"].Exists == true)
                {
                    return animdata.Attributes["soundAtFrame"].AsFloat(-1) / animdata.AnimationSpeed;
                }
            }
            return -1;
        }

        public override string GetHeldTpUseAnimation(ItemSlot activeHotbarSlot, Entity byEntity)
        {
            return "interactstatic";
        }
        public override void OnHeldAttackStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandHandling handling)
        {
            startAttack(slot, byEntity);
            handling = EnumHandHandling.PreventDefault;
        }

        public override bool OnHeldAttackCancel(float secondsPassed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSelection, EntitySelection entitySel, EnumItemUseCancelReason cancelReason)
        {
            return false;
        }
        public override bool OnHeldAttackStep(float secondsPassed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSelection, EntitySelection entitySel)
        {
            return stepAttack(slot, byEntity);
        }
        public override void OnHeldAttackStop(float secondsPassed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSelection, EntitySelection entitySel)
        {

        }


        protected void startAttack(ItemSlot slot, EntityAgent byEntity)
        {
            string anim = GetHeldTpHitAnimation(slot, byEntity);

            byEntity.Attributes.SetInt("didattack", 0);

            byEntity.AnimManager.RegisterFrameCallback(new AnimFrameCallback()
            {
                Animation = anim,
                Frame = getSoundAtFrame(byEntity, anim),
                Callback = () => playStrikeSound(byEntity)
            });

            byEntity.AnimManager.RegisterFrameCallback(new AnimFrameCallback()
            {
                Animation = anim,
                Frame = getHitDamageAtFrame(byEntity, anim),
                Callback = () => hitEntity(byEntity)
            });
        }

   
     
        protected bool stepAttack(ItemSlot slot, EntityAgent byEntity)
        {
            string animCode = GetHeldTpHitAnimation(slot, byEntity);

            return byEntity.AnimManager.IsAnimationActive(animCode);
        }



        protected virtual void playStrikeSound(EntityAgent byEntity)
        {
            IPlayer byPlayer = (byEntity as EntityPlayer).Player;
            if (byPlayer == null) return;

            if (byEntity.Controls.HandUse == EnumHandInteract.HeldItemAttack)
            {
                byPlayer.Entity.World.PlaySoundAt(strikeSound, byPlayer.Entity, byPlayer, 0.9f + (float)api.World.Rand.NextDouble() * 0.2f, 16, 0.35f);
            }
        }

        protected virtual void hitEntity(EntityAgent byEntity)
        {
            var entitySel = (byEntity as EntityPlayer)?.EntitySelection;

            if (byEntity.World.Side == EnumAppSide.Client)
            {
                IClientWorldAccessor world = byEntity.World as IClientWorldAccessor;

                if (byEntity.Attributes.GetInt("didattack") == 0)
                {
                    if (entitySel != null) world.TryAttackEntity(entitySel);
                    byEntity.Attributes.SetInt("didattack", 1);
                    world.AddCameraShake(0.25f);
                }
            }
            else
            {
                if (byEntity.Attributes.GetInt("didattack") == 0 && entitySel != null)
                {
                    byEntity.Attributes.SetInt("didattack", 1);
                }
            }
        }
    }
}
