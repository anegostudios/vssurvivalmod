using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent
{
    public class ItemSling : Item
    {
        WorldInteraction[] interactions;
        private TagSet munitionTag = TagSet.Empty;

        public override void OnLoaded(ICoreAPI api)
        {
            if(munitionTag.IsEmpty) api.CollectibleTagRegistry.TryRegisterAndCreateTagSetAndLogIssues(out munitionTag, "sling-munition");

            if (api.Side != EnumAppSide.Client) return;
            ICoreClientAPI capi = api as ICoreClientAPI;

            interactions = ObjectCacheUtil.GetOrCreate(api, "slingInteractions", () =>
            {
                List<ItemStack> stacks = new List<ItemStack>();

                foreach (CollectibleObject obj in api.World.Collectibles)
                {
                    if (obj is ItemStone)
                    {
                        stacks.Add(new ItemStack(obj));
                    }
                }

                return new WorldInteraction[]
                {
                    new WorldInteraction()
                    {
                        ActionLangCode = "heldhelp-chargesling",
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = stacks.ToArray()
                    }
                };
            });
        }



        public override string GetHeldTpUseAnimation(ItemSlot activeHotbarSlot, Entity byEntity)
        {
            return null;
        }

        ItemSlot GetNextMunition(EntityAgent byEntity)
        {
            ItemSlot slot = null;
            byEntity.WalkInventory((invslot) =>
            {
                if (invslot is ItemSlotCreative) return true;

                var item = invslot.Itemstack?.Collectible;
                if (item == null || invslot.StackSize <= 0) return true;

                if (item is ItemStone || item.GetTags(invslot.Itemstack).Overlaps(munitionTag))
                {
                    slot = invslot;
                    return false;
                }

                return true;
            });

            return slot;
        }

        private void ResetSlingAiming(ItemSlot slot, EntityAgent byEntity)
        {
            byEntity.Attributes.SetInt("aiming", 0);
            byEntity.AnimManager.StopAnimation("slingaimbalearic");

            if (byEntity.World is IClientWorldAccessor)
            {
                slot.Itemstack.TempAttributes.RemoveAttribute("renderVariant");
            }

            slot.Itemstack.Attributes.SetInt("renderVariant", 0);
        }

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            if (byEntity.Swimming) return;

            ItemSlot invslot = GetNextMunition(byEntity);
            if (invslot == null) return;

            if (byEntity.World is IClientWorldAccessor)
            {
                slot.Itemstack.TempAttributes.SetInt("renderVariant", 1);
            }

            slot.Itemstack.Attributes.SetInt("renderVariant", 1);

            // Not ideal to code the aiming controls this way. Needs an elegant solution - maybe an event bus?
            byEntity.Attributes.SetInt("aiming", 1);
            byEntity.Attributes.SetInt("aimingCancel", 0);
            byEntity.AnimManager.StartAnimation("slingaimbalearic");

            IPlayer byPlayer = null;
            if (byEntity is EntityPlayer) byPlayer = byEntity.World.PlayerByUid(((EntityPlayer)byEntity).PlayerUID);
            byEntity.World.PlaySoundAt(new AssetLocation("sounds/bow-draw"), byEntity, byPlayer, false, 8);

            handling = EnumHandHandling.PreventDefault;
        }

        public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (byEntity.Swimming)
            {
                ResetSlingAiming(slot, byEntity);
                byEntity.Attributes.SetInt("aimingCancel", 1);
                (byEntity as EntityPlayer)?.Player?.InventoryManager.BroadcastHotbarSlot();
                return false;
            }

            if (byEntity.World.Side == EnumAppSide.Client && byEntity.Controls.LeftMouseDown)
            {
                byEntity.RequestedHandUseCancelReason = EnumItemUseCancelReason.PlayerTriggered;
            }

            int renderVariant = GameMath.Clamp((int)Math.Ceiling(secondsUsed * 4), 0, 3);
            int prevRenderVariant = slot.Itemstack.Attributes.GetInt("renderVariant", 0);

            slot.Itemstack.TempAttributes.SetInt("renderVariant", renderVariant);
            slot.Itemstack.Attributes.SetInt("renderVariant", renderVariant);

            if (prevRenderVariant != renderVariant)
            {
                (byEntity as EntityPlayer)?.Player?.InventoryManager.BroadcastHotbarSlot();
            }

            return true;
        }


        public override bool OnHeldInteractCancel(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, EnumItemUseCancelReason cancelReason)
        {
            ResetSlingAiming(slot, byEntity);
            if (cancelReason != EnumItemUseCancelReason.Destroyed) (byEntity as EntityPlayer)?.Player?.InventoryManager.BroadcastHotbarSlot();

            if (cancelReason != EnumItemUseCancelReason.ReleasedMouse)
            {
                byEntity.Attributes.SetInt("aimingCancel", 1);
            }

            return true;
        }

        public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (byEntity.Attributes.GetInt("aimingCancel") == 1) return;
            if (byEntity.Swimming)
            {
                ResetSlingAiming(slot, byEntity);
                (byEntity as EntityPlayer)?.Player?.InventoryManager.BroadcastHotbarSlot();
                return;
            }

            byEntity.Attributes.SetInt("aiming", 0);
            byEntity.AnimManager.StopAnimation("slingaimbalearic");

            byEntity.World.RegisterCallback((dt) => slot.Itemstack?.Attributes.SetInt("renderVariant", 2), 250);
            byEntity.World.RegisterCallback((dt) =>
            {
                if (byEntity.World is IClientWorldAccessor)
                {
                    slot.Itemstack?.TempAttributes.RemoveAttribute("renderVariant");
                }
                slot.Itemstack?.Attributes.SetInt("renderVariant", 0);
            }, 450);

            (byEntity as EntityPlayer)?.Player?.InventoryManager.BroadcastHotbarSlot();

            if (secondsUsed < 0.75f) return;

            ItemSlot munitionSlot = GetNextMunition(byEntity);
            if (munitionSlot == null) return;

            float damage = 0;

            // Sling damage
            damage += slot.Itemstack.ItemAttributes?["damage"].AsFloat(0) ?? 0;

            // Munition damage
            var munition = munitionSlot.Itemstack.Collectible.GetBehavior<CollectibleBehaviorAmmunition>();
            damage += munition?.Config.Damage ?? munitionSlot.Itemstack.ItemAttributes?["damage"].AsFloat(0) ?? 0;

            // Entity ranged damage multiplier
            damage *= byEntity?.Stats.GetBlended("rangedWeaponsDamage") ?? 1;

            ItemStack stack = munitionSlot.TakeOut(1);
            munitionSlot.MarkDirty();

            if (api.Side == EnumAppSide.Server) byEntity.World.PlaySoundAt(new AssetLocation("sounds/tool/sling1"), byEntity, null, false, 8, 0.25f);

            float dropChance = munition?.Config.DropOnImpactChance ?? (1 - (stack.ItemAttributes?["breakChanceOnImpact"].AsFloat(0.5f) ?? 0.5f));

            string code = munition?.MunitionProjectileCode ?? "thrownitem";
            EntityProperties type = byEntity.World.GetEntityType(code);
            var munitionEntity = byEntity.World.ClassRegistry.CreateEntity(type);
            if (munitionEntity is not IProjectile projectile)
            {
                byEntity.World.Logger.Warning($"Sling munition entity type code {code} does not exist or does not implement IProjectile, will not spawn projectile");
                return;
            }

            projectile.FiredBy = byEntity;
            projectile.Damage = damage;
            projectile.DamageType = EnumDamageType.BluntAttack;
            projectile.DamageTier = Attributes["damageTier"].AsInt(0);
            projectile.ProjectileStack = stack;
            projectile.DropOnImpactChance = dropChance;
            projectile.IgnoreInvFrames = Attributes["ignoreInvFrames"].AsBool(false);
            projectile.WeaponStack = slot.Itemstack;
            projectile.Collectible = munition?.Config.Collectible ?? true;

            double speed = 0.8 * byEntity.Stats.GetBlended("bowDrawingStrength");
            EntityProjectile.SpawnProjectile(munitionEntity, byEntity, speed, 0.75, -0.2, 0.1, 0.4, 20);

            slot.Itemstack.Collectible.DamageItem(byEntity.World, byEntity, slot);

            byEntity.AnimManager.StartAnimation("slingthrowbalearic");

            byEntity.World.RegisterCallback((dt) => byEntity.AnimManager.StopAnimation("slingthrowbalearic"), 400);
        }


        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

            if (inSlot.Itemstack.Collectible.Attributes == null) return;

            float dmg = inSlot.Itemstack.Collectible.Attributes["damage"].AsFloat(0);
            if (dmg != 0) dsc.AppendLine(Lang.Get("sling-bluntdamage", dmg));
        }


        public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
        {
            if (((ICoreClientAPI)api).World.Player.Entity.Attributes.GetInt("aiming") != 0)
            {
                return [new WorldInteraction { ActionLangCode = "general-cancel", MouseButton = EnumMouseButton.Left }];
            }

            return interactions.Append(base.GetHeldInteractionHelp(inSlot));
        }
    }
}
