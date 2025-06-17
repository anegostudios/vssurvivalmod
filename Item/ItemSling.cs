using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent
{
    public class ItemSling : Item
    {
        WorldInteraction[] interactions;

        public override void OnLoaded(ICoreAPI api)
        {
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

                if (invslot.Itemstack != null && invslot.Itemstack.Collectible is ItemStone)
                {
                    slot = invslot;
                    return false;
                }

                return true;
            });

            return slot;
        }

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
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
            byEntity.Attributes.SetInt("aiming", 0);
            byEntity.AnimManager.StopAnimation("slingaimbalearic");

            if (byEntity.World is IClientWorldAccessor)
            {
                slot.Itemstack.TempAttributes.RemoveAttribute("renderVariant");
            }

            slot.Itemstack.Attributes.SetInt("renderVariant", 0);
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

            ItemSlot arrowSlot = GetNextMunition(byEntity);
            if (arrowSlot == null) return;

            float damage = 0;

            // Sling damage
            if (slot.Itemstack.Collectible.Attributes != null)
            {
                damage += slot.Itemstack.Collectible.Attributes["damage"].AsFloat(0);
            }

            // Stone damage
            if (arrowSlot.Itemstack.Collectible.Attributes != null)
            {
                damage += arrowSlot.Itemstack.Collectible.Attributes["damage"].AsFloat(0);
            }

            if (byEntity != null) damage *= byEntity.Stats.GetBlended("rangedWeaponsDamage");

            ItemStack stack = arrowSlot.TakeOut(1);
            arrowSlot.MarkDirty();

            IPlayer byPlayer = null;
            if (byEntity is EntityPlayer) byPlayer = byEntity.World.PlayerByUid(((EntityPlayer)byEntity).PlayerUID);
            if (api.Side == EnumAppSide.Server) byEntity.World.PlaySoundAt(new AssetLocation("sounds/tool/sling1"), byEntity, null, false, 8, 0.25f);


            EntityProperties type = byEntity.World.GetEntityType(new AssetLocation("thrownstone-" + stack.Collectible.Variant["rock"]));
            Entity entity = byEntity.World.ClassRegistry.CreateEntity(type);
            ((EntityThrownStone)entity).FiredBy = byEntity;
            ((EntityThrownStone)entity).Damage = damage;
            ((EntityThrownStone)entity).ProjectileStack = stack;
            

            EntityProjectile.SpawnThrownEntity(entity, byEntity, 0.75, 0, 0, byEntity.Stats.GetBlended("bowDrawingStrength") * 0.8f);

            slot.Itemstack.Collectible.DamageItem(byEntity.World, byEntity, slot);

            byEntity.AnimManager.StartAnimation("slingthrowbalearic");

            byEntity.World.RegisterCallback((dt) => byEntity.AnimManager.StopAnimation("slingthrowbalearic"), 400);
        }


        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

            if (inSlot.Itemstack.Collectible.Attributes == null) return;

            float dmg = inSlot.Itemstack.Collectible.Attributes["damage"].AsFloat(0);
            if (dmg != 0) dsc.AppendLine(Lang.Get("sling-piercingdamage", dmg));
        }


        public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
        {
            return interactions.Append(base.GetHeldInteractionHelp(inSlot));
        }
    }
}
