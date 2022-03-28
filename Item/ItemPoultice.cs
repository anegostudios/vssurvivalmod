﻿using System;
using System.Text;
using Vintagestory.API;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class ItemPoultice : Item
    {
        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            byEntity.World.RegisterCallback((dt) =>
            {
                if (byEntity.Controls.HandUse == EnumHandInteract.HeldItemInteract)
                {
                    IPlayer player = null;
                    if (byEntity is EntityPlayer) player = byEntity.World.PlayerByUid(((EntityPlayer)byEntity).PlayerUID);

                    byEntity.World.PlaySoundAt(new AssetLocation("sounds/player/poultice"), byEntity, player);
                }
            }, 200);


            JsonObject attr = slot.Itemstack.Collectible.Attributes;
            if (attr != null && attr["health"].Exists)
            {
                handling = EnumHandHandling.PreventDefault;
                return;
            }

            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
        }

        public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {

            if (byEntity.World is IClientWorldAccessor)
            {
                ModelTransform tf = new ModelTransform();

                tf.EnsureDefaultValues();

                tf.Origin.Set(0f, 0, 0f);

                tf.Translation.X -= Math.Min(1.5f, secondsUsed * 4 * 1.57f);

                //tf.Rotation.X += Math.Min(30f, secondsUsed * 350);
                tf.Rotation.Y += Math.Min(130f, secondsUsed * 350);

                byEntity.Controls.UsingHeldItemTransformAfter = tf;

                return secondsUsed < 0.75f;
            }

            return true;
        }

        public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (secondsUsed > 0.7f && byEntity.World.Side == EnumAppSide.Server)
            {
                JsonObject attr = slot.Itemstack.Collectible.Attributes;
                float health = attr["health"].AsFloat();

                var source = byEntity is EntityPlayer ? EnumDamageSource.Player : EnumDamageSource.Entity;
                var entityToHeal = entitySel?.Entity;
                if (entityToHeal == null)
                {
                    entityToHeal = byEntity;
                    source = EnumDamageSource.Internal;
                }

                if (entityToHeal == null)
                    return;

                entityToHeal.ReceiveDamage(new DamageSource()
                {
                    Source = source,
                    SourceEntity = source == EnumDamageSource.Internal ? null : byEntity,
                    Type = health > 0 ? EnumDamageType.Heal : EnumDamageType.Poison
                }, Math.Abs(health));

                slot.TakeOut(1);
                slot.MarkDirty();
            }
        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

            JsonObject attr = inSlot.Itemstack.Collectible.Attributes;
            if (attr != null && attr["health"].Exists)
            {
                float health = attr["health"].AsFloat();
                dsc.AppendLine(Lang.Get("When used: +{0} hp", health));
            }
        }


        public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
        {
            return new WorldInteraction[] {
                new WorldInteraction()
                {
                    ActionLangCode = "heldhelp-heal",
                    MouseButton = EnumMouseButton.Right,
                }
            }.Append(base.GetHeldInteractionHelp(inSlot));
        }
    }


}