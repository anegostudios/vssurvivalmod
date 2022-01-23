using System;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    

    public class ItemSpear : Item
    {
        public override string GetHeldTpUseAnimation(ItemSlot activeHotbarSlot, Entity byEntity)
        {
            return null;
        }

        public override void OnHeldInteractStart(ItemSlot itemslot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            if (!byEntity.Controls.TriesToMove && byEntity.Controls.Sprint)
            {
                return;
            }

            handling = EnumHandHandling.PreventDefault;

            // Not ideal to code the aiming controls this way. Needs an elegant solution - maybe an event bus?
            byEntity.Attributes.SetInt("aiming", 1);
            byEntity.Attributes.SetInt("aimingCancel", 0);
            byEntity.StartAnimation("aim");
        }

        public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (byEntity.World is IClientWorldAccessor)
            {
                ModelTransform tf = new ModelTransform();
                tf.EnsureDefaultValues();

                float offset = GameMath.Serp(0, 2, GameMath.Clamp(secondsUsed * 4f, 0, 2f) / 2f);

                tf.Translation.Set(0, offset / 5, offset / 3);
                tf.Rotation.Set(offset * 10, 0, 0);
                byEntity.Controls.UsingHeldItemTransformAfter = tf;
            }

            return true;
        }


        public override bool OnHeldInteractCancel(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, EnumItemUseCancelReason cancelReason)
        {
            byEntity.Attributes.SetInt("aiming", 0);
            byEntity.StopAnimation("aim");

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
            byEntity.StopAnimation("aim");

            if (secondsUsed < 0.35f) return;

            float damage = 1.5f;

            if (slot.Itemstack.Collectible.Attributes != null)
            {
                damage = slot.Itemstack.Collectible.Attributes["damage"].AsFloat(0);
            }

            (api as ICoreClientAPI)?.World.AddCameraShake(0.17f);

            ItemStack stack = slot.TakeOut(1);
            slot.MarkDirty();
            
            IPlayer byPlayer = null;
            if (byEntity is EntityPlayer) byPlayer = byEntity.World.PlayerByUid(((EntityPlayer)byEntity).PlayerUID);
            byEntity.World.PlaySoundAt(new AssetLocation("sounds/player/throw"), byEntity, byPlayer, false, 8);

            EntityProperties type = byEntity.World.GetEntityType(new AssetLocation(Attributes["spearEntityCode"].AsString()));
            EntityProjectile enpr = byEntity.World.ClassRegistry.CreateEntity(type) as EntityProjectile;
            enpr.FiredBy = byEntity;
            enpr.Damage = damage;
            enpr.ProjectileStack = stack;
            enpr.DropOnImpactChance = 1.1f;
            enpr.DamageStackOnImpact = true;
            enpr.Weight = 0.3f;


            float acc = (1 - byEntity.Attributes.GetFloat("aimingAccuracy", 0));
            double rndpitch = byEntity.WatchedAttributes.GetDouble("aimingRandPitch", 1) * acc * 0.75;
            double rndyaw = byEntity.WatchedAttributes.GetDouble("aimingRandYaw", 1) * acc * 0.75;

            Vec3d pos = byEntity.ServerPos.XYZ.Add(0, byEntity.LocalEyePos.Y - 0.2, 0);

            Vec3d aheadPos = pos.AheadCopy(1, byEntity.ServerPos.Pitch + rndpitch, byEntity.ServerPos.Yaw + rndyaw);
            Vec3d velocity = (aheadPos - pos) * 0.65;
            Vec3d spawnPos = byEntity.ServerPos.BehindCopy(0.21).XYZ.Add(byEntity.LocalEyePos.X, byEntity.LocalEyePos.Y - 0.2, byEntity.LocalEyePos.Z);

            enpr.ServerPos.SetPos(spawnPos);
            enpr.ServerPos.Motion.Set(velocity);


            enpr.Pos.SetFrom(enpr.ServerPos);
            enpr.World = byEntity.World;
            enpr.SetRotation();

            byEntity.World.SpawnEntity(enpr);
            byEntity.StartAnimation("throw");

            if (byEntity is EntityPlayer) RefillSlotIfEmpty(slot, byEntity, (itemstack) => itemstack.Collectible is ItemSpear);

            byPlayer.Entity.World.PlaySoundAt(new AssetLocation("sounds/player/strike"), byPlayer.Entity, byPlayer, 0.9f + (float)api.World.Rand.NextDouble() * 0.2f, 16, 0.5f);
        }




        public override void OnHeldAttackStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandHandling handling)
        {
            byEntity.Attributes.SetInt("didattack", 0);

            byEntity.World.RegisterCallback((dt) =>
            {
                IPlayer byPlayer = (byEntity as EntityPlayer).Player;
                if (byPlayer == null) return;

                if (byEntity.Controls.HandUse == EnumHandInteract.HeldItemAttack)
                {
                    byPlayer.Entity.World.PlaySoundAt(new AssetLocation("sounds/player/strike"), byPlayer.Entity, byPlayer, 0.9f + (float)api.World.Rand.NextDouble() * 0.2f, 16, 0.5f);
                }
            }, 464);

            handling = EnumHandHandling.PreventDefault;
        }

        public override bool OnHeldAttackCancel(float secondsPassed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSelection, EntitySelection entitySel, EnumItemUseCancelReason cancelReason)
        {
            return false;
        }

        public override bool OnHeldAttackStep(float secondsPassed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSelection, EntitySelection entitySel)
        {
            float backwards = -Math.Min(0.8f, 3 * secondsPassed);
            float stab = Math.Min(1.2f, 20 * Math.Max(0, secondsPassed - 0.25f));

            if (byEntity.World.Side == EnumAppSide.Client)
            {
                IClientWorldAccessor world = byEntity.World as IClientWorldAccessor;
                ModelTransform tf = new ModelTransform();
                tf.EnsureDefaultValues();

              
                float sum = stab + backwards;
                float ztranslation = Math.Min(0.2f, 1.5f * secondsPassed);
                float easeout = Math.Max(0, 2 * (secondsPassed - 1));

                if (secondsPassed > 0.4f) sum = Math.Max(0, sum - easeout);
                ztranslation = Math.Max(0, ztranslation - easeout);

                tf.Translation.Set(-1f * sum, ztranslation * 0.4f, -sum * 0.8f * 2.6f);
                tf.Rotation.Set(-sum * 9, sum * 30, -sum*30);

                byEntity.Controls.UsingHeldItemTransformAfter = tf;
                

                if (stab > 1.15f && byEntity.Attributes.GetInt("didattack") == 0)
                {
                    world.TryAttackEntity(entitySel);
                    byEntity.Attributes.SetInt("didattack", 1);
                    world.AddCameraShake(0.25f);
                }
            } else
            {
                if (stab > 1.15f && byEntity.Attributes.GetInt("didattack") == 0 && entitySel != null)
                {
                    byEntity.Attributes.SetInt("didattack", 1);

                    bool canhackEntity =
                        entitySel.Entity.Properties.Attributes?["hackedEntity"].Exists == true
                        && slot.Itemstack.ItemAttributes.IsTrue("hacking") == true && api.ModLoader.GetModSystem<CharacterSystem>().HasTrait((byEntity as EntityPlayer).Player, "technical")
                    ;
                    ICoreServerAPI sapi = api as ICoreServerAPI;

                    if (canhackEntity)
                    {
                        sapi.World.PlaySoundAt(new AssetLocation("sounds/player/hackingspearhit.ogg"), entitySel.Entity, null);
                    }

                    if (api.World.Rand.NextDouble() < 0.15 && canhackEntity)
                    {
                        SpawnEntityInPlaceOf(entitySel.Entity, entitySel.Entity.Properties.Attributes["hackedEntity"].AsString(), byEntity);
                        sapi.World.DespawnEntity(entitySel.Entity, new EntityDespawnReason() { reason = EnumDespawnReason.Removed });
                    }
                }
            }

            return secondsPassed < 1.2f;
        }


        private void SpawnEntityInPlaceOf(Entity byEntity, string code, EntityAgent causingEntity)
        {
            AssetLocation location = AssetLocation.Create(code, byEntity.Code.Domain);
            EntityProperties type = byEntity.World.GetEntityType(location);
            if (type == null)
            {
                byEntity.World.Logger.Error("ItemCreature: No such entity - {0}", location);
                if (api.World.Side == EnumAppSide.Client)
                {
                    (api as ICoreClientAPI).TriggerIngameError(this, "nosuchentity", string.Format("No such entity loaded - '{0}'.", location));
                }
                return;
            }

            Entity entity = byEntity.World.ClassRegistry.CreateEntity(type);

            if (entity != null)
            {
                entity.ServerPos.X = byEntity.ServerPos.X;
                entity.ServerPos.Y = byEntity.ServerPos.Y;
                entity.ServerPos.Z = byEntity.ServerPos.Z;
                entity.ServerPos.Motion.X = byEntity.ServerPos.Motion.X;
                entity.ServerPos.Motion.Y = byEntity.ServerPos.Motion.Y;
                entity.ServerPos.Motion.Z = byEntity.ServerPos.Motion.Z;
                entity.ServerPos.Yaw = byEntity.ServerPos.Yaw;

                entity.Pos.SetFrom(entity.ServerPos);
                entity.PositionBeforeFalling.Set(entity.ServerPos.X, entity.ServerPos.Y, entity.ServerPos.Z);

                entity.Attributes.SetString("origin", "playerplaced");

                
                entity.WatchedAttributes.SetLong("guardedEntityId", byEntity.EntityId);
                if (causingEntity is EntityPlayer eplr)
                {
                    entity.WatchedAttributes.SetString("guardedPlayerUid", eplr.PlayerUID);
                }

                byEntity.World.SpawnEntity(entity);
            }
        }

        public override void OnHeldAttackStop(float secondsPassed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSelection, EntitySelection entitySel)
        {

        }



        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
            if (inSlot.Itemstack.Collectible.Attributes == null) return;

            float damage = 1.5f;

            if (inSlot.Itemstack.Collectible.Attributes != null)
            {
                damage = inSlot.Itemstack.Collectible.Attributes["damage"].AsFloat(0);
            }

            dsc.AppendLine(damage + Lang.Get("piercing-damage-thrown"));
        }

        public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
        {
            return new WorldInteraction[] {
                new WorldInteraction()
                {
                    ActionLangCode = "heldhelp-throw",
                    MouseButton = EnumMouseButton.Right,
                }
            }.Append(base.GetHeldInteractionHelp(inSlot));
        }
    }
}
