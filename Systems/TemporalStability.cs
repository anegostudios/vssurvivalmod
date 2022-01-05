using ProtoBuf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent.Mechanics;
using Vintagestory.ServerMods;

namespace Vintagestory.GameContent
{
    public enum EnumTempStormStrength
    {
        Light, Medium, Heavy
    }

    class TemporalStormConfig
    {
        public NatFloat Frequency;
        public float StrengthIncreaseCap;
        public float StrengthIncrease;
    }

    class TemporalStormText
    {
        public string Approaching;
        public string Imminent;
        public string Waning;
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class TemporalStormRunTimeData
    {
        public bool nowStormActive;
        
        public int stormDayNotify = 99;
        public float stormGlitchStrength;
        public double stormActiveTotalDays = 0;


        public double nextStormTotalDays = 5;
        public EnumTempStormStrength nextStormStrength = 0;
        public double nextStormStrDouble;
    }

    public class SystemTemporalStability : ModSystem
    {
        IServerNetworkChannel serverChannel;
        IClientNetworkChannel clientChannel;

        SimplexNoise stabilityNoise;
        ICoreAPI api;
        ICoreServerAPI sapi;

        EntityProperties[] drifterTypes;
        EntityProperties doubleHeadedDrifterType;
        bool temporalStabilityEnabled;
        bool stormsEnabled;

        Dictionary<string, TemporalStormConfig> configs;
        Dictionary<EnumTempStormStrength, TemporalStormText> texts;

        TemporalStormConfig config;
        TemporalStormRunTimeData data = new TemporalStormRunTimeData();

        ModSystemRifts riftSys;

        public float modGlitchStrength;


        string worldConfigStorminess;

        public float StormStrength
        {
            get
            {
                if (data.nowStormActive) return data.stormGlitchStrength;
                return 0;
            }
        }

        public TemporalStormRunTimeData StormData => data;

        public override bool ShouldLoad(EnumAppSide forSide)
        {
            return true;
        }

        public override void Start(ICoreAPI api)
        {
            base.Start(api);
            this.api = api;

            riftSys = api.ModLoader.GetModSystem<ModSystemRifts>();


            texts = new Dictionary<EnumTempStormStrength, TemporalStormText>()
            {
                { EnumTempStormStrength.Light, new TemporalStormText() { Approaching = Lang.Get("A light temporal storm is approaching"), Imminent = Lang.Get("A light temporal storm is imminent"), Waning = Lang.Get("The temporal storm seems to be waning") } },
                { EnumTempStormStrength.Medium, new TemporalStormText() { Approaching = Lang.Get("A medium temporal storm is approaching"), Imminent = Lang.Get("A medium temporal storm is imminent"), Waning = Lang.Get("The temporal storm seems to be waning") } },
                { EnumTempStormStrength.Heavy, new TemporalStormText() { Approaching = Lang.Get("A heavy temporal storm is approaching"), Imminent = Lang.Get("A heavy temporal storm is imminent"), Waning = Lang.Get("The temporal storm seems to be waning") } },
            };

            configs = new Dictionary<string, TemporalStormConfig>()
            {
                {  "veryrare", new TemporalStormConfig() {
                    Frequency = NatFloat.create(EnumDistribution.UNIFORM, 30, 5),
                    StrengthIncrease = 2.5f/100,
                    StrengthIncreaseCap = 25f/100
                } },

                {  "rare", new TemporalStormConfig() {
                    Frequency = NatFloat.create(EnumDistribution.UNIFORM, 25, 5),
                    StrengthIncrease = 5f/100,
                    StrengthIncreaseCap = 50f/100
                } },

                {  "sometimes", new TemporalStormConfig() {
                    Frequency = NatFloat.create(EnumDistribution.UNIFORM, 15, 5),
                    StrengthIncrease = 10f/100,
                    StrengthIncreaseCap = 100f/100
                } },

                {  "often", new TemporalStormConfig() {
                    Frequency = NatFloat.create(EnumDistribution.UNIFORM, 7.5f, 2.5f),
                    StrengthIncrease = 15f/100,
                    StrengthIncreaseCap = 150f/100
                } },

                {  "veryoften", new TemporalStormConfig() {
                    Frequency = NatFloat.create(EnumDistribution.UNIFORM, 4.5f, 1.5f),
                    StrengthIncrease = 20f/100,
                    StrengthIncreaseCap = 200f/100
                } }
            };
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            base.StartClientSide(api);
            api.Event.BlockTexturesLoaded += LoadNoise;


            clientChannel =
                api.Network.RegisterChannel("temporalstability")
                .RegisterMessageType(typeof(TemporalStormRunTimeData))
                .SetMessageHandler<TemporalStormRunTimeData>(onServerData)
            ;
        }

        private void onServerData(TemporalStormRunTimeData data)
        {
            this.data = data;
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);
            this.sapi = api;

            api.RegisterCommand("nexttempstorm", "", "Tells you the amount of days until the next storm", onCmdNextStorm, Privilege.controlserver);

            serverChannel =
               api.Network.RegisterChannel("temporalstability")
               .RegisterMessageType(typeof(TemporalStormRunTimeData))
            ;

            api.Event.SaveGameLoaded += () =>
            {
                bool prepNextStorm = sapi.WorldManager.SaveGame.IsNew;

                // Init old saves
                if (!sapi.World.Config.HasAttribute("temmporalStability"))
                {
                    string playstyle = sapi.WorldManager.SaveGame.PlayStyle;
                    if (playstyle == "surviveandbuild" || playstyle == "wildernesssurvival")
                    {
                        sapi.WorldManager.SaveGame.WorldConfiguration.SetBool("temmporalStability", true);
                    }
                }

                if (!sapi.World.Config.HasAttribute("temporalStorms"))
                {
                    string playstyle = sapi.WorldManager.SaveGame.PlayStyle;
                    if (playstyle == "surviveandbuild" || playstyle == "wildernesssurvival")
                    {
                        sapi.WorldManager.SaveGame.WorldConfiguration.SetString("temporalStorms", playstyle == "surviveandbuild" ? "sometimes" : "often");
                    }
                }

                byte[] bytedata = sapi.WorldManager.SaveGame.GetData("temporalStormData");
                if (bytedata != null)
                {
                    try
                    {
                        data = SerializerUtil.Deserialize<TemporalStormRunTimeData>(bytedata);
                    }
                    catch (Exception)
                    {
                        api.World.Logger.Notification("Failed loading temporal storm data, will initialize new data set");
                        data = new TemporalStormRunTimeData();
                        prepNextStorm = true;
                    }
                } else
                {
                    data = new TemporalStormRunTimeData();
                    prepNextStorm = true;
                }

                LoadNoise();


                if (prepNextStorm)
                {
                    prepareNextStorm();
                }
            };

            api.Event.OnTrySpawnEntity += Event_OnTrySpawnEntity;
            api.Event.GameWorldSave += Event_GameWorldSave;
            api.Event.PlayerJoin += Event_PlayerJoin;
            api.Event.PlayerNowPlaying += Event_PlayerNowPlaying;
            api.Event.RegisterGameTickListener(onTempStormTick, 2000);
        }

        private void onCmdNextStorm(IServerPlayer player, int groupId, CmdArgs args)
        {
            if (data.nowStormActive)
            {
                double daysleft = data.stormActiveTotalDays - api.World.Calendar.TotalDays;
                player.SendMessage(groupId, Lang.Get(data.nextStormStrength + " Storm still active for {0:0.##} days", daysleft), EnumChatType.Notification);
            }
            else
            {
                if (args.PopWord() == "now")
                {
                    data.nextStormTotalDays = api.World.Calendar.TotalDays;
                    return;
                }

                double nextStormDaysLeft = data.nextStormTotalDays - api.World.Calendar.TotalDays;
                player.SendMessage(groupId, Lang.Get("temporalstorm-cmd-daysleft", nextStormDaysLeft), EnumChatType.Notification);
            }
        }

        private void Event_PlayerNowPlaying(IServerPlayer byPlayer)
        {
            if (sapi.WorldManager.SaveGame.IsNew && stormsEnabled)
            {
                double nextStormDaysLeft = data.nextStormTotalDays - api.World.Calendar.TotalDays;
                byPlayer.SendMessage(GlobalConstants.GeneralChatGroup, Lang.Get("{0} days until the first temporal storm.", (int)nextStormDaysLeft), EnumChatType.Notification);
            }
        }

        private void Event_PlayerJoin(IServerPlayer byPlayer)
        {
            byPlayer.Entity.OnCanSpawnNearby = (type, spawnPos, sc) =>
            {
                return CanSpawnNearby(byPlayer, type, spawnPos, sc);
            };

            serverChannel.SendPacket(data, byPlayer);
        }

        private void Event_GameWorldSave()
        {
            sapi.WorldManager.SaveGame.StoreData("temporalStormData", SerializerUtil.Serialize(data));
        }

        private void onTempStormTick(float dt)
        {
            if (config == null) return;
            if (!stormsEnabled)
            {
                data.stormGlitchStrength = 0;
                data.nowStormActive = false;
                return;
            }

            if (data.nowStormActive)
            {
                trySpawnDrifters();
            }

            double nextStormDaysLeft = data.nextStormTotalDays - api.World.Calendar.TotalDays;

            if (nextStormDaysLeft > 0.03 && nextStormDaysLeft < 0.35 && data.stormDayNotify > 1)
            {
                data.stormDayNotify = 1;
                sapi.BroadcastMessageToAllGroups(texts[data.nextStormStrength].Approaching, EnumChatType.Notification);
            }

            if (nextStormDaysLeft <= 0.02 && data.stormDayNotify > 0)
            {
                data.stormDayNotify = 0;
                sapi.BroadcastMessageToAllGroups(texts[data.nextStormStrength].Imminent, EnumChatType.Notification);
            }

            if (nextStormDaysLeft <= 0)
            {
                float tempstormDurationMul = api.World.Config.GetFloat("tempstormDurationMul", 1);
                double stormActiveDays = (0.1f + data.nextStormStrDouble * 0.1f) * tempstormDurationMul;

                // Happens when time is fast forwarded
                if (!data.nowStormActive && nextStormDaysLeft + stormActiveDays < 0)
                {
                    prepareNextStorm();
                    serverChannel.BroadcastPacket(data);
                    return;
                }
                
                if (!data.nowStormActive)
                {
                    data.stormActiveTotalDays = api.World.Calendar.TotalDays + stormActiveDays;
                    data.stormGlitchStrength = 0.53f + (float)api.World.Rand.NextDouble() / 10;
                    if (data.nextStormStrength == EnumTempStormStrength.Medium) data.stormGlitchStrength = 0.67f + (float)api.World.Rand.NextDouble() / 10;
                    if (data.nextStormStrength == EnumTempStormStrength.Heavy) data.stormGlitchStrength = 0.9f + (float)api.World.Rand.NextDouble() / 10;
                    data.nowStormActive = true;

                    serverChannel.BroadcastPacket(data);

                    var list = (api.World as IServerWorldAccessor).LoadedEntities.Values;
                    foreach (var e in list)
                    {
                        if (e.Code.Path.Contains("drifter"))
                        {
                            e.Attributes.SetBool("ignoreDaylightFlee", true);
                        }
                        
                    }

                }

                double activeDaysLeft = data.stormActiveTotalDays - api.World.Calendar.TotalDays;
                if (activeDaysLeft < 0.02 && data.stormDayNotify == 0)
                {
                    data.stormDayNotify = -1;
                    sapi.BroadcastMessageToAllGroups(texts[data.nextStormStrength].Waning, EnumChatType.Notification);
                }

                if (activeDaysLeft < 0)
                {
                    data.stormGlitchStrength = 0;
                    data.nowStormActive = false;
                    data.stormDayNotify = 99;
                    prepareNextStorm();

                    serverChannel.BroadcastPacket(data);

                    var list = (api.World as IServerWorldAccessor).LoadedEntities.Values;
                    foreach (var e in list)
                    {
                        if (e.Code.Path.Contains("drifter"))
                        {
                            e.Attributes.RemoveAttribute("ignoreDaylightFlee");

                            if (api.World.Rand.NextDouble() < 0.5)
                            {
                                sapi.World.DespawnEntity(e, new EntityDespawnReason() { reason = EnumDespawnReason.Expire });
                            }
                        }
                    }
                }
            }
        }

        private void prepareNextStorm()
        {
            if (config == null) return;

            double addStrength = Math.Min(config.StrengthIncreaseCap, config.StrengthIncrease * api.World.Calendar.TotalDays / config.Frequency.avg);

            double frequencyMod = api.World.Config.GetDecimal("tempStormFrequencyMul", 1);

            data.nextStormTotalDays = api.World.Calendar.TotalDays + config.Frequency.nextFloat(1, api.World.Rand) / (1 + addStrength/3) / frequencyMod;

            double stormStrength = addStrength + (api.World.Rand.NextDouble() * api.World.Rand.NextDouble()) * (float)addStrength * 5f;

            int index = (int)Math.Min(2, stormStrength);
            data.nextStormStrength = (EnumTempStormStrength)index;

            data.nextStormStrDouble = Math.Max(0, addStrength);

            doubleHeadedDrifterCountByPlayer.Clear();
        }


        CollisionTester collisionTester = new CollisionTester();
        long spawnBreakUntilMs;
        int nobreakSpawns = 0;

        Dictionary<string, int> doubleHeadedDrifterCountByPlayer = new Dictionary<string, int>();

        private void trySpawnDrifters()
        {
            float str = StormStrength;
            if (str < 0.01f) return;
            if (api.World.Rand.NextDouble() < 0.5 || spawnBreakUntilMs > api.World.ElapsedMilliseconds) return;

            var part = api.ModLoader.GetModSystem<EntityPartitioning>();
            int range = 15;
            Vec3d plrPos;
            Vec3d spawnPos = new Vec3d();
            BlockPos spawnPosi = new BlockPos();

            nobreakSpawns++;
            if (api.World.Rand.NextDouble() + 0.04f < nobreakSpawns / 100f)
            {
                spawnBreakUntilMs = api.World.ElapsedMilliseconds + 1000 * api.World.Rand.Next(15);
            }


            foreach (var val in api.World.AllOnlinePlayers)
            {
                if (api.World.Rand.NextDouble() < 0.75) continue;

                int dHeadedDrifterCount = 0;


                int drifterCount = 0;
                plrPos = val.Entity.ServerPos.XYZ;
                part.WalkEntities(plrPos, range + 5, (e) => { 
                    drifterCount += e.Code.Path.Contains("drifter") ? 1 : 0;
                    dHeadedDrifterCount += e.Code.Path.Contains("drifter-double-headed") ? 1 : 0;
                    return true; 
                });

                doubleHeadedDrifterCountByPlayer.TryGetValue(val.PlayerUID, out int prevcnt);
                doubleHeadedDrifterCountByPlayer[val.PlayerUID] = dHeadedDrifterCount + prevcnt;

                if (drifterCount <= 2 + str * 8)
                {
                    int tries = 15;
                    int spawned = 0;
                    while (tries-- > 0 && spawned < 2)
                    {
                        float typernd = (str * 0.15f + (float)api.World.Rand.NextDouble() * (0.3f + str/2f)) * drifterTypes.Length;
                        int index = GameMath.RoundRandom(api.World.Rand, typernd);
                        var type = drifterTypes[GameMath.Clamp(index, 0, drifterTypes.Length - 1)];

                        if ((index == 3 || index == 4) && api.World.Rand.NextDouble() < 0.2 && dHeadedDrifterCount == 0)
                        {
                            type = doubleHeadedDrifterType;
                        }

                        int rndx = api.World.Rand.Next(2 * range) - range;
                        int rndy = api.World.Rand.Next(2 * range) - range;
                        int rndz = api.World.Rand.Next(2 * range) - range;

                        spawnPos.Set((int)plrPos.X + rndx + 0.5, (int)plrPos.Y + rndy + 0.001, (int)plrPos.Z + rndz + 0.5);
                        spawnPosi.Set((int)spawnPos.X, (int)spawnPos.Y, (int)spawnPos.Z);

                        while (api.World.BlockAccessor.GetBlock(spawnPosi.X, spawnPosi.Y - 1, spawnPosi.Z).Id == 0 && spawnPos.Y > 0)
                        {
                            spawnPosi.Y--;
                            spawnPos.Y--;
                        }

                        if (!api.World.BlockAccessor.IsValidPos((int)spawnPos.X, (int)spawnPos.Y, (int)spawnPos.Z)) continue;
                        Cuboidf collisionBox = type.SpawnCollisionBox.OmniNotDownGrowBy(0.1f);
                        if (collisionTester.IsColliding(api.World.BlockAccessor, collisionBox, spawnPos, false)) continue;

                        DoSpawn(type, spawnPos, 0);
                        spawned++;
                    }
                }
            }
        }

        private void DoSpawn(EntityProperties entityType, Vec3d spawnPosition, long herdid)
        {
            Entity entity = api.ClassRegistry.CreateEntity(entityType);

            EntityAgent agent = entity as EntityAgent;
            if (agent != null) agent.HerdId = herdid;

            entity.ServerPos.SetPos(spawnPosition);
            entity.ServerPos.SetYaw((float)api.World.Rand.NextDouble() * GameMath.TWOPI);
            entity.Pos.SetFrom(entity.ServerPos);
            entity.PositionBeforeFalling.Set(entity.ServerPos.X, entity.ServerPos.Y, entity.ServerPos.Z);

            entity.Attributes.SetString("origin", "timedistortion");

            api.World.SpawnEntity(entity);

            entity.WatchedAttributes.SetDouble("temporalStability", GameMath.Clamp((1 - 1.5f * StormStrength), 0, 1));
            entity.Attributes.SetBool("ignoreDaylightFlee", true);
        }



        private bool Event_OnTrySpawnEntity(ref EntityProperties properties, Vec3d spawnPosition, long herdId)
        {
            if (!properties.Code.Path.StartsWithFast("drifter")) return true;

            IPlayer plr = api.World.NearestPlayer(spawnPosition.X, spawnPosition.Y, spawnPosition.Z);
            if (plr == null) return true;

            double stab = plr.Entity.WatchedAttributes.GetDouble("temporalStability", 1);

            stab = Math.Min(stab, 1 - 1f * data.stormGlitchStrength);

            if (stab < 0.25f)
            {
                int index = -1;
                for (int i = 0; i < drifterTypes.Length; i++)
                {
                    if (drifterTypes[i].Code.Equals(properties.Code))
                    {
                        index = i;
                        break;
                    }
                }

                if (index == -1) return true;

                int hardnessIncrease = (int)Math.Round((0.25f - stab) * 15);

                int newIndex = Math.Min(index + hardnessIncrease, drifterTypes.Length - 1);
                properties = drifterTypes[newIndex];
            }

            return true;
        }

        private void LoadNoise()
        {
            if (api.Side == EnumAppSide.Server) updateOldWorlds();

            temporalStabilityEnabled = api.World.Config.GetBool("temporalStability", true);
            if (!temporalStabilityEnabled) return;


            stabilityNoise = SimplexNoise.FromDefaultOctaves(4, 0.1, 0.9, api.World.Seed);


            if (api.Side == EnumAppSide.Server)
            {
                worldConfigStorminess = api.World.Config.GetString("temporalStorms");

                stormsEnabled = worldConfigStorminess != "off";

                if (worldConfigStorminess != null && configs.ContainsKey(worldConfigStorminess))
                {
                    config = configs[worldConfigStorminess];
                } else 
                {
                    string playstyle = sapi.WorldManager.SaveGame.PlayStyle;
                    if (playstyle == "surviveandbuild" || playstyle == "wildernesssurvival")
                    {
                        config = configs["rare"];
                    } else
                    {
                        config = null;
                    }
                }

                sapi.Event.OnEntityDeath += Event_OnEntityDeath;


                doubleHeadedDrifterType = sapi.World.GetEntityType(new AssetLocation("drifter-double-headed"));
                drifterTypes = new EntityProperties[]
                {
                    sapi.World.GetEntityType(new AssetLocation("drifter-normal")),
                    sapi.World.GetEntityType(new AssetLocation("drifter-deep")),
                    sapi.World.GetEntityType(new AssetLocation("drifter-tainted")),
                    sapi.World.GetEntityType(new AssetLocation("drifter-corrupt")),
                    sapi.World.GetEntityType(new AssetLocation("drifter-nightmare"))
                };
            }
        }



        internal float GetGlitchEffectExtraStrength()
        {
            if (!data.nowStormActive) return modGlitchStrength;
            return data.stormGlitchStrength + modGlitchStrength;
        }

        private void Event_OnEntityDeath(Entity entity, DamageSource damageSource)
        {
            if (damageSource?.SourceEntity == null || !damageSource.SourceEntity.WatchedAttributes.HasAttribute("temporalStability")) return;
            if (entity.Properties.Attributes == null) return;

            float stabrecovery = entity.Properties.Attributes["onDeathStabilityRecovery"].AsFloat(0);
            double ownstab = damageSource.SourceEntity.WatchedAttributes.GetDouble("temporalStability", 1);
            damageSource.SourceEntity.WatchedAttributes.SetDouble("temporalStability", Math.Min(1, ownstab + stabrecovery));
        }

        public float GetTemporalStability(BlockPos pos)
        {
            return GetTemporalStability(pos.X, pos.Y, pos.Z);
        }

        public float GetTemporalStability(Vec3d pos)
        {
            return GetTemporalStability(pos.X, pos.Y, pos.Z);
        }


        public bool CanSpawnNearby(IPlayer byPlayer, EntityProperties type, Vec3d spawnPosition, RuntimeSpawnConditions sc)
        {
            // Moved from EntitySpawner to here. Make drifters spawn at any light level if temporally unstable. A bit of an ugly hack, i know
            int lightLevel = api.World.BlockAccessor.GetLightLevel((int)spawnPosition.X, (int)spawnPosition.Y, (int)spawnPosition.Z, sc.LightLevelType);

            if (api.World.Config.GetBool("temporalStability", true) && type.Attributes?["spawnCloserDuringLowStability"].AsBool() == true)
            {
                // Below 25% begin reducing range
                double mod = Math.Min(1, 4 * byPlayer.Entity.WatchedAttributes.GetDouble("temporalStability", 1));

                mod = Math.Min(mod, Math.Max(0, 1 - 2 * data.stormGlitchStrength));

                int surfaceY = api.World.BlockAccessor.GetTerrainMapheightAt(spawnPosition.AsBlockPos);
                bool isSurface = spawnPosition.Y >= surfaceY - 5;

                float riftDist = NearestRiftDistance(spawnPosition);

                float minl = GameMath.Mix(0, sc.MinLightLevel, (float)mod);
                float maxl = GameMath.Mix(32, sc.MaxLightLevel, (float)mod);
                if (minl > lightLevel || maxl < lightLevel)
                {
                    if (!isSurface || riftDist >= 4 || api.World.Rand.NextDouble() > 0.02)
                    {
                        return false;
                    }
                }

                double sqdist = byPlayer.Entity.ServerPos.SquareDistanceTo(spawnPosition);

                if (isSurface)
                {
                    return riftDist < 24;
                }

                // Force a maximum distance
                if (mod < 0.5)
                {
                    return sqdist < 10 * 10;
                }

                // Force a minimum distance
                return sqdist > sc.MinDistanceToPlayer * sc.MinDistanceToPlayer * mod;
            }

            if (sc.MinLightLevel > lightLevel || sc.MaxLightLevel < lightLevel) return false;

            return byPlayer.Entity.ServerPos.SquareDistanceTo(spawnPosition) > sc.MinDistanceToPlayer * sc.MinDistanceToPlayer;
        }

        private float NearestRiftDistance(Vec3d pos)
        {
            var nrift = riftSys.rifts.Nearest(rift => rift.Position.SquareDistanceTo(pos));
            if (nrift != null)
            {
                return nrift.Position.DistanceTo(pos);
            }

            return 9999;
        }

        public float GetTemporalStability(double x, double y, double z)
        {
            if (!temporalStabilityEnabled) return 2;

            float noiseval = (float)GameMath.Clamp(stabilityNoise.Noise(x/80, y/80, z/80)*1.2f + 0.1f, -1f, 2f);

            float sealLevelDistance = (float)(TerraGenConfig.seaLevel - y);

            // The deeper you go, the more the stability varies. Surface 100% to 80%. Deep below down 100% to 0%
            float surfacenoiseval = GameMath.Clamp(1.6f + noiseval, 0.8f, 1.5f);

            float l = (float)GameMath.Clamp(Math.Pow(Math.Max(0, (float)y) / TerraGenConfig.seaLevel, 2), 0, 1);
            noiseval = GameMath.Mix(noiseval, surfacenoiseval, l);

            // The deeper you go, the lower the stability. Up to -25% stability
            noiseval -= Math.Max(0, sealLevelDistance / api.World.BlockAccessor.MapSizeY) / 3.5f;

            noiseval = GameMath.Clamp(noiseval, 0, 1.5f);

            float extraStr = 1.5f * GetGlitchEffectExtraStrength();
            
            return GameMath.Clamp(noiseval - extraStr, 0, 1.5f);
        }




        private void updateOldWorlds()
        {
            // Pre v1.12 worlds

            if (!api.World.Config.HasAttribute("temporalStorms"))
            {
                if (sapi.WorldManager.SaveGame.PlayStyle == "wildernesssurvival")
                {
                    api.World.Config.SetString("temporalStorms", "often");
                }
                if (sapi.WorldManager.SaveGame.PlayStyle == "surviveandbuild")
                {
                    api.World.Config.SetString("temporalStorms", "rare");
                }
            }

            if (!api.World.Config.HasAttribute("temporalStability"))
            {
                if (sapi.WorldManager.SaveGame.PlayStyle == "wildernesssurvival" || sapi.WorldManager.SaveGame.PlayStyle == "surviveandbuild")
                {
                    api.World.Config.SetBool("temporalStability", true);
                }
            }
        }
    }
}
