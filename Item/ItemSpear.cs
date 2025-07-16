using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent
{

    public class ItemSpear : Item
    {
        bool isHackingSpear;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            var bh = GetCollectibleBehavior<CollectibleBehaviorAnimationAuthoritative>(true);

            if (bh == null)
            {
                api.World.Logger.Warning("Spear {0} uses ItemSpear class, but lacks required AnimationAuthoritative behavior. I'll take the freedom to add this behavior, but please fix json item type.", Code);
                bh = new CollectibleBehaviorAnimationAuthoritative(this);
                bh.OnLoaded(api);
                CollectibleBehaviors = CollectibleBehaviors.Append(bh);
            }

            bh.OnBeginHitEntity += ItemSpear_OnBeginHitEntity;

            isHackingSpear = Attributes.IsTrue("hacking") == true;
        }

        private void ItemSpear_OnBeginHitEntity(EntityAgent byEntity, ref EnumHandling handling)
        {
            if (byEntity.World.Side == EnumAppSide.Client)
            {
                return;
            }

            var entitySel = (byEntity as EntityPlayer)?.EntitySelection;

            if (byEntity.Attributes.GetInt("didattack") == 0 && entitySel != null)
            {
                byEntity.Attributes.SetInt("didattack", 1);

                var slot = byEntity.ActiveHandItemSlot;

                bool canhackEntity =
                    entitySel.Entity.Properties.Attributes?["hackedEntity"].Exists == true
                    && isHackingSpear 
                    && api.ModLoader.GetModSystem<CharacterSystem>().HasTrait((byEntity as EntityPlayer).Player, "technical")
                ;

                ICoreServerAPI sapi = api as ICoreServerAPI;

                if (canhackEntity)
                {
                    sapi.World.PlaySoundAt(new AssetLocation("sounds/player/hackingspearhit.ogg"), entitySel.Entity, null);
                }

                if (api.World.Rand.NextDouble() < 0.15 && canhackEntity)
                {
                    SpawnEntityInPlaceOf(entitySel.Entity, entitySel.Entity.Properties.Attributes["hackedEntity"].AsString(), byEntity);
                    sapi.World.DespawnEntity(entitySel.Entity, new EntityDespawnData() { Reason = EnumDespawnReason.Removed });
                }
            }
        }

        public override string GetHeldTpUseAnimation(ItemSlot activeHotbarSlot, Entity byEntity)
        {
            return null;
        }

        public override void OnHeldInteractStart(ItemSlot itemslot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            base.OnHeldInteractStart(itemslot, byEntity, blockSel, entitySel, firstEvent, ref handling);
            if (handling == EnumHandHandling.PreventDefault) return;

            handling = EnumHandHandling.PreventDefault;

            // Not ideal to code the aiming controls this way. Needs an elegant solution - maybe an event bus?
            byEntity.Attributes.SetInt("aiming", 1);
            byEntity.Attributes.SetInt("aimingCancel", 0);
            byEntity.StartAnimation("aim");
        }

        public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
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
            Entity enpr = byEntity.World.ClassRegistry.CreateEntity(type);
            IProjectile projectile = enpr as IProjectile;
            projectile.FiredBy = byEntity;
            projectile.Damage = damage;
            projectile.DamageTier = Attributes["damageTier"].AsInt(0);
            projectile.ProjectileStack = stack;
            projectile.DropOnImpactChance = 1.1f;
            projectile.DamageStackOnImpact = true;
            projectile.Weight = 0.3f;
            projectile.IgnoreInvFrames = Attributes["ignoreInvFrames"].AsBool(false);

            EntityProjectile.SpawnThrownEntity(enpr, byEntity, 0.75, -0.2, 0, 0.65 * byEntity.Stats.GetBlended("bowDrawingStrength"), 0.15);
            byEntity.StartAnimation("throw");

            if (byEntity is EntityPlayer) RefillSlotIfEmpty(slot, byEntity, (itemstack) => itemstack.Collectible is ItemSpear);

            var pitch = (byEntity as EntityPlayer).talkUtil.pitchModifier;
            byPlayer.Entity.World.PlaySoundAt(new AssetLocation("sounds/player/strike"), byPlayer.Entity, byPlayer, pitch * 0.9f + (float)api.World.Rand.NextDouble() * 0.2f, 16, 0.35f);
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
