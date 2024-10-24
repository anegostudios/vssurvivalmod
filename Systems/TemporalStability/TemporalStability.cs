using Newtonsoft.Json;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.ServerMods;
using Vintagestory.ServerMods.NoObf;

namespace Vintagestory.GameContent
{
    
    public class MobExtraSpawns
    {
        public TempStormMobConfig temporalStormSpawns;
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class TempStormMobConfig
    {
        [JsonProperty]
        public SpawnsByStormStrength spawnsByStormStrength;
        [JsonProperty]
        public RareStormSpawns rareSpawns;

        public class SpawnsByStormStrength
        {
            [JsonProperty]
            public float QuantityMul;
            [JsonProperty]
            public Dictionary<string, AssetLocation[]> variantGroups;

            public Dictionary<string, EntityProperties[]> resolvedVariantGroups;

            [JsonProperty]
            public Dictionary<string, TempStormSpawnPattern> spawnPatterns;
        }

        public class TempStormSpawnPattern
        {
            public float Weight;
            public Dictionary<string, float> GroupWeights;
        }

        public class RareStormSpawns
        {
            public RareStormSpawnsVariant[] Variants;
        }

        public class RareStormSpawnsVariant
        {
            public AssetLocation Code;
            public float ChancePerStorm;

            public EntityProperties ResolvedCode;
        }
    }

    

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
        public string spawnPatternCode="default";
        public bool nowStormActive;       
        public int stormDayNotify = 99;
        public float stormGlitchStrength;
        public double stormActiveTotalDays = 0;
        public double nextStormTotalDays = 5;
        public EnumTempStormStrength nextStormStrength = 0;
        public double nextStormStrDouble;
        public Dictionary<string, int> rareSpawnCount;
    }

    public delegate float GetTemporalStabilityDelegate(float stability, double x, double y, double z);

    public class SystemTemporalStability : ModSystem
    {
        IServerNetworkChannel serverChannel;
        IClientNetworkChannel clientChannel;

        SimplexNoise stabilityNoise;
        ICoreAPI api;
        ICoreServerAPI sapi;

        bool temporalStabilityEnabled;
        bool stormsEnabled;

        Dictionary<string, TemporalStormConfig> configs;
        Dictionary<EnumTempStormStrength, TemporalStormText> texts;

        TemporalStormConfig config;
        TemporalStormRunTimeData data = new TemporalStormRunTimeData();

        TempStormMobConfig mobConfig;

        ModSystemRifts riftSys;

        public float modGlitchStrength;

        public event GetTemporalStabilityDelegate OnGetTemporalStability;

        public HashSet<AssetLocation> stormMobCache = new HashSet<AssetLocation>();

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
            sapi = api;

            api.ChatCommands.Create("nexttempstorm")
                .WithDescription("Tells you the amount of days until the next storm")
                .RequiresPrivilege(Privilege.controlserver)
                .HandleWith(OnCmdNextStorm)
                
                .BeginSubCommand("now")
                    .WithDescription("Start next temporal storm now")
                    .HandleWith(_ =>
                    {
                        data.nextStormTotalDays = api.World.Calendar.TotalDays;
                        return TextCommandResult.Success();
                    })
                .EndSubCommand()
                ;

            serverChannel =
               api.Network.RegisterChannel("temporalstability")
               .RegisterMessageType(typeof(TemporalStormRunTimeData))
            ;

            api.Event.SaveGameLoaded += () =>
            {
                bool prepNextStorm = sapi.WorldManager.SaveGame.IsNew;

                // Init old saves
                if (!sapi.World.Config.HasAttribute("temporalStability"))
                {
                    string playstyle = sapi.WorldManager.SaveGame.PlayStyle;
                    if (playstyle == "surviveandbuild" || playstyle == "wildernesssurvival")
                    {
                        sapi.WorldManager.SaveGame.WorldConfiguration.SetBool("temporalStability", true);
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

        private TextCommandResult OnCmdNextStorm(TextCommandCallingArgs textCommandCallingArgs)
        {
            if (data.nowStormActive)
            {
                var daysleft = data.stormActiveTotalDays - api.World.Calendar.TotalDays;
                return TextCommandResult.Success(Lang.Get(data.nextStormStrength + " Storm still active for {0:0.##} days", daysleft));
            }

            var nextStormDaysLeft = data.nextStormTotalDays - api.World.Calendar.TotalDays;
            return TextCommandResult.Success(Lang.Get("temporalstorm-cmd-daysleft", nextStormDaysLeft));
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
                trySpawnMobs();
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
                float tempstormDurationMul = (float)api.World.Config.GetDecimal("tempstormDurationMul", 1);
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

                    var list = ((CachingConcurrentDictionary<long, Entity>)(api.World as IServerWorldAccessor).LoadedEntities).Values;
                    foreach (var e in list)
                    {
                        if (stormMobCache.Contains(e.Code))
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

                    var list = ((CachingConcurrentDictionary<long,Entity>)(api.World as IServerWorldAccessor).LoadedEntities).Values;
                    foreach (var e in list)
                    {
                        if (stormMobCache.Contains(e.Code))
                        {
                            e.Attributes.RemoveAttribute("ignoreDaylightFlee");

                            if (api.World.Rand.NextDouble() < 0.5)
                            {
                                sapi.World.DespawnEntity(e, new EntityDespawnData() { Reason = EnumDespawnReason.Expire });
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

            var patterns = mobConfig.spawnsByStormStrength.spawnPatterns;
            var patterncodes = patterns.Keys.ToArray().Shuffle(sapi.World.Rand);
            float sumWeight = patterncodes.Sum(code => patterns[code].Weight);
            var rndval = sapi.World.Rand.NextDouble() * sumWeight;
            for (int i = 0; i < patterncodes.Length; i++)
            {
                var patterncode = patterncodes[i];
                var pattern = patterns[patterncode];
                rndval -= pattern.Weight;
                if (rndval <= 0)
                {
                    data.spawnPatternCode = patterncode;
                }
            }

            data.rareSpawnCount = new Dictionary<string, int>();

            foreach (var val in mobConfig.rareSpawns.Variants)
            {
                data.rareSpawnCount[val.Code] = GameMath.RoundRandom(sapi.World.Rand, val.ChancePerStorm);
            }

            rareSpawnsCountByCodeByPlayer.Clear();
        }


        CollisionTester collisionTester = new CollisionTester();
        long spawnBreakUntilMs;
        int nobreakSpawns = 0;

        Dictionary<AssetLocation, Dictionary<string, int>> rareSpawnsCountByCodeByPlayer = new Dictionary<AssetLocation, Dictionary<string, int>>();

        private void trySpawnMobs()
        {
            float str = StormStrength;
            if (str < 0.01f) return;
            if (api.World.Rand.NextDouble() < 0.5 || spawnBreakUntilMs > api.World.ElapsedMilliseconds) return;

            var part = api.ModLoader.GetModSystem<EntityPartitioning>();
            int range = 15;

            nobreakSpawns++;
            if (api.World.Rand.NextDouble() + 0.04f < nobreakSpawns / 100f)
            {
                spawnBreakUntilMs = api.World.ElapsedMilliseconds + 1000 * api.World.Rand.Next(15);
            }

            foreach (var plr in api.World.AllOnlinePlayers)
            {
                if (api.World.Rand.NextDouble() < 0.7) continue;
                trySpawnForPlayer(plr, range, str, part);
            }
        }

        private void trySpawnForPlayer(IPlayer plr, int range, float stormStr, EntityPartitioning part)
        {
            Vec3d spawnPos = new Vec3d();
            BlockPos spawnPosi = new BlockPos();

            var rareSpawns = mobConfig.rareSpawns.Variants;
            var spawnPattern = mobConfig.spawnsByStormStrength.spawnPatterns[data.spawnPatternCode];
            var variantGroups = mobConfig.spawnsByStormStrength.variantGroups;
            var resovariantGroups = mobConfig.spawnsByStormStrength.resolvedVariantGroups;
            Dictionary<AssetLocation, int> rareSpawnCounts = new Dictionary<AssetLocation, int>();
            Dictionary<string, int> mainSpawnCountsByGroup = new Dictionary<string, int>();

            var plrPos = plr.Entity.ServerPos.XYZ;
            part.WalkEntities(plrPos, range + 5, (e) =>
            {
                foreach (var vg in variantGroups)
                {
                    if (vg.Value.Contains(e.Code))
                    {
                        mainSpawnCountsByGroup.TryGetValue(vg.Key, out int cnt);
                        mainSpawnCountsByGroup[vg.Key] = cnt + 1;
                    }
                }

                for (int i = 0; i < rareSpawns.Length; i++)
                {
                    if (rareSpawns[i].Code.Equals(e.Code)) {
                        rareSpawnCounts.TryGetValue(e.Code, out int cnt);
                        rareSpawnCounts[e.Code] = cnt + 1; 
                        break; 
                    }
                }
                return true;
            }, EnumEntitySearchType.Creatures);

            if (!rareSpawnsCountByCodeByPlayer.TryGetValue(plr.PlayerUID, out var plrdict))
            {
                plrdict = rareSpawnsCountByCodeByPlayer[plr.PlayerUID] = new Dictionary<string, int>();
            }

            foreach (var rspc in rareSpawnCounts)
            {
                int prevcnt = 0;
                plrdict.TryGetValue(rspc.Key, out prevcnt);
                rareSpawnCounts.TryGetValue(rspc.Key, out int cnt);
                plrdict[rspc.Key] = cnt + prevcnt;
            }

            foreach (var group in spawnPattern.GroupWeights)
            {
                int allowedCount = (int)Math.Round((2 + stormStr * 8) * group.Value);
                int nowCount = 0;
                mainSpawnCountsByGroup.TryGetValue(group.Key, out nowCount);

                if (nowCount < allowedCount)
                {
                    var variantGroup = resovariantGroups[group.Key];
                    int tries = 15;
                    int spawned = 0;
                    while (tries-- > 0 && spawned < 2)
                    {
                        float typernd = (stormStr * 0.15f + (float)api.World.Rand.NextDouble() * (0.3f + stormStr / 2f)) * variantGroup.Length;
                        int index = GameMath.RoundRandom(api.World.Rand, typernd);
                        var type = variantGroup[GameMath.Clamp(index, 0, variantGroup.Length - 1)];

                        if ((index == 3 || index == 4) && api.World.Rand.NextDouble() < 0.2)
                        {
                            for (int i = 0; i < rareSpawns.Length; i++)
                            {
                                plrdict.TryGetValue(rareSpawns[i].Code, out int cnt);
                                if (cnt == 0)
                                {
                                    type = rareSpawns[i].ResolvedCode;
                                    tries = -1;
                                    break;
                                }
                            }
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

            entity.ServerPos.SetPosWithDimension(spawnPosition);
            entity.ServerPos.SetYaw((float)api.World.Rand.NextDouble() * GameMath.TWOPI);
            entity.Pos.SetFrom(entity.ServerPos);
            entity.PositionBeforeFalling.Set(entity.ServerPos.X, entity.ServerPos.Y, entity.ServerPos.Z);

            entity.Attributes.SetString("origin", "timedistortion");

            api.World.SpawnEntity(entity);

            entity.WatchedAttributes.SetDouble("temporalStability", GameMath.Clamp((1 - 1.5f * StormStrength), 0, 1));
            entity.Attributes.SetBool("ignoreDaylightFlee", true);
            var bh = entity.GetBehavior("timeddespawn");   // Gradually despawn the storm-spawned entities after the storm ends - maximum time 2.4 in-game hours for maximum strength storm
            if (bh is ITimedDespawn bhDespawn)
            {
                bhDespawn.SetDespawnByCalendarDate(data.stormActiveTotalDays + 0.1 * StormStrength * api.World.Rand.NextDouble());
            }
        }


        // Replace standard spawning of mobs with more difficult variants during storms and low stability
        private bool Event_OnTrySpawnEntity(IBlockAccessor blockAccessor, ref EntityProperties properties, Vec3d spawnPosition, long herdId)
        {
            if (mobConfig == null || !stormMobCache.Contains(properties.Code)) return true;

            IPlayer plr = api.World.NearestPlayer(spawnPosition.X, spawnPosition.Y, spawnPosition.Z);
            if (plr == null) return true;

            double stab = plr.Entity.WatchedAttributes.GetDouble("temporalStability", 1);

            stab = Math.Min(stab, 1 - 1f * data.stormGlitchStrength);

            if (stab < 0.25f)
            {
                int index = -1;
                string groupCode = null;
                foreach (var group in mobConfig.spawnsByStormStrength.variantGroups)
                {
                    for (int i = 0; i < group.Value.Length; i++)
                    {
                        if (group.Value[i].Equals(properties.Code))
                        {
                            index = i;
                            groupCode = group.Key;
                            break;
                        }
                    }
                }

                if (index == -1) return true;

                EntityProperties[] resolvedVariantGroups = null;
                // Get Target group
                var spawnPattern = mobConfig.spawnsByStormStrength.spawnPatterns[data.spawnPatternCode];
                float sumWeight = spawnPattern.GroupWeights.Sum(w => w.Value);
                var rndval = sapi.World.Rand.NextDouble() * sumWeight;
                foreach (var w in spawnPattern.GroupWeights)
                {
                    rndval -= w.Value;
                    if (rndval <= 0)
                    {
                        resolvedVariantGroups = mobConfig.spawnsByStormStrength.resolvedVariantGroups[w.Key];
                    }
                }

                int difficultyIncrease = (int)Math.Round((0.25f - stab) * 15);
                int newIndex = Math.Min(index + difficultyIncrease, resolvedVariantGroups.Length - 1);
                properties = resolvedVariantGroups[newIndex];
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

                mobConfig = sapi.Assets.Get("config/mobextraspawns.json").ToObject<MobExtraSpawns>().temporalStormSpawns;
                var rdi = mobConfig.spawnsByStormStrength.resolvedVariantGroups = new Dictionary<string, EntityProperties[]>();

                foreach (var val in mobConfig.spawnsByStormStrength.variantGroups)
                {
                    int i = 0;
                    rdi[val.Key] = new EntityProperties[val.Value.Length];
                    foreach (var code in val.Value)
                    {
                        rdi[val.Key][i++] = sapi.World.GetEntityType(code);
                        stormMobCache.Add(code);
                    }
                }

                foreach (var val in mobConfig.rareSpawns.Variants)
                {
                    val.ResolvedCode = sapi.World.GetEntityType(val.Code);
                    stormMobCache.Add(val.Code);
                }
            }
        }



        internal float GetGlitchEffectExtraStrength()
        {
            if (!data.nowStormActive) return modGlitchStrength;
            return data.stormGlitchStrength + modGlitchStrength;
        }

        private void Event_OnEntityDeath(Entity entity, DamageSource damageSource)
        {
            var damagedBy = damageSource?.GetCauseEntity();
            if (damagedBy == null || !damagedBy.WatchedAttributes.HasAttribute("temporalStability")) return;
            if (entity.Properties.Attributes == null) return;

            float stabrecovery = entity.Properties.Attributes["onDeathStabilityRecovery"].AsFloat(0);
            double ownstab = damagedBy.WatchedAttributes.GetDouble("temporalStability", 1);
            damagedBy.WatchedAttributes.SetDouble("temporalStability", Math.Min(1, ownstab + stabrecovery));
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
            // Moved from EntitySpawner to here. Make mobs spawn at any light level if temporally unstable. A bit of an ugly hack, i know
            int herelightLevel = api.World.BlockAccessor.GetLightLevel((int)spawnPosition.X, (int)spawnPosition.Y, (int)spawnPosition.Z, sc.LightLevelType);

            if (temporalStabilityEnabled && type.Attributes?["spawnCloserDuringLowStability"].AsBool() == true)
            {
                // Below 25% begin reducing range
                double mod = Math.Min(1, 4 * byPlayer.Entity.WatchedAttributes.GetDouble("temporalStability", 1));

                mod = Math.Min(mod, Math.Max(0, 1 - 2 * data.stormGlitchStrength));

                int surfaceY = api.World.BlockAccessor.GetTerrainMapheightAt(spawnPosition.AsBlockPos);
                bool isSurface = spawnPosition.Y >= surfaceY - 5;

                float riftDist = NearestRiftDistance(spawnPosition);

                // Still allow some mobs to spawn during daylight, but therefore must be very close to the rift
                float minl = GameMath.Mix(0, sc.MinLightLevel, (float)mod);
                float maxl = GameMath.Mix(32, sc.MaxLightLevel, (float)mod);
                if (minl > herelightLevel || maxl < herelightLevel)
                {
                    if (!isSurface || riftDist >= 5 || api.World.Rand.NextDouble() > 0.05)
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

            if (sc.MinLightLevel > herelightLevel || sc.MaxLightLevel < herelightLevel) return false;

            return byPlayer.Entity.ServerPos.SquareDistanceTo(spawnPosition) > sc.MinDistanceToPlayer * sc.MinDistanceToPlayer;
        }

        private float NearestRiftDistance(Vec3d pos)
        {
            var nrift = riftSys.riftsById.Values.Nearest(rift => rift.Position.SquareDistanceTo(pos));
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
            
            var stability = GameMath.Clamp(noiseval - extraStr, 0, 1.5f);

            if (OnGetTemporalStability != null)
            {
                stability = OnGetTemporalStability.Invoke(stability, x, y, z);
            }

            return stability;
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
