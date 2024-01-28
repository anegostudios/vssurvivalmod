using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class ItemCleaver : ItemSword
    {
        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            strikeSound = new AssetLocation("sounds/tool/slash");
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
                startAttack(slot, byEntity);
                return;
            }

            handling = EnumHandHandling.NotHandled;
        }

        public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (entitySel != null)
            {
                return stepAttack(slot, byEntity);
            }

            return false;
        }

        protected override void playStrikeSound(EntityAgent byEntity)
        {
            IPlayer byPlayer = (byEntity as EntityPlayer).Player;
            if (byPlayer == null) return;

            if (byEntity.Controls.HandUse == EnumHandInteract.HeldItemInteract)
            {
                byPlayer.Entity.World.PlaySoundAt(strikeSound, byPlayer.Entity, byPlayer, 0.9f + (float)api.World.Rand.NextDouble() * 0.2f, 16, 0.35f);
            }
        }

        protected override void hitEntity(EntityAgent byEntity)
        {
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
