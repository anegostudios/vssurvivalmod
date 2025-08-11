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
    public class ItemBow : Item
    {
        WorldInteraction[] interactions;

        string aimAnimation;

        public override void OnLoaded(ICoreAPI api)
        {
            aimAnimation = Attributes["aimAnimation"].AsString();

            if (api.Side != EnumAppSide.Client) return;
            ICoreClientAPI capi = api as ICoreClientAPI;


            interactions = ObjectCacheUtil.GetOrCreate(api, "bowInteractions", () =>
            {
                List<ItemStack> stacks = new List<ItemStack>();

                foreach (CollectibleObject obj in api.World.Collectibles)
                {
                    if (obj.Code.PathStartsWith("arrow-"))
                    {
                        stacks.Add(new ItemStack(obj));
                    }
                }

                return new WorldInteraction[]
                {
                    new WorldInteraction()
                    {
                        ActionLangCode = "heldhelp-chargebow",
                        MouseButton = EnumMouseButton.Right,
                        HotKeyCode = "dropitems",
                        Itemstacks = stacks.ToArray()
                    }
                };
            });
        }


        public override string GetHeldTpUseAnimation(ItemSlot activeHotbarSlot, Entity byEntity)
        {
            return null;
        }

        protected ItemSlot GetNextArrow(EntityAgent byEntity)
        {
            ItemSlot slot = null;
            byEntity.WalkInventory((invslot) =>
            {
                if (invslot is ItemSlotCreative) return true;

                ItemStack stack = invslot.Itemstack;
                if (stack != null && stack.Collectible != null && stack.Collectible.Code.PathStartsWith("arrow-") && stack.StackSize > 0)
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
            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
            if (handling == EnumHandHandling.PreventDefault) return;

            var controls = byEntity.MountedOn?.Controls ?? byEntity.Controls;
            if (controls.CtrlKey 
                && (entitySel?.SelectionBoxIndex ?? -1) >= 0 
                && entitySel.Entity?.GetBehavior<EntityBehaviorAttachable>() != null)
            {
                return;
            }

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
            byEntity.AnimManager.StartAnimation(aimAnimation);

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
            byEntity.AnimManager.StopAnimation(aimAnimation);

            if (byEntity.World is IClientWorldAccessor)
            {
                slot.Itemstack?.TempAttributes.RemoveAttribute("renderVariant");
            }

            slot.Itemstack?.Attributes.SetInt("renderVariant", 0);
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
            byEntity.AnimManager.StopAnimation(aimAnimation);

            if (byEntity.World.Side == EnumAppSide.Client)
            {
                slot.Itemstack.TempAttributes.RemoveAttribute("renderVariant");
                byEntity.AnimManager.StartAnimation("bowhit");
                return;
            }

            slot.Itemstack.Attributes.SetInt("renderVariant", 0);
            (byEntity as EntityPlayer)?.Player?.InventoryManager.BroadcastHotbarSlot();

            if (secondsUsed < 0.65f) return;

            ItemSlot arrowSlot = GetNextArrow(byEntity);
            if (arrowSlot == null) return;

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

            byEntity.World.PlaySoundAt(new AssetLocation("sounds/bow-release"), byEntity, null, false, 8);

            float breakChance = 0.5f;
            if (stack.ItemAttributes != null) breakChance = stack.ItemAttributes["breakChanceOnImpact"].AsFloat(0.5f);

            EntityProperties type = byEntity.World.GetEntityType(new AssetLocation(stack.ItemAttributes["arrowEntityCode"].AsString("arrow-" + stack.Collectible.Variant["material"])));
            Entity entityToSpawn = byEntity.World.ClassRegistry.CreateEntity(type);
            var entityarrow = entityToSpawn as IProjectile;
            entityarrow.FiredBy = byEntity;
            entityarrow.Damage = damage;
            entityarrow.DamageTier = Attributes["damageTier"].AsInt(0);
            entityarrow.ProjectileStack = stack;
            entityarrow.DropOnImpactChance = 1 - breakChance;
            entityarrow.IgnoreInvFrames = Attributes["ignoreInvFrames"].AsBool(false);
            entityarrow.WeaponStack = slot.Itemstack;

            float acc = Math.Max(0.001f, 1 - byEntity.Attributes.GetFloat("aimingAccuracy", 0));
            double rndpitch = byEntity.WatchedAttributes.GetDouble("aimingRandPitch", 1) * acc * 0.75f;
            double rndyaw = byEntity.WatchedAttributes.GetDouble("aimingRandYaw", 1) * acc * 0.75f;
            
            Vec3d pos = byEntity.ServerPos.XYZ.Add(0, byEntity.LocalEyePos.Y, 0);
            Vec3d aheadPos = pos.AheadCopy(1, byEntity.SidedPos.Pitch + rndpitch, byEntity.SidedPos.Yaw + rndyaw);
            Vec3d velocity = (aheadPos - pos) * byEntity.Stats.GetBlended("bowDrawingStrength");


            entityToSpawn.ServerPos.SetPosWithDimension(byEntity.SidedPos.BehindCopy(0.21).XYZ.Add(0, byEntity.LocalEyePos.Y, 0));
            entityToSpawn.ServerPos.Motion.Set(velocity);
            entityToSpawn.Pos.SetFrom(entityToSpawn.ServerPos);
            entityToSpawn.World = byEntity.World;
            entityarrow.PreInitialize();

            byEntity.World.SpawnPriorityEntity(entityToSpawn);

            slot.Itemstack.Collectible.DamageItem(byEntity.World, byEntity, slot);
            slot.MarkDirty();

            byEntity.AnimManager.StartAnimation("bowhit");
        }


        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

            if (inSlot.Itemstack.Collectible.Attributes == null) return;

            float dmg = inSlot.Itemstack.Collectible.Attributes?["damage"].AsFloat(0) ?? 0;
            if (dmg != 0) dsc.AppendLine(Lang.Get("bow-piercingdamage", dmg));

            float accuracyBonus = inSlot.Itemstack.Collectible?.Attributes["statModifier"]["rangedWeaponsAcc"].AsFloat(0) ?? 0;
            if (accuracyBonus != 0) dsc.AppendLine(Lang.Get("bow-accuracybonus", accuracyBonus > 0 ? "+" : "", (int)(100*accuracyBonus)));
        }


        public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
        {
            return interactions.Append(base.GetHeldInteractionHelp(inSlot));
        }
    }
}
