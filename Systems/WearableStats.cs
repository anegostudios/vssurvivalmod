using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

#nullable disable
using static Vintagestory.API.Common.EntityAgent;

namespace Vintagestory.GameContent
{
    public class ModSystemWearableStats : ModSystem
    {
        ICoreAPI api;
        ICoreClientAPI capi;

        Dictionary<int, EnumCharacterDressType[]> clothingDamageTargetsByAttackTacket = new Dictionary<int, EnumCharacterDressType[]>()
        {
            { 0, new EnumCharacterDressType[] { EnumCharacterDressType.Head, EnumCharacterDressType.Face, EnumCharacterDressType.Neck } },
            { 1, new EnumCharacterDressType[] { EnumCharacterDressType.UpperBody, EnumCharacterDressType.UpperBodyOver, EnumCharacterDressType.Shoulder, EnumCharacterDressType.Arm, EnumCharacterDressType.Hand } },
            { 2, new EnumCharacterDressType[] { EnumCharacterDressType.LowerBody, EnumCharacterDressType.Foot } }
        };

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
            capi.World.Player.Entity.OnFootStep += () => onFootStep(capi.World.Player.Entity);
            capi.World.Player.Entity.OnImpact += (motionY) => onFallToGround(capi.World.Player.Entity, motionY);
            var bh = capi.World.Player.Entity.GetBehavior<EntityBehaviorHealth>();
            if (bh != null) bh.onDamaged += (dmg, dmgSource) => handleDamaged(capi.World.Player, dmg, dmgSource);
            capi.Logger.VerboseDebug("Done wearable stats");
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
            if (bh != null) bh.onDamaged += (dmg, dmgSource) => handleDamaged(byPlayer, dmg, dmgSource);

            byPlayer.Entity.OnFootStep += () => onFootStep(byPlayer.Entity);
            byPlayer.Entity.OnImpact += (motionY) => onFallToGround(byPlayer.Entity, motionY);

            updateWearableStats(inv, byPlayer);
        }


        private void onFallToGround(EntityPlayer entity, double motionY)
        {
            if (Math.Abs(motionY) > 0.1)
            {
                onFootStep(entity);
            }
        }


        private void onFootStep(EntityPlayer entity)
        {
            //IInventory gearInv = entity.GearInventory;
            var inv = entity.GetBehavior<EntityBehaviorPlayerInventory>()?.Inventory;
            if (inv == null) return;

            foreach (var slot in inv)
            {
                if (slot.Empty || slot.Itemstack.Collectible.GetCollectibleInterface<IWearableStatsSupplier>() is not IWearableStatsSupplier wearableStats) continue;

                AssetLocation[] soundlocs = wearableStats.GetFootStepSounds(slot);
                if (soundlocs == null || soundlocs.Length == 0) continue;

                AssetLocation loc = soundlocs[api.World.Rand.Next(soundlocs.Length)];

                float pitch = (float)api.World.Rand.NextDouble() * 0.5f + 0.7f;
                float volume = entity.Player.Entity.Controls.Sneak ? 0.5f : 1f + (float)api.World.Rand.NextDouble() * 0.3f + 0.7f;
                api.World.PlaySoundAt(loc, entity, api.Side == EnumAppSide.Server ? entity.Player : null, pitch, 16f, volume);
            }
        }

        AssetLocation ripSound = new AssetLocation("sounds/effect/clothrip");

        private float handleDamaged(IPlayer player, float damage, DamageSource dmgSource)
        {
            EnumDamageType type = dmgSource.Type;

            // Reduce damage if player holds a shield
            damage = applyShieldProtection(player, damage, dmgSource);

            if (damage <= 0) return 0;
            // The code below only the server needs to execute
            if (api.Side == EnumAppSide.Client) return damage;

            // Does not protect against non-attack damages

            if (type != EnumDamageType.BluntAttack && type != EnumDamageType.PiercingAttack && type != EnumDamageType.SlashingAttack) return damage;
            if (dmgSource.Source == EnumDamageSource.Internal || dmgSource.Source == EnumDamageSource.Suicide) return damage;

            ItemSlot armorSlot;
            IInventory inv = player.InventoryManager.GetOwnInventory(GlobalConstants.characterInvClassName);
            double rnd = api.World.Rand.NextDouble();

            int attackTarget;

            if ((rnd -= 0.2) < 0)
            {
                // Head
                armorSlot = inv[12];
                attackTarget = 0;
            }
            else if ((rnd -= 0.5) < 0)
            {
                // Body
                armorSlot = inv[13];
                attackTarget = 1;
            }
            else
            {
                // Legs
                armorSlot = inv[14];
                attackTarget = 2;
            }

            // Apply full damage if no armor is in this slot
            if (armorSlot.Empty
                || !(armorSlot.Itemstack.Collectible.GetCollectibleInterface<IWearableStatsSupplier>() is IWearableStatsSupplier wearableStats && wearableStats.IsArmorType(armorSlot))
                || armorSlot.Itemstack.Collectible.GetRemainingDurability(armorSlot.Itemstack) <= 0)
            {
                EnumCharacterDressType[] dressTargets = clothingDamageTargetsByAttackTacket[attackTarget];
                EnumCharacterDressType target = dressTargets[api.World.Rand.Next(dressTargets.Length)];

                ItemSlot targetslot = inv[(int)target];
                //ItemSlot targetslot = player.Entity.GearInventory[(int)target];
                if (!targetslot.Empty)
                {
                    // Wolf: 10 hp damage = 10% condition loss
                    // Ram: 10 hp damage = 2.5% condition loss
                    // Bronze locust: 10 hp damage = 5% condition loss
                    float mul = 0.25f;
                    if (type == EnumDamageType.SlashingAttack) mul = 1f;
                    if (type == EnumDamageType.PiercingAttack) mul = 0.5f;

                    float diff = -damage / 100 * mul;

                    if (Math.Abs(diff) > 0.05)
                    {
                        api.World.PlaySoundAt(ripSound, player.Entity);
                    }

                    targetslot.Itemstack.Collectible.GetCollectibleInterface<IWearable>()?.ChangeCondition(targetslot, diff);
                }

                return damage;
            }

            ProtectionModifiers protMods = armorSlot.Itemstack.Collectible.GetCollectibleInterface<IWearableStatsSupplier>().GetProtectionModifiers(armorSlot);

            int weaponTier = dmgSource.DamageTier;
            float flatDmgProt = protMods.FlatDamageReduction;
            float percentProt = protMods.RelativeProtection;

            for (int tier = 1; tier <= weaponTier; tier++)
            {
                bool aboveTier = tier > protMods.ProtectionTier;

                float flatLoss = aboveTier ? protMods.PerTierFlatDamageReductionLoss[1] : protMods.PerTierFlatDamageReductionLoss[0];
                float percLoss = aboveTier ? protMods.PerTierRelativeProtectionLoss[1] : protMods.PerTierRelativeProtectionLoss[0];

                if (aboveTier && protMods.HighDamageTierResistant)
                {
                    flatLoss /= 2;
                    percLoss /= 2;
                }

                flatDmgProt -= flatLoss;
                percentProt *= 1 - percLoss;
            }

            // Durability loss is the one before the damage reductions
            float durabilityLoss = 0.5f + damage * Math.Max(0.5f, (weaponTier - protMods.ProtectionTier) * 3);
            int durabilityLossInt = GameMath.RoundRandom(api.World.Rand, durabilityLoss);

            // Now reduce the damage
            damage = Math.Max(0, damage - flatDmgProt);
            damage *= 1 - Math.Max(0, percentProt);

            armorSlot.Itemstack.Collectible.DamageItem(api.World, player.Entity, armorSlot, durabilityLossInt);

            if (armorSlot.Empty)
            {
                api.World.PlaySoundAt(new AssetLocation("sounds/effect/toolbreak"), player);
            }

            return damage;
        }


        private float applyShieldProtection(IPlayer player, float damage, DamageSource dmgSource)
        {
            double horizontalAngleProtectionRange = 120 / 2 * GameMath.DEG2RAD;
            float unabsorbedDamage = damage;

            ItemSlot[] shieldSlots = new ItemSlot[] { player.Entity.LeftHandItemSlot, player.Entity.RightHandItemSlot };

            for (int i = 0; i < shieldSlots.Length; i++)
            {
                var shieldSlot = shieldSlots[i];
                var attr = shieldSlot.Itemstack?.ItemAttributes?["shield"];
                if (attr == null || !attr.Exists) continue;

                bool projectile = dmgSource.SourceEntity?.Properties.Attributes?["isProjectile"].AsBool(false) ?? false;
                string usetype = player.Entity.Controls.Sneak && player.Entity.Attributes.GetInt("aiming") != 1 ? "active" : "passive";

                float flatdmgabsorb = 0;
                float chance = 0;
                if (projectile && attr["protectionChance"][usetype + "-projectile"].Exists)
                {
                    chance = attr["protectionChance"][usetype + "-projectile"].AsFloat(0);
                    flatdmgabsorb = attr["projectileDamageAbsorption"].AsFloat(2);
                } else
                {
                    chance = attr["protectionChance"][usetype].AsFloat(0);
                    flatdmgabsorb = attr["damageAbsorption"].AsFloat(2);
                }

                if (!dmgSource.GetAttackAngle(player.Entity.Pos.XYZ, out var attackYaw, out var attackPitch))
                {
                    break;
                }

                bool verticalAttack = Math.Abs(attackPitch) > 65 * GameMath.DEG2RAD;
                double playerYaw = player.Entity.Pos.Yaw;
                double playerPitch = GameMath.PI - player.Entity.Pos.Pitch;

                if (projectile)
                {
                    var dx = dmgSource.SourceEntity.Pos.Motion.X;
                    var dy = dmgSource.SourceEntity.Pos.Motion.Y;
                    var dz = dmgSource.SourceEntity.Pos.Motion.Z;
                    verticalAttack = Math.Sqrt(dx * dx + dz * dz) * 1.2f < Math.Abs(dy);
                }

                bool inProtectionRange;
                if (verticalAttack)
                {
                    inProtectionRange = Math.Abs(GameMath.AngleRadDistance((float)playerPitch, (float)attackPitch)) < 30 * GameMath.DEG2RAD;
                } else
                {
                    inProtectionRange = Math.Abs(GameMath.AngleRadDistance((float)playerYaw, (float)attackYaw)) < horizontalAngleProtectionRange;
                }

                if (inProtectionRange)
                {
                    float totaldmgabsorb = 0;
                    var rndval = api.World.Rand.NextDouble();

                    if (rndval < chance)
                    {
                        totaldmgabsorb += flatdmgabsorb;
                    }

                    (player as IServerPlayer)?.SendMessage(GlobalConstants.DamageLogChatGroup, Lang.Get("{0:0.#} of {1:0.#} damage blocked by shield ({2} use)", Math.Min(totaldmgabsorb, damage), damage, usetype), EnumChatType.Notification);
                    damage = Math.Max(0, damage - totaldmgabsorb);

                    string key = "blockSound" + ((unabsorbedDamage > 6) ? "Heavy" : "Light");
                    var loc = shieldSlot.Itemstack.ItemAttributes["shield"][key].AsString("held/shieldblock-wood-light");
                    var sloc = AssetLocation.Create(loc, shieldSlot.Itemstack.Collectible.Code.Domain).WithPathPrefixOnce("sounds/").WithPathAppendixOnce(".ogg");
                    api.World.PlaySoundAt(sloc, player, null);

                    if (rndval < chance) (api as ICoreServerAPI).Network.BroadcastEntityPacket(player.Entity.EntityId, (int)EntityServerPacketId.PlayPlayerAnim, SerializerUtil.Serialize("shieldBlock" + ((i == 0) ? "L" : "R")));


                    if (api.Side == EnumAppSide.Server)
                    {
                        shieldSlot.Itemstack.Collectible.DamageItem(api.World, dmgSource.SourceEntity, shieldSlot, 1);
                        shieldSlot.MarkDirty();
                    }
                }
            }

            return damage;
        }

        private void updateWearableStats(IInventory inv, IServerPlayer player)
        {
            StatModifiers allmod = new StatModifiers();

            float walkSpeedmul = player.Entity.Stats.GetBlended("armorWalkSpeedAffectedness");

            foreach (var slot in inv)
            {
                if (slot.Empty || slot.Itemstack.Collectible.GetCollectibleInterface<IWearableStatsSupplier>() is not IWearableStatsSupplier wearableStats) continue;
                StatModifiers statmod = wearableStats.GetStatModifiers(slot);
                if (statmod == null) continue;

                // No positive effects when broken
                bool broken = slot.Itemstack.Collectible.GetRemainingDurability(slot.Itemstack) == 0;

                allmod.canEat &= statmod.canEat;
                allmod.healingeffectivness += broken ? Math.Min(0, statmod.healingeffectivness) : statmod.healingeffectivness;
                allmod.hungerrate += broken ? Math.Max(0, statmod.hungerrate) : statmod.hungerrate;

                if (statmod.walkSpeed < 0)
                {
                    allmod.walkSpeed += statmod.walkSpeed * walkSpeedmul;
                } else
                {
                    allmod.walkSpeed += broken ? 0 : statmod.walkSpeed;
                }

                allmod.rangedWeaponsAcc += broken ? Math.Min(0, statmod.rangedWeaponsAcc) : statmod.rangedWeaponsAcc;
                allmod.rangedWeaponsSpeed += broken ? Math.Min(0, statmod.rangedWeaponsSpeed) : statmod.rangedWeaponsSpeed;
            }

            EntityPlayer entity = player.Entity;
            entity.Stats
                .Set("walkspeed", "wearablemod", allmod.walkSpeed, true)
                .Set("healingeffectivness", "wearablemod", allmod.healingeffectivness, true)
                .Set("hungerrate", "wearablemod", allmod.hungerrate, true)
                .Set("rangedWeaponsAcc", "wearablemod", allmod.rangedWeaponsAcc, true)
                .Set("rangedWeaponsSpeed", "wearablemod", allmod.rangedWeaponsSpeed, true)
            ;
            entity.walkSpeed = entity.Stats.GetBlended("walkspeed");

            entity.WatchedAttributes.SetBool("canEat", allmod.canEat);
        }

    }
}
