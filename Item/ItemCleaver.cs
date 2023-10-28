using System;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class ItemCleaver : Item
    {
        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
            if (handling == EnumHandHandling.PreventDefault) return;

            if (entitySel != null && entitySel.Entity.Alive && !(entitySel.Entity is EntityArmorStand))
            {
                handling = EnumHandHandling.PreventDefault;

                if (api.World.Side == EnumAppSide.Client)
                {
                    api.ObjectCache["slaughterSoundPlayed"] = false;
                }

                return;
            }

            handling = EnumHandHandling.NotHandled;
        }

        public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (entitySel != null)
            {
                if (byEntity.World.Side == EnumAppSide.Client)
                {
                    ModelTransform tf = new ModelTransform();
                    tf.EnsureDefaultValues();

                    float offset = GameMath.Clamp(secondsUsed * 5, 0, 2f);

                    byEntity.Controls.UsingHeldItemTransformBefore = tf;
                    tf.Origin.Set(1f, 0, 0.5f);
                    tf.Rotation.Set(0, 0, -offset * 15);
                    tf.Translation.Set(0, Math.Min(1f, secondsUsed), 0);


                    if (secondsUsed > 0.9f)
                    {
                        float offset2 = GameMath.Clamp((secondsUsed-0.9f) * 5 * 240, 0, 70f);
                        tf.Rotation.Add(0, 0, offset2);
                        tf.Translation.Add(0, -Math.Min(1f, (secondsUsed - 0.9f) * 10), 0);
                    }
                    if (secondsUsed > 1f)
                    {
                        if (api.ObjectCache.ContainsKey("slaughterSoundPlayed") && (bool)api.ObjectCache["slaughterSoundPlayed"] == false)
                        {
                            byEntity.World.PlaySoundAt(new AssetLocation("sounds/tool/slash"), entitySel.Entity, (byEntity as EntityPlayer)?.Player, false, 12);
                            api.ObjectCache["slaughterSoundPlayed"] = true;
                        }
                    }

                    byEntity.Controls.UsingHeldItemTransformBefore = tf;
                }

                return secondsUsed < 1.15f;
            }

            return false;
        }

        public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (entitySel == null || secondsUsed < 1.05f) return;
            if (entitySel.Entity is EntityPlayer) return;

            int generation = entitySel.Entity.WatchedAttributes.GetInt("generation", 0);

            slot?.Itemstack?.Collectible.DamageItem(byEntity.World, byEntity, slot, 1);

            float slaughterChance = generation / 3f;
            float dmg = 9999;

            if (api.World.Rand.NextDouble() > slaughterChance)
            {
                dmg = 1;
            } 

            entitySel.Entity.ReceiveDamage(new DamageSource()
            {
                DamageTier = 0,
                HitPosition = entitySel.HitPosition,
                Source = EnumDamageSource.Player,
                SourceEntity = byEntity,
                Type = EnumDamageType.SlashingAttack
            }, dmg);

            
        }


        public override bool OnHeldInteractCancel(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, EnumItemUseCancelReason cancelReason)
        {
            //byEntity.World.Logger.Debug("{0} knife interact cancelled, seconds used {1}", byEntity.World.Side, secondsUsed);

            return base.OnHeldInteractCancel(secondsUsed, slot, byEntity, blockSel, entitySel, cancelReason);
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
