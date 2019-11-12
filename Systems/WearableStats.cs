using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{

    public class ModSystemWearableStats : ModSystem
    {
        ICoreAPI api;

        ICoreClientAPI capi;
        
        public override bool ShouldLoad(EnumAppSide forSide)
        {
            return true;
        }

        public override void Start(ICoreAPI api)
        {
            this.api = api;
        }


        public override void StartClientSide(ICoreClientAPI api)
        {
            base.StartClientSide(api);

            api.Event.LevelFinalize += Event_LevelFinalize;

            capi = api;
        }

        private void Event_LevelFinalize()
        {
            capi.World.Player.Entity.OnFootStep = () => onFootStep(capi.World.Player.Entity);
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);

            api.Event.PlayerJoin += Event_PlayerJoin;
        }

        private void Event_PlayerJoin(IServerPlayer byPlayer)
        {
            IInventory inv = byPlayer.InventoryManager.GetOwnInventory(GlobalConstants.characterInvClassName);
            inv.SlotModified += (slotid) => updateWearableStats(inv, byPlayer);

            var bh = byPlayer.Entity.GetBehavior<EntityBehaviorHealth>();
            if (bh != null) bh.onDamaged = (dmg, dmgSource) => handleDamaged(byPlayer, dmg, dmgSource);

            byPlayer.Entity.OnFootStep = () => onFootStep(byPlayer.Entity);

            updateWearableStats(inv, byPlayer);
        }

        private void onFootStep(EntityPlayer entity)
        {
            IInventory gearInv = entity.GearInventory;

            foreach (var slot in gearInv)
            {
                ItemWearable item;
                if (slot.Empty || (item = slot.Itemstack.Collectible as ItemWearable)==null) continue;

                AssetLocation[] soundlocs = item.FootStepSounds;
                if (soundlocs == null || soundlocs.Length == 0) continue;

                AssetLocation loc = soundlocs[api.World.Rand.Next(soundlocs.Length)];

                float pitch = (float)api.World.Rand.NextDouble() * 0.4f + 0.8f;
                api.World.PlaySoundAt(loc, entity, api.Side == EnumAppSide.Server ? entity.Player : null, pitch, 16f, 1f);
            }
        }

        private float handleDamaged(IPlayer player, float damage, DamageSource dmgSource)
        {
            // Does not protect against non-attack damages
            EnumDamageType type = dmgSource.Type;
            if (type != EnumDamageType.BluntAttack && type != EnumDamageType.PiercingAttack && type != EnumDamageType.SlashingAttack) return damage;

            ItemSlot armorSlot;
            IInventory inv = player.InventoryManager.GetOwnInventory(GlobalConstants.characterInvClassName);
            double rnd = api.World.Rand.NextDouble();


            if ((rnd -= 0.2) < 0)
            {
                // Head
                armorSlot = inv[12];
            }
            else if ((rnd -= 0.5) < 0)
            {
                // Body
                armorSlot = inv[13];
            }
            else
            {
                // Legs
                armorSlot = inv[14];
            }

            // Apply full damage if no armor is in this slot
            if (armorSlot.Empty || !(armorSlot.Itemstack.Item is ItemWearable)) return damage;

            ProtectionModifiers protMods = (armorSlot.Itemstack.Item as ItemWearable).ProtectionModifiers;

            int weaponTier = dmgSource.DamageTier;
            float flatDmgProt = protMods.FlatDamageReduction;
            float percentProt = protMods.RelativeProtection;
            bool aboveTier = weaponTier > protMods.ProtectionTier;

            for (int i = 0; i < weaponTier; i++)
            {
                float flatLoss = aboveTier ? protMods.PerTierFlatDamageReductionLoss[1] : protMods.PerTierFlatDamageReductionLoss[0];
                float percLoss = aboveTier ? protMods.PerTierRelativeProtectionLoss[1] : protMods.PerTierRelativeProtectionLoss[0];

                flatDmgProt -= flatLoss;
                percentProt -= percLoss;
            }

            damage -= Math.Max(0, flatDmgProt);
            damage *= 1 - Math.Max(0, percentProt);

            armorSlot.Itemstack.Collectible.DamageItem(api.World, player.Entity, armorSlot, 1 + (api.World.Rand.NextDouble() > 0.5 ? Math.Max(0, weaponTier - protMods.ProtectionTier) : 0));

            return damage;
        }


        private void updateWearableStats(IInventory inv, IServerPlayer player)
        {
            StatModifiers allmod = new StatModifiers();

            foreach (var slot in inv)
            {
                if (slot.Empty || !(slot.Itemstack.Item is ItemWearable)) continue;
                StatModifiers statmod = (slot.Itemstack.Item as ItemWearable).StatModifers;
                if (statmod == null) continue;

                allmod.canEat &= statmod.canEat;
                allmod.healingeffectivness += statmod.healingeffectivness;
                allmod.hungerrate += statmod.hungerrate;
                allmod.walkSpeed += statmod.walkSpeed;
                allmod.rangedWeaponsAcc += statmod.rangedWeaponsAcc;
                allmod.rangedWeaponsSpeed += statmod.rangedWeaponsSpeed;
            }

            EntityPlayer entity = player.Entity;
            entity.Stats
                .Set("walkspeed", "wearablemod", allmod.walkSpeed, true)
                .Set("healingeffectivness", "wearablemod", allmod.healingeffectivness, true)
                .Set("hungerrate", "wearablemod", allmod.hungerrate, true)
                .Set("rangedWeaponsAcc", "wearablemod", allmod.rangedWeaponsAcc, true)
                .Set("rangedWeaponsSpeed", "wearablemod", allmod.rangedWeaponsSpeed, true)
            ;

            entity.WatchedAttributes.SetBool("canEat", allmod.canEat);

            
        }

    }
}
