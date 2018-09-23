using System;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    

    public class ItemSpear : Item
    {
        public override string GetHeldTpUseAnimation(IItemSlot activeHotbarSlot, Entity byEntity)
        {
            return null;
        }

        public override void OnHeldInteractStart(IItemSlot itemslot, IEntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandHandling handling)
        {
            // Not ideal to code the aiming controls this way. Needs an elegant solution - maybe an event bus?
            byEntity.Attributes.SetInt("aiming", 1);
            byEntity.Attributes.SetInt("aimingCancel", 0);
            byEntity.StartAnimation("aim");

            handling = EnumHandHandling.PreventDefault;
        }

        public override bool OnHeldInteractStep(float secondsUsed, IItemSlot slot, IEntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (byEntity.World is IClientWorldAccessor)
            {
                ModelTransform tf = new ModelTransform();
                tf.EnsureDefaultValues();

                float offset = GameMath.Clamp(secondsUsed * 5, 0, 2f);

                //tf.Translation.Set(-offset/4, 0, offset/3);
                tf.Translation.Set(-offset/3, 0, offset / 3);
                tf.Rotation.Set(0, -offset * 15, 0);

                byEntity.Controls.UsingHeldItemTransformBefore = tf;
            }

            return true;
        }


        public override bool OnHeldInteractCancel(float secondsUsed, IItemSlot slot, IEntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, EnumItemUseCancelReason cancelReason)
        {
            byEntity.Attributes.SetInt("aiming", 0);
            byEntity.StopAnimation("aim");

            if (cancelReason != EnumItemUseCancelReason.ReleasedMouse)
            {
                byEntity.Attributes.SetInt("aimingCancel", 1);
            }

            return true;
        }

        public override void OnHeldInteractStop(float secondsUsed, IItemSlot slot, IEntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (byEntity.Attributes.GetInt("aimingCancel") == 1) return;

            byEntity.Attributes.SetInt("aiming", 0);
            byEntity.StopAnimation("aim");

            if (secondsUsed < 0.35f) return;

            float damage = 1.5f;

            if (slot.Itemstack.Collectible.Attributes != null)
            {
                damage = slot.Itemstack.Collectible.Attributes["damage"].AsFloat(0);
            }

            string spearMaterial = slot.Itemstack.Collectible.FirstCodePart(1);

            ItemStack stack = slot.TakeOut(1);
            stack.Collectible.DamageItem(byEntity.World, byEntity, new DummySlot(stack));

            IPlayer byPlayer = null;
            if (byEntity is IEntityPlayer) byPlayer = byEntity.World.PlayerByUid(((IEntityPlayer)byEntity).PlayerUID);
            byEntity.World.PlaySoundAt(new AssetLocation("sounds/player/throw"), byEntity, byPlayer, false, 8);

            EntityProperties type = byEntity.World.GetEntityType(new AssetLocation(Attributes["spearEntityCode"].AsString()));
            Entity entity = byEntity.World.ClassRegistry.CreateEntity(type);
            ((EntityProjectile)entity).FiredBy = byEntity;
            ((EntityProjectile)entity).Damage = damage;
            ((EntityProjectile)entity).ProjectileStack = stack;
            ((EntityProjectile)entity).DropOnImpactChance = 1.1f;
            ((EntityProjectile)entity).Weight = 0.3f;

            int? texIndex = type.Attributes?["texturealternateMapping"]?[spearMaterial].AsInt(0);
            entity.WatchedAttributes.SetInt("textureIndex", texIndex == null ? 0 : (int)texIndex);

            float acc = (1 - byEntity.Attributes.GetFloat("aimingAccuracy", 0));
            double rndpitch = byEntity.WatchedAttributes.GetDouble("aimingRandPitch", 1) * acc * 0.75;
            double rndyaw = byEntity.WatchedAttributes.GetDouble("aimingRandYaw", 1) * acc * 0.75;

            Vec3d pos = byEntity.ServerPos.XYZ.Add(0, byEntity.EyeHeight - 0.2, 0);
            Vec3d aheadPos = pos.AheadCopy(1, byEntity.ServerPos.Pitch + rndpitch, byEntity.ServerPos.Yaw + rndyaw);
            Vec3d velocity = (aheadPos - pos) * 0.65;

            entity.ServerPos.SetPos(byEntity.ServerPos.BehindCopy(0.21).XYZ.Add(0, byEntity.EyeHeight - 0.2, 0));
            entity.ServerPos.Motion.Set(velocity);

            entity.Pos.SetFrom(entity.ServerPos);
            entity.World = byEntity.World;
            ((EntityProjectile)entity).SetRotation();

            byEntity.World.SpawnEntity(entity);
            byEntity.StartAnimation("throw");

            RefillSlotIfEmpty(slot, byEntity);
        }




        public override void OnHeldAttackStart(IItemSlot slot, IEntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandHandling handling)
        {
            byEntity.Attributes.SetInt("didattack", 0);

            byEntity.World.RegisterCallback((dt) =>
            {
                IPlayer byPlayer = (byEntity as EntityPlayer).Player;
                if (byPlayer == null) return;

                if (byEntity.Controls.HandUse == EnumHandInteract.HeldItemAttack)
                {
                    byPlayer.Entity.World.PlaySoundAt(new AssetLocation("sounds/player/strike"), byPlayer, byPlayer, true, 16, 0.5f);
                }
            }, 464);

            handling = EnumHandHandling.PreventDefault;
        }

        public override bool OnHeldAttackCancel(float secondsPassed, IItemSlot slot, IEntityAgent byEntity, BlockSelection blockSelection, EntitySelection entitySel, EnumItemUseCancelReason cancelReason)
        {
            return false;
        }

        public override bool OnHeldAttackStep(float secondsPassed, IItemSlot slot, IEntityAgent byEntity, BlockSelection blockSelection, EntitySelection entitySel)
        {
            float backwards = -Math.Min(0.35f, 2 * secondsPassed);
            float stab = Math.Min(1.2f, 20 * Math.Max(0, secondsPassed - 0.35f)); // + Math.Max(0, 5*(secondsPassed - 0.5f));

            if (byEntity.World.Side == EnumAppSide.Client)
            {
                IClientWorldAccessor world = byEntity.World as IClientWorldAccessor;
                ModelTransform tf = new ModelTransform();
                tf.EnsureDefaultValues();

                float sum = stab + backwards;
                float ztranslation = Math.Min(0.2f, 1.5f * secondsPassed);
                float easeout = Math.Max(0, 10 * (secondsPassed - 1));

                if (secondsPassed > 0.4f) sum = Math.Max(0, sum - easeout);
                ztranslation = Math.Max(0, ztranslation - easeout);

                tf.Translation.Set(sum * 0.8f, 2.5f * sum / 3, -ztranslation);
                tf.Rotation.Set(sum * 10, sum * 2, sum * 25);

                byEntity.Controls.UsingHeldItemTransformBefore = tf;

                if (stab > 1.15f && byEntity.Attributes.GetInt("didattack") == 0)
                {
                    world.TryAttackEntity(entitySel);
                    byEntity.Attributes.SetInt("didattack", 1);
                    world.ShakeCamera(0.125f);
                }
            }



            return secondsPassed < 1.2f;
        }

        public override void OnHeldAttackStop(float secondsPassed, IItemSlot slot, IEntityAgent byEntity, BlockSelection blockSelection, EntitySelection entitySel)
        {

        }


        private void RefillSlotIfEmpty(IItemSlot slot, IEntityAgent byEntity)
        {
            if (!slot.Empty) return;

            byEntity.WalkInventory((invslot) =>
            {
                if (invslot is ItemSlotCreative) return true;

                if (invslot.Itemstack != null && invslot.Itemstack.Collectible is ItemSpear)
                {
                    invslot.TryPutInto(byEntity.World, slot);
                    invslot.Inventory.PerformNotifySlot(invslot.Inventory.GetSlotId(invslot));
                    slot.Inventory.PerformNotifySlot(slot.Inventory.GetSlotId(slot));

                    return false;
                }

                return true;
            });
        }

        public override void GetHeldItemInfo(ItemStack stack, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(stack, dsc, world, withDebugInfo);
            if (stack.Collectible.Attributes == null) return;

            float damage = 1.5f;

            if (stack.Collectible.Attributes != null)
            {
                damage = stack.Collectible.Attributes["damage"].AsFloat(0);
            }

            dsc.AppendLine(damage + " piercing damage when thrown");
        }
    }
}
