using System;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent
{
    public class ItemPoultice : Item, ICanHealCreature
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
            return secondsUsed < 0.7f + (byEntity.World.Side == EnumAppSide.Client ? 0.3f : 0);
        }

        public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (secondsUsed > 0.7f && byEntity.World.Side == EnumAppSide.Server)
            {
                JsonObject attr = slot.Itemstack.Collectible.Attributes;
                float health = attr["health"].AsFloat();

                Entity targetEntity = byEntity;

                var ebh = entitySel?.Entity?.GetBehavior<EntityBehaviorHealth>();
                if (byEntity.Controls.CtrlKey && !byEntity.Controls.Forward && !byEntity.Controls.Backward && !byEntity.Controls.Left && !byEntity.Controls.Right && ebh?.IsHealable(byEntity) == true)
                {
                    targetEntity = entitySel.Entity;
                }

                if (health > 0)
                {
                    float healingEffectivness = targetEntity.Stats.GetBlended("healingeffectivness");
                    health *= Math.Max(0, healingEffectivness);
                }

                targetEntity.ReceiveDamage(new DamageSource()
                {
                    Source = EnumDamageSource.Internal,
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


        public bool CanHeal(Entity entity)
        {
            int mingen = entity.Properties.Attributes?["minGenerationToAllowHealing"].AsInt(-1) ?? -1;
            return mingen >= 0 && mingen >= entity.WatchedAttributes.GetInt("generation", 0);
        }

        public WorldInteraction[] GetHealInteractionHelp(IClientWorldAccessor world, EntitySelection es, IClientPlayer player)
        {
            return new WorldInteraction[] {
                new WorldInteraction()
                {
                    ActionLangCode = "heldhelp-heal",
                    HotKeyCode = "ctrl",
                    MouseButton = EnumMouseButton.Right,
                }
            };
        }
    }


}
