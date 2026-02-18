using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

#nullable disable

namespace Vintagestory.GameContent
{
    public class ItemKnife : Item
    {
        public static SimpleParticleProperties particlesStab;
        static ItemKnife()
        {
            particlesStab = new SimpleParticleProperties(
                1, 1,
                ColorUtil.ToRgba(50, 220, 220, 220),
                new Vec3d(),
                new Vec3d(),
                new Vec3f(-0.1f, -0.1f, -0.1f),
                new Vec3f(0.1f, 0.1f, 0.1f),
                1.5f,
                0,
                0.5f,
                0.75f,
                EnumParticleModel.Cube
            );

            particlesStab.SizeEvolve = EvolvingNatFloat.create(EnumTransformFunction.QUADRATIC, -0.6f);
            particlesStab.AddPos.Set(0.1f, 0.1f, 0.1f);
            particlesStab.addLifeLength = 0.5f;
            particlesStab.RandomVelocityChange = true;
            particlesStab.MinQuantity = 200;
            particlesStab.AddQuantity = 50;
            particlesStab.MinSize = 0.2f;
            particlesStab.ParticleModel = EnumParticleModel.Quad;
            particlesStab.OpacityEvolve = EvolvingNatFloat.create(EnumTransformFunction.LINEAR, -150);
        }


        // Knife harvesting speed is equivalent to 50% of the plant breaking speed bonus
        public virtual float GetKnifeHarvestingSpeed(ItemSlot slot) => 1f / ((GetMiningSpeeds(slot)[EnumBlockMaterial.Plant] - 1) * 0.5f + 1);
        public string knifeHitBlockAnimation;
        public string knifeHitEntityAnimation;


        public override string GetHeldTpHitAnimation(ItemSlot slot, Entity byEntity)
        {
            if ((byEntity as EntityPlayer)?.EntitySelection != null) return knifeHitEntityAnimation;
            if ((byEntity as EntityPlayer)?.BlockSelection != null)
            {
                if (byEntity.AnimManager.IsAnimationActive("knifestab")) return "idle";
                return knifeHitBlockAnimation;
            }
            return base.GetHeldTpHitAnimation(slot, byEntity);
        }

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            knifeHitBlockAnimation = Attributes["knifeHitBlockAnimation"].AsString(HeldTpHitAnimation);
            knifeHitEntityAnimation = Attributes["knifeHitEntityAnimation"].AsString(HeldTpHitAnimation);
        }



        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
            if (handling == EnumHandHandling.PreventDefault) return;

            byEntity.Attributes.SetBool("isInsertGear", false);

            if (byEntity.LeftHandItemSlot?.Itemstack?.Collectible is ItemTemporalGear && byEntity.GetBehavior<EntityBehaviorTemporalStabilityAffected>() != null)
            {
                byEntity.StartAnimation("insertgear");
                byEntity.Attributes.SetBool("isInsertGear", true);
                byEntity.Attributes.SetBool("stabPlayed", false);
                byEntity.Attributes.SetBool("didHurt", false);

                var healthTree = byEntity.WatchedAttributes.GetTreeAttribute("health");
                if (healthTree != null && healthTree.GetFloat("currenthealth") <= 2)
                {
                    if (api.Side == EnumAppSide.Client)
                    {
                        (api as ICoreClientAPI).TriggerIngameError(this, "toodangerous", Lang.Get("Cannot apply without dying"));
                    }
                    return;
                }

                handling = EnumHandHandling.PreventDefault;
                return;
            }

            
            if (byEntity.Controls.ShiftKey)
            {
                var bh = getIHarvestable(byEntity, blockSel, entitySel, out var soundPos);
                if (bh != null && bh.IsHarvestable(slot, byEntity))
                {
                    byEntity.World.PlaySoundAt(bh.HarvestableSound, soundPos.X, soundPos.Y, soundPos.Z, (byEntity as EntityPlayer)?.Player, false, 12);
                    handling = EnumHandHandling.PreventDefault;
                    return;
                }
            }

            handling = EnumHandHandling.NotHandled;
        }


        public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (byEntity.Attributes.GetBool("isInsertGear", false))
            {
                if (!(byEntity.LeftHandItemSlot?.Itemstack?.Collectible is ItemTemporalGear))
                {
                    return false;
                }

                if (byEntity.World.Side == EnumAppSide.Client)
                {
                    if (secondsUsed > 1.1f && !byEntity.Attributes.GetBool("stabPlayed", false))
                    {
                        byEntity.World.PlaySoundAt(new AssetLocation("sounds/player/stab"), byEntity, (byEntity as EntityPlayer)?.Player, false, 12, 0.3f);
                        byEntity.Attributes.SetBool("stabPlayed", true);
                    }

                    return true;
                }
                else
                {
                    if (secondsUsed > 1.1f && !byEntity.Attributes.GetBool("didHurt", false))
                    {
                        byEntity.ReceiveDamage(new DamageSource() { DamageTier = 0, Source = EnumDamageSource.Internal, Type = EnumDamageType.Injury }, 2);
                        slot?.Itemstack?.Collectible.DamageItem(byEntity.World, byEntity, slot, 1);

                        byEntity.Attributes.SetBool("didHurt", true);
                    }

                    if (secondsUsed >= 1.95f && !byEntity.Attributes.GetBool("stabPlayed", false))
                    {
                        byEntity.GetBehavior<EntityBehaviorTemporalStabilityAffected>().AddStability(0.30);

                        byEntity.LeftHandItemSlot.TakeOut(1);
                        byEntity.LeftHandItemSlot.MarkDirty();

                        ((byEntity as EntityPlayer)?.Player as IServerPlayer)?.BroadcastPlayerData();

                        int h = 110 + api.World.Rand.Next(15);
                        int v = 100 + api.World.Rand.Next(50);
                        particlesStab.MinPos = byEntity.Pos.XYZ.Add(byEntity.SelectionBox.X1, 0, byEntity.SelectionBox.Z1);
                        particlesStab.AddPos = new Vec3d(byEntity.SelectionBox.XSize, byEntity.SelectionBox.Y2, byEntity.SelectionBox.ZSize);
                        particlesStab.Color = ColorUtil.ReverseColorBytes(ColorUtil.HsvToRgba(h, 180, v, 150));
                        api.World.SpawnParticles(particlesStab);

                        byEntity.Attributes.SetBool("stabPlayed", true);
                    }

                    return secondsUsed < 2f;
                }
            }


            // Crappy fix to make animal harvesting not buggy T_T
            if (api.Side == EnumAppSide.Server) return true;

            var bh = getIHarvestable(byEntity, blockSel, entitySel, out _);
            if (bh != null && bh.IsHarvestable(slot, byEntity))
            {
                if (byEntity.World.Side == EnumAppSide.Client)
                {
                    slot.Itemstack.TempAttributes.SetString("harvestanimation", bh.HarvestAnimation);
                    byEntity.StartAnimation(bh.HarvestAnimation);
                }

                return secondsUsed < GetKnifeHarvestingSpeed(slot) * bh.GetHarvestDuration(slot,byEntity) + 0.15f;
            }

            return false;
        }



        public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            byEntity.StopAnimation("insertgear");
            byEntity.StopAnimation(slot.Itemstack.TempAttributes.GetString("harvestanimation"));

            if (byEntity.LeftHandItemSlot?.Itemstack?.Collectible is ItemTemporalGear)
            {
                return;
            }

            IHarvestable bh = getIHarvestable(byEntity, blockSel, entitySel, out _);
            if (bh != null && bh.IsHarvestable(slot, byEntity) && secondsUsed >= GetKnifeHarvestingSpeed(slot) * bh.GetHarvestDuration(slot, byEntity) - 0.1f)
            {
                bh.SetHarvested((byEntity as EntityPlayer)?.Player);
                slot?.Itemstack?.Collectible.DamageItem(byEntity.World, byEntity, slot, 3);
            }
        }


        public override bool OnHeldInteractCancel(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, EnumItemUseCancelReason cancelReason)
        {
            byEntity.StopAnimation(slot.Itemstack.TempAttributes.GetString("harvestanimation"));
            byEntity.StopAnimation("insertgear");
            return base.OnHeldInteractCancel(secondsUsed, slot, byEntity, blockSel, entitySel, cancelReason);
        }


        public override void OnHeldAttackStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandHandling handling)
        {
            if (blockSel == null)
            {
                base.OnHeldAttackStart(slot, byEntity, blockSel, entitySel, ref handling);
                handling = EnumHandHandling.PreventDefault;
            }
        }


        protected static IHarvestable getIHarvestable(EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, out Vec3d pos)
        {
            IHarvestable bh = entitySel?.Entity.GetInterface<IHarvestable>();
            if (bh != null)
            {
                pos = entitySel.Position;
                return bh;
            }

            pos = null;
            if (blockSel != null)
            {
                bh = byEntity.World.BlockAccessor.GetBlock(blockSel.Position)?.GetInterface<IHarvestable>(byEntity.World, blockSel.Position);
                pos = blockSel.Position.ToVec3d().Add(0.5, 0, 0.5);
            }
            
            return bh;
        }



    }
}
