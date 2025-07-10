using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent
{
    public class ItemCleaver : Item
    {
        CollectibleBehaviorAnimationAuthoritative bhaa;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            bhaa = GetCollectibleBehavior<CollectibleBehaviorAnimationAuthoritative>(true);

            if (bhaa == null)
            {
                api.World.Logger.Warning("Cleaver {0} uses ItemCleaver class, but lacks required AnimationAuthoritative behavior. I'll take the freedom to add this behavior, but please fix json item type.", Code);
                bhaa = new CollectibleBehaviorAnimationAuthoritative(this);
                bhaa.OnLoaded(api);
                CollectibleBehaviors = CollectibleBehaviors.Append(bhaa);
            }

            bhaa.strikeSoundHandInteract = EnumHandInteract.HeldItemInteract;
            bhaa.OnBeginHitEntity += Bhaa_OnBeginHitEntity;
        }


        public override void OnHeldAttackStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandHandling handling)
        {
            handling = EnumHandHandling.PreventDefault;
        }

        public override bool OnHeldAttackStep(float secondsPassed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSelection, EntitySelection entitySel)
        {
            return false;
        }

        public override string GetHeldTpUseAnimation(ItemSlot activeHotbarSlot, Entity byEntity)
        {
            return "cleaverhit";
        }

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
            if (handling == EnumHandHandling.PreventDefault) return;

            if (entitySel != null && entitySel.Entity.Alive && !(entitySel.Entity is EntityArmorStand))
            {
                handling = EnumHandHandling.PreventDefault;
                bhaa.StartAttack(slot, byEntity);
                return;
            }

            handling = EnumHandHandling.NotHandled;
        }

        public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (entitySel != null)
            {
                return bhaa.StepAttack(slot, byEntity);
            }

            return false;
        }


        private void Bhaa_OnBeginHitEntity(EntityAgent byEntity, ref EnumHandling handling)
        {
            handling = EnumHandling.PreventDefault;

            var entitySel = (byEntity as EntityPlayer)?.EntitySelection;
            var slot = (byEntity as EntityPlayer)?.Player.InventoryManager.ActiveHotbarSlot;

            if (entitySel == null || entitySel.Entity is EntityPlayer) return;   // entitySel can occasionally be null, especially if the player is moving the camera a lot while attacking

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


    }
}
