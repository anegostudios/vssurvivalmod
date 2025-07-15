﻿using System;
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
                
        // Knife harvesting speed is equivalent to 50% of the plant breaking speed bonus
        public float KnifeHarvestingSpeed => 1f / ((MiningSpeed[EnumBlockMaterial.Plant] - 1) * 0.5f + 1);


        public string knifeHitBlockAnimation;
        public string knifeHitEntityAnimation;

        public override string GetHeldTpHitAnimation(ItemSlot slot, Entity byEntity)
        {
            if ((byEntity as EntityPlayer)?.EntitySelection != null) return knifeHitEntityAnimation;
            if ((byEntity as EntityPlayer)?.BlockSelection != null) return knifeHitBlockAnimation;
            return base.GetHeldTpHitAnimation(slot, byEntity);
        }

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            knifeHitBlockAnimation = Attributes["knifeHitBlockAnimation"].AsString(HeldTpHitAnimation);
            knifeHitEntityAnimation = Attributes["knifeHitEntityAnimation"].AsString(HeldTpHitAnimation);
        }

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

                var healthTree = byEntity.WatchedAttributes.GetTreeAttribute("health") as ITreeAttribute;
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

            EntityBehaviorHarvestable bh;
            if (byEntity.Controls.ShiftKey && entitySel != null && (bh = entitySel.Entity.GetBehavior<EntityBehaviorHarvestable>()) != null && bh.Harvestable)
            {
                byEntity.World.PlaySoundAt(new AssetLocation("sounds/player/scrape"), entitySel.Entity, (byEntity as EntityPlayer)?.Player, false, 12);
                handling = EnumHandHandling.PreventDefault;
                return;
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
                        particlesStab.MinPos = byEntity.SidedPos.XYZ.Add(byEntity.SelectionBox.X1, 0, byEntity.SelectionBox.Z1);
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



            EntityBehaviorHarvestable bh;
            if (entitySel != null && (bh = entitySel.Entity.GetBehavior<EntityBehaviorHarvestable>()) != null && bh.Harvestable)
            {
                if (byEntity.World.Side == EnumAppSide.Client)
                {
                    byEntity.StartAnimation("knifecut");
                }
                 
                return secondsUsed < KnifeHarvestingSpeed * bh.GetHarvestDuration(byEntity) + 0.15f;
            }

            return false;
        }

        

        public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            byEntity.StopAnimation("insertgear");
            byEntity.StopAnimation("knifecut");

            if (byEntity.LeftHandItemSlot?.Itemstack?.Collectible is ItemTemporalGear)
            {
                return;
            }

            if (entitySel == null) return;


            EntityBehaviorHarvestable bh = entitySel.Entity.GetBehavior<EntityBehaviorHarvestable>();
            //byEntity.World.Logger.Debug("{0} knife interact stop, seconds used {1} / {2}, entity: {3}", byEntity.World.Side, secondsUsed, bh?.HarvestDuration, entitySel.Entity);

            if (bh != null && bh.Harvestable && secondsUsed >= KnifeHarvestingSpeed * bh.GetHarvestDuration(byEntity) - 0.1f)
            {
                bh.SetHarvested((byEntity as EntityPlayer)?.Player);
                slot?.Itemstack?.Collectible.DamageItem(byEntity.World, byEntity, slot, 3);
            }
        }


        public override bool OnHeldInteractCancel(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, EnumItemUseCancelReason cancelReason)
        {
            //byEntity.World.Logger.Debug("{0} knife interact cancelled, seconds used {1}", byEntity.World.Side, secondsUsed);
            byEntity.StopAnimation("knifecut");
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


    }
}
