using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Vintagestory.GameContent
{
    public class ItemBow : Item
    {
        public override string GetHeldTpUseAnimation(ItemSlot activeHotbarSlot, Entity byEntity)
        {
            return null;
        }

        ItemSlot GetNextArrow(EntityAgent byEntity)
        {
            ItemSlot slot = null;
            byEntity.WalkInventory((invslot) =>
            {
                if (invslot is ItemSlotCreative) return true;

                if (invslot.Itemstack != null && invslot.Itemstack.Collectible.Code.Path.StartsWith("arrow-"))
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
            ItemSlot invslot = GetNextArrow(byEntity);
            if (invslot == null) return;

            if (byEntity.World is IClientWorldAccessor)
            {
                slot.Itemstack.TempAttributes.SetInt("renderVariant", 1);
            }

            slot.Itemstack.Attributes.SetInt("renderVariant", 1);

            // Not ideal to code the aiming controls this way. Needs an elegant solution - maybe an event bus?
            byEntity.Attributes.SetInt("aiming", 1);
            byEntity.Attributes.SetInt("aimingCancel", 0);
            byEntity.AnimManager.StartAnimation("bowaim");

            IPlayer byPlayer = null;
            if (byEntity is EntityPlayer) byPlayer = byEntity.World.PlayerByUid(((EntityPlayer)byEntity).PlayerUID);
            byEntity.World.PlaySoundAt(new AssetLocation("sounds/bow-draw"), byEntity, byPlayer, false, 8);

            handling = EnumHandHandling.PreventDefault;
        }

        public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
//            if (byEntity.World is IClientWorldAccessor)
            {
                int renderVariant = GameMath.Clamp((int)Math.Ceiling(secondsUsed * 4), 0, 3);
                int prevRenderVariant = slot.Itemstack.Attributes.GetInt("renderVariant", 0);

                slot.Itemstack.TempAttributes.SetInt("renderVariant", renderVariant);
                slot.Itemstack.Attributes.SetInt("renderVariant", renderVariant);

                if (prevRenderVariant != renderVariant)
                {
                    (byEntity as EntityPlayer)?.Player?.InventoryManager.BroadcastHotbarSlot();
                }
            }

            
            return true;
        }


        public override bool OnHeldInteractCancel(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, EnumItemUseCancelReason cancelReason)
        {
            byEntity.Attributes.SetInt("aiming", 0);
            byEntity.AnimManager.StopAnimation("bowaim");

            if (byEntity.World is IClientWorldAccessor)
            {
                slot.Itemstack.TempAttributes.RemoveAttribute("renderVariant");
            }

            slot.Itemstack.Attributes.SetInt("renderVariant", 0);
            (byEntity as EntityPlayer)?.Player?.InventoryManager.BroadcastHotbarSlot();

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
            byEntity.AnimManager.StopAnimation("bowaim");

            if (byEntity.World is IClientWorldAccessor)
            {
                slot.Itemstack.TempAttributes.RemoveAttribute("renderVariant");
            }

            slot.Itemstack.Attributes.SetInt("renderVariant", 0);
            (byEntity as EntityPlayer)?.Player?.InventoryManager.BroadcastHotbarSlot();

            if (secondsUsed < 0.35f) return;

            ItemSlot arrowSlot = GetNextArrow(byEntity);
            if (arrowSlot == null) return;

            string arrowMaterial = arrowSlot.Itemstack.Collectible.FirstCodePart(1);
            float damage = 0;

            // Bow damage
            if (slot.Itemstack.Collectible.Attributes != null)
            {
                damage += slot.Itemstack.Collectible.Attributes["damage"].AsFloat(0);
            }

            // Arrow damage
            if (arrowSlot.Itemstack.Collectible.Attributes != null)
            {
                damage += arrowSlot.Itemstack.Collectible.Attributes["damage"].AsFloat(0);
            }

            ItemStack stack = arrowSlot.TakeOut(1);
            arrowSlot.MarkDirty();

            IPlayer byPlayer = null;
            if (byEntity is EntityPlayer) byPlayer = byEntity.World.PlayerByUid(((EntityPlayer)byEntity).PlayerUID);
            byEntity.World.PlaySoundAt(new AssetLocation("sounds/bow-release"), byEntity, byPlayer, false, 8);

            float breakChance = 0.5f;
            if (stack.ItemAttributes != null) breakChance = stack.ItemAttributes["breakChanceOnImpact"].AsFloat(0.5f);

            EntityProperties type = byEntity.World.GetEntityType(new AssetLocation("arrow"));
            Entity entity = byEntity.World.ClassRegistry.CreateEntity(type);
            ((EntityProjectile)entity).FiredBy = byEntity;
            ((EntityProjectile)entity).Damage = damage;
            ((EntityProjectile)entity).ProjectileStack = stack;
            ((EntityProjectile)entity).DropOnImpactChance = 1 - breakChance;


            int? texIndex = type.Attributes?["texturealternateMapping"]?[arrowMaterial].AsInt(0);
            entity.WatchedAttributes.SetInt("textureIndex", texIndex == null ? 0 : (int)texIndex);

            float acc = (1 - byEntity.Attributes.GetFloat("aimingAccuracy", 0));
            double rndpitch = byEntity.WatchedAttributes.GetDouble("aimingRandPitch", 1) * acc * 0.75;
            double rndyaw = byEntity.WatchedAttributes.GetDouble("aimingRandYaw", 1) * acc * 0.75;
            
            Vec3d pos = byEntity.ServerPos.XYZ.Add(0, byEntity.EyeHeight - 0.2, 0);
            Vec3d aheadPos = pos.AheadCopy(1, byEntity.ServerPos.Pitch + rndpitch, byEntity.ServerPos.Yaw + rndyaw);
            Vec3d velocity = (aheadPos - pos) * 0.95;


            entity.ServerPos.SetPos(byEntity.ServerPos.BehindCopy(0.21).XYZ.Add(0, byEntity.EyeHeight - 0.2, 0));
            entity.ServerPos.Motion.Set(velocity);

            entity.Pos.SetFrom(entity.ServerPos);
            entity.World = byEntity.World;
            ((EntityProjectile)entity).SetRotation();

            byEntity.World.SpawnEntity(entity);

            slot.Itemstack.Collectible.DamageItem(byEntity.World, byEntity, slot);

            byEntity.AnimManager.StartAnimation("bowhit");
        }


        public override void GetHeldItemInfo(ItemStack stack, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(stack, dsc, world, withDebugInfo);

            if (stack.Collectible.Attributes == null) return;

            float dmg = stack.Collectible.Attributes["damage"].AsFloat(0);
            if (dmg != 0) dsc.AppendLine(dmg + Lang.Get("piercing-damage"));
        }

    }
}
