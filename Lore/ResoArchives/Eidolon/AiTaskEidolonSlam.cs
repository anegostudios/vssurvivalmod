using ProtoBuf;
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
    [ProtoContract]
    public class ScreenshakePacket
    {
        [ProtoMember(1)]
        public float Strength;
    }

    public class ScreenshakeToClientModSystem : ModSystem {

        ICoreClientAPI capi;
        ICoreServerAPI sapi;

        public override void Start(ICoreAPI api)
        {
            api.Network.RegisterChannel("screenshake").RegisterMessageType<ScreenshakePacket>();
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;
        }

        public void ShakeScreen(Vec3d pos, float strength, float range)
        {
            foreach (IServerPlayer plr in sapi.World.AllOnlinePlayers)
            {
                if (plr.ConnectionState != EnumClientState.Playing) continue;

                float dist = (float)plr.Entity.Pos.DistanceTo(pos);

                // https://pfortuny.net/fooplot.com/#W3sidHlwZSI6MCwiZXEiOiJtaW4oMSwoMjAteCkveCkiLCJjb2xvciI6IiMwMDAwMDAifSx7InR5cGUiOjEwMDAsIndpbmRvdyI6WyIwIiwiMjAiLCIwIiwiMSJdLCJzaXplIjpbNjQ4LDM5OF19XQ--
                float str = Math.Min(1, (range - dist)/dist) * strength;
                if (str > 0.05)
                {
                    sapi.Network.GetChannel("screenshake").SendPacket(new ScreenshakePacket() { Strength = str }, plr);
                }

            }
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            capi = api;
            api.Network.GetChannel("screenshake").SetMessageHandler<ScreenshakePacket>(onScreenshakePacket);
        }

        private void onScreenshakePacket(ScreenshakePacket packet)
        {
            capi.World.AddCameraShake(packet.Strength);
        }
    }



    public class AiTaskEidolonSlam : AiTaskBaseTargetable
    {
        int durationMs;
        int releaseAtMs;
        long lastSearchTotalMs;
        protected int searchWaitMs = 2000;
        float maxDist = 15f;

        float projectileDamage;
        int projectileDamageTier;
        AssetLocation projectileCode;

        public float creatureSpawnChance = 0f;
        public float creatureSpawnCount = 6.5f;
        AssetLocation creatureCode;

        public float spawnRange;
        public float spawnHeight;
        public float spawnAmount;


        // Runtime fields
        float accum;
        bool didSpawn;
        int creaturesLeftToSpawn;


        public AiTaskEidolonSlam(EntityAgent entity, JsonObject taskConfig, JsonObject aiConfig) : base(entity, taskConfig, aiConfig)
        {
            this.durationMs = taskConfig["durationMs"].AsInt(1500);
            this.releaseAtMs = taskConfig["releaseAtMs"].AsInt(1000);
            this.projectileDamage = taskConfig["projectileDamage"].AsFloat(1f);
            this.projectileDamageTier = taskConfig["projectileDamageTier"].AsInt(0);
            this.projectileCode = AssetLocation.Create(taskConfig["projectileCode"].AsString("thrownstone-{rock}"), entity.Code.Domain);
            if (taskConfig["creatureCode"].Exists)
            {
                this.creatureCode = AssetLocation.Create(taskConfig["creatureCode"].AsString(null), entity.Code.Domain);
            }

            this.spawnRange = taskConfig["spawnRange"].AsFloat(9f);
            this.spawnHeight = taskConfig["spawnHeight"].AsFloat(9f);
            this.spawnAmount = taskConfig["spawnAmount"].AsFloat(10f);

            this.maxDist = taskConfig["maxDist"].AsFloat(12f);
        }

        public override bool ShouldExecute()
        {
            // React immediately on hurt, otherwise only 1/10 chance of execution
            if (rand.NextDouble() > 0.1f && (WhenInEmotionStates == null || IsInEmotionState(WhenInEmotionStates) != true)) return false;

            if (!PreconditionsSatisfied()) return false;
            if (lastSearchTotalMs + searchWaitMs > entity.World.ElapsedMilliseconds) return false;
            if (WhenInEmotionStates == null && rand.NextDouble() > 0.5f) return false;
            if (cooldownUntilMs > entity.World.ElapsedMilliseconds) return false;

            float range = maxDist;
            lastSearchTotalMs = entity.World.ElapsedMilliseconds;

            targetEntity = partitionUtil.GetNearestEntity(entity.Pos.XYZ, range, (e) => IsTargetableEntity(e, range), EnumEntitySearchType.Creatures);

            return targetEntity != null;
        }



        public override void StartExecute()
        {
            base.StartExecute();
            accum = 0;
            didSpawn = false;
        }



        public override bool ContinueExecute(float dt)
        {

            base.ContinueExecute(dt);

            //Check if time is still valid for task.
            if (!IsInValidDayTimeHours(false)) return false;

            if (animMeta != null)
            {
                animMeta.EaseInSpeed = 1f;
                animMeta.EaseOutSpeed = 1f;
                entity.AnimManager.StartAnimation(animMeta);
            }

            accum += dt;

            var pos = entity.Pos.XYZ;
            float damage = 6f;

            if (accum > releaseAtMs / 1000f && !didSpawn)
            {
                didSpawn = true;
                var rnd = entity.World.Rand;

                if (entity.World.Rand.NextDouble() < creatureSpawnChance)
                {
                    int count = 0;
                    partitionUtil.WalkEntities(pos, 7f, (e) => { if (e.Code.Equals(creatureCode) && e.Alive) count++; return true; }, EnumEntitySearchType.Creatures);

                    creaturesLeftToSpawn = Math.Max(0, GameMath.RoundRandom(entity.World.Rand, creatureSpawnCount) - count);
                }

                for (int i = 0; i < spawnAmount; i++)
                {
                    float dx = (float)rnd.NextDouble() * 2 * spawnRange - spawnRange;
                    float dz = (float)rnd.NextDouble() * 2 * spawnRange - spawnRange;
                    float dy = spawnHeight;
                    spawnProjectile(dx, dy, dz);
                }

                // Damage and knockback nearby creatures
                partitionUtil.WalkEntities(pos, 9f, (e) =>
                {
                    if (e.EntityId == entity.EntityId || !e.IsInteractable) return true;
                    if (!e.Alive || !e.OnGround) return true;

                    double dist = e.Pos.DistanceTo(pos);
                    float attenuate = (float)(5 - dist) / 5f;
                    float dmg = Math.Max(0.02f, damage * GlobalConstants.CreatureDamageModifier * attenuate);
                    bool gotDamaged = e.ReceiveDamage(
                        new DamageSource()
                        {
                            Source = EnumDamageSource.Entity,
                            SourceEntity = entity,
                            Type = EnumDamageType.BluntAttack,
                            DamageTier = 1,
                            KnockbackStrength = 0
                        }, dmg
                    );

                    float kbStrength = GameMath.Clamp(10f - (float)dist, 0, 5);
                    // Opposite dir to pull you *towards* the eidolon >:D
                    Vec3d dir = (entity.Pos.XYZ - e.Pos.XYZ).Normalize();
                    dir.Y = 0.7f;
                    float factor = kbStrength * GameMath.Clamp((1 - e.Properties.KnockbackResistance) / 10f, 0, 1);

                    e.WatchedAttributes.SetFloat("onHurtDir", (float)Math.Atan2(dir.X, dir.Z));
                    e.WatchedAttributes.SetDouble("kbdirX", dir.X * factor);
                    e.WatchedAttributes.SetDouble("kbdirY", dir.Y * factor);
                    e.WatchedAttributes.SetDouble("kbdirZ", dir.Z * factor);
                    e.WatchedAttributes.SetInt("onHurtCounter", e.WatchedAttributes.GetInt("onHurtCounter") + 1);
                    e.WatchedAttributes.SetFloat("onHurt", 0.01f); // Causes the client to be notified

                    return true;
                }, EnumEntitySearchType.Creatures);

                // One boulder on every player as well >:D
                foreach (IServerPlayer plr in entity.World.AllOnlinePlayers)
                {
                    if (plr.ConnectionState != EnumClientState.Playing) continue;

                    double dx = plr.Entity.Pos.X - entity.Pos.X;
                    double dz = plr.Entity.Pos.Z - entity.Pos.Z;

                    if (Math.Abs(dx) <= spawnRange && Math.Abs(dz) <= spawnRange)
                    {
                        spawnProjectile((float)dx, spawnHeight, (float)dz);
                    }
                }

                entity.World.Api.ModLoader.GetModSystem<ScreenshakeToClientModSystem>().ShakeScreen(entity.Pos.XYZ, 1, 16);
            }

            return accum < durationMs / 1000f;
        }

        private void spawnProjectile(float dx, float dy, float dz)
        {
            if (creaturesLeftToSpawn > 0)
            {
                spawnCreature(dx, dy, dz);
                return;
            }

            var loc = projectileCode.Clone();
            string rocktype = "granite";
            var ba = entity.World.BlockAccessor;
            var mc = ba.GetMapChunkAtBlockPos(entity.Pos.AsBlockPos);
            if (mc != null)
            {
                int lz = (int)entity.Pos.Z % GlobalConstants.ChunkSize;
                int lx = (int)entity.Pos.X % GlobalConstants.ChunkSize;
                var rockBlock = entity.World.Blocks[mc.TopRockIdMap[lz * GlobalConstants.ChunkSize + lx]];
                rocktype = rockBlock.Variant["rock"] ?? "granite";
            }
            loc.Path = loc.Path.Replace("{rock}", rocktype);


            EntityProperties type = entity.World.GetEntityType(loc);
            var entitypr = entity.World.ClassRegistry.CreateEntity(type) as EntityThrownStone;

            entitypr.FiredBy = entity;
            entitypr.Damage = projectileDamage;
            entitypr.DamageTier = projectileDamageTier;
            entitypr.ProjectileStack = new ItemStack(entity.World.GetItem(new AssetLocation("stone-" + rocktype)));
            entitypr.NonCollectible = true;
            entitypr.VerticalImpactBreakChance = 1f;
            entitypr.ImpactParticleSize = 1.5f;
            entitypr.ImpactParticleCount = 30;

            entitypr.Pos.SetPosWithDimension(entity.Pos.XYZ.Add(dx, dy, dz));
            entitypr.World = entity.World;
            entity.World.SpawnEntity(entitypr);
        }

        private void spawnCreature(float dx, float dy, float dz)
        {
            EntityProperties type = entity.World.GetEntityType(creatureCode);
            Entity entitypr = entity.World.ClassRegistry.CreateEntity(type);
            entitypr.Pos.SetPosWithDimension(entity.Pos.XYZ.Add(dx, dy, dz));
            entitypr.World = entity.World;
            entity.World.SpawnEntity(entitypr);
            creaturesLeftToSpawn--;
        }
    }
}
