using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ProtoBuf;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.ServerMods;
using static Vintagestory.GameContent.MobExtraSpawnsDeva;

namespace Vintagestory.GameContent;

[ProtoContract]
public class DevaLocation
{
    [ProtoMember(1)]
    public BlockPos Pos;

    [ProtoMember(2)]
    public int Radius;
}

[ProtoContract]
public class ErelAnnoyedPacket
{
    [ProtoMember(1)]
    public bool Annoyed;
}

public class MobExtraSpawnsDeva
{
    public DevaAreaMobConfig devastationAreaSpawns;

    public class DevaAreaMobConfig
    {
        public Dictionary<string, float> Quantities;
        public Dictionary<string, AssetLocation[]> VariantGroups;

        public Dictionary<string, EntityProperties[]> ResolvedVariantGroups;
    }
}

public class ModSystemDevastationEffects : ModSystem, IRenderer
{
    public DevaAreaMobConfig mobConfig;
    public Vec3d DevaLocationPresent;
    public Vec3d DevaLocationPast;
    public int EffectRadius;
    private ICoreClientAPI capi;
    private int EffectDist = 5000;

    private static SimpleParticleProperties dustParticles;

    private ICoreServerAPI sapi;
    CollisionTester collisionTester = new CollisionTester();
    AmbientModifier towerAmbientPresent;
    AmbientModifier towerAmbientPast;

    EntityErel entityErel;

    public bool ErelAnnoyed;

    public double RenderOrder => 1;

    public int RenderRange => 1000;

    public override double ExecuteOrder() => 2;

    public override void Start(ICoreAPI api)
    {
        base.Start(api);

        api.Event.OnGetWindSpeed += Event_OnGetWindSpeed;

        api.Network.GetChannel("devastation")
            .RegisterMessageType<ErelAnnoyedPacket>()
        ;
    }

    private void Event_OnGetWindSpeed(Vec3d pos, ref Vec3d windSpeed)
    {
        if (DevaLocationPresent == null) return;
        var dist = DevaLocationPresent.DistanceTo(pos.X, pos.Y, pos.Z);
        if (dist > EffectDist) return;

        windSpeed.Mul(GameMath.Clamp(dist/EffectRadius - 0.5f, 0, 1));
    }

    public override void StartClientSide(ICoreClientAPI api)
    {
        capi = api;
        api.Event.PlayerDimensionChanged += Event_PlayerDimensionChanged;

        dustParticles = new SimpleParticleProperties(1, 3, ColorUtil.ToRgba(40, 220, 220, 220), new Vec3d(), new Vec3d(),
            new Vec3f(-0.25f, -0.25f, -0.25f), new Vec3f(0.25f, 0.25f, 0.25f), 1, 1, 0.3f, 0.3f, EnumParticleModel.Quad);
        dustParticles.MinQuantity = 0.1f;
        dustParticles.MinVelocity.Set(-0.05f, -0.4f, -0.05f);
        dustParticles.AddVelocity.Set(0, 0, 0);
        dustParticles.ParticleModel = EnumParticleModel.Quad;
        dustParticles.GravityEffect = 0;
        dustParticles.MaxSize = 1f;
        dustParticles.AddPos.Set(0, 0, 0);
        dustParticles.MinSize = 0.2f;
        dustParticles.Color = ColorUtil.ColorFromRgba(75 / 4, 100 / 4, 150 / 4, 255);
        dustParticles.addLifeLength = 0;
        dustParticles.WithTerrainCollision = false;
        dustParticles.SelfPropelled = false;
        dustParticles.LifeLength = 4f;
        dustParticles.OpacityEvolve = EvolvingNatFloat.create(EnumTransformFunction.QUADRATIC, -16f);

        // devastation location and radius is send from GenDevastationLayer.cs
        api.Network.GetChannel("devastation")
            .SetMessageHandler<DevaLocation>(OnDevaLocation)
            .SetMessageHandler<ErelAnnoyedPacket>((p) => { this.ErelAnnoyed = p.Annoyed; });

        api.Event.RegisterAsyncParticleSpawner(asyncParticleSpawn);
        api.Event.RegisterRenderer(this, EnumRenderStage.Before, "devastationeffects");
        api.Event.RegisterGameTickListener(clientTick100ms, 100, 123);

        towerAmbientPresent = new AmbientModifier()
        {
            FogColor = new WeightedFloatArray(new float[] { 66/255f, 45 / 255f, 25 / 255f }, 0),
            FogDensity = new WeightedFloat(0.05f, 0)
        }.EnsurePopulated();

        api.Ambient.CurrentModifiers["towerAmbientPresent"] = towerAmbientPresent;

        towerAmbientPast = new AmbientModifier()
        {
            FogColor = AmbientModifier.DefaultAmbient.FogColor,
            FogDensity = new WeightedFloat(0.04f, 0)
        }.EnsurePopulated();

        towerAmbientPast.FogColor.Value[0] *= 0.8f;
        towerAmbientPast.FogColor.Value[1] *= 0.8f;
        towerAmbientPast.FogColor.Value[2] *= 0.8f;

        api.Ambient.CurrentModifiers["towerAmbientPast"] = towerAmbientPast;

        api.ModLoader.GetModSystem<ModSystemAmbientParticles>().ShouldSpawnAmbientParticles += () => devaRangeness > 1;
        api.ModLoader.GetModSystem<WeatherSystemClient>().OnGetBlendedWeatherData += DevastationEffects_OnGetBlendedWeatherData;
        api.Event.OnGetClimate += Event_OnGetClimate; 
        api.Settings.Int.AddWatcher("musicLevel", (level) =>
        {
            baseTrack?.UpdateVolume();
            rustTrack?.UpdateVolume();
            layer2Track?.UpdateVolume();
            bossFightTrack?.UpdateVolume();
        });
    }

    private void Event_OnGetClimate(ref ClimateCondition climate, BlockPos pos, EnumGetClimateMode mode = EnumGetClimateMode.WorldGenValues, double totalDays = 0)
    {
        if (devaRangeness < 1.1)
        {
            climate.Rainfall = Math.Max(0, climate.Rainfall - weatherAttenuate);
            climate.RainCloudOverlay = Math.Max(0, climate.RainCloudOverlay - weatherAttenuate);
        }
    }

    // Client side disable rain and near lighting in the devastation area. It doesn't matter if it still rains server side
    private void DevastationEffects_OnGetBlendedWeatherData(WeatherDataSnapshot obj)
    {
        if (devaRangeness < 1.1)
        {
            obj.nearLightningRate = Math.Max(0, obj.nearLightningRate - weatherAttenuate);
            obj.distantLightningRate = Math.Max(0, obj.distantLightningRate - weatherAttenuate);
            obj.PrecIntensity = Math.Max(0, obj.PrecIntensity - weatherAttenuate);

            float nowspeed = Math.Max(0, obj.curWindSpeed.Length() - weatherAttenuate);
            obj.curWindSpeed.Normalize().Mul(nowspeed);
        }
    }

    float weatherAttenuate;
    float devaRangeness = 0;
    bool layer2InRange;
    bool bossFightInRange;
    bool allLayersLoaded;
    private void clientTick100ms(float obj)
    {
        if (DevaLocationPresent == null) return;
        var plrpos = capi.World.Player.Entity.Pos.XYZ;
        float distsq = plrpos.HorizontalSquareDistanceTo(DevaLocationPresent);

        devaRangeness = distsq / (790 * 790);
        weatherAttenuate = Math.Max(0, 1.1f - devaRangeness) * 1.7f;

        if (devaRangeness < 1)
        {
            LoadMusic();
            StartMusic();
        }

        if (allLayersLoaded && !wasStopped)
        {
            // Layer 2
            if (distsq < 72 * 72 && !layer2InRange)
            {
                layer2Track.Sound.FadeTo(1, fadeInDuration, null);
                layer2InRange = true;
            }
            if (distsq > 76 * 76 && layer2InRange)
            {
                layer2Track.Sound.FadeTo(0, fadeInDuration, (s) => s.SetVolume(0));
                layer2InRange = false;
            }

            bool playBossTrack = !ErelAnnoyed;

            // +Boss fight -Rust layer
            var towerLevel = plrpos.Y - (capi.World.Player.Entity.Pos.Dimension > 0 ? DevaLocationPast.Y : DevaLocationPresent.Y);
            if (distsq < 40 * 40 && towerLevel > 68 && !bossFightInRange && playBossTrack)
            {
                bossFightTrack.Sound.FadeTo(1, fadeInDuration, null);
                rustTrack.Sound.FadeTo(0, fadeInDuration, (s) => s.SetVolume(0));
                bossFightInRange = true;
            }
            if ((distsq > 50 * 50 || towerLevel <= 64 || !playBossTrack) && bossFightInRange)
            {
                bossFightTrack.Sound.FadeTo(0, fadeInDuration, (s) => s.SetVolume(0));
                rustTrack.Sound.FadeTo(1, fadeInDuration, null);
                bossFightInRange = false;
            }
        }

        if (distsq > 810 * 810)
        {
            StopMusic();
        }
    }

    #region Music

    MusicTrack baseTrack;
    MusicTrack rustTrack;
    MusicTrack layer2Track;
    MusicTrack bossFightTrack;
    bool wasStopped = true;
    bool wasStarted;
    float priority = 4.5f;
    float fadeInDuration = 2f;
    AssetLocation baseLayerMloc = new AssetLocation("music/devastation-baselayer.ogg");
    AssetLocation rustlayerMloc = new AssetLocation("music/devastation-bellsandrust.ogg");
    AssetLocation layer2Mloc = new AssetLocation("music/devastation-erelharrass.ogg");
    AssetLocation erelFightMloc = new AssetLocation("music/devastation-erelfight.ogg");

    bool baseLayerLoaded, rustLayerLoaded, kayer2Loaded, erelFightLoaded;

    void LoadMusic()
    {
        if (layer2Track != null)
        {
            return;
        }

        layer2Track = capi.StartTrack(layer2Mloc, 99f, EnumSoundType.MusicGlitchunaffected, (s) => { kayer2Loaded = true; onTrackLoaded(s, layer2Track); });
        layer2Track.Priority = priority;

        bossFightTrack = capi.StartTrack(erelFightMloc, 99f, EnumSoundType.MusicGlitchunaffected, (s) => { erelFightLoaded = true; onTrackLoaded(s, bossFightTrack); });
        bossFightTrack.Priority = priority;

        rustTrack = capi.StartTrack(rustlayerMloc, 99f, EnumSoundType.MusicGlitchunaffected, (s) => { rustLayerLoaded = true; onTrackLoaded(s, rustTrack); });
        rustTrack.Priority = priority;

        wasStopped = false;
    }

    void StopMusic()
    {
        if (capi == null || wasStopped) return;

        baseTrack?.Sound?.FadeTo(0, 4, (s)=>s.Stop());
        rustTrack?.Sound?.FadeTo(0, 4, (s) => s.Stop());
        layer2Track?.Sound?.FadeTo(0, 4, (s) => s.Stop());
        bossFightTrack?.Sound?.FadeTo(0, 4, (s) => s.Stop());
        
        wasStopped = true;
        wasStarted = false;
    }

    private void onTrackLoaded(ILoadedSound sound, MusicTrack track)
    {
        if (track == null)
        {
            sound?.Dispose();
            return;
        }
        if (sound == null) return;

        track.Sound = sound;
        track.Sound.SetLooping(true);
        // Needed so that the music engine does not dispose the sound
        track.ManualDispose = true;

        if (rustLayerLoaded && kayer2Loaded && erelFightLoaded)
        {
            // Load the base layer last because that will enter the field MusicEngine.currentTrack to test against standard music tracks
            baseTrack = capi.StartTrack(baseLayerMloc, 99f, EnumSoundType.MusicGlitchunaffected, (s) => {
                baseLayerLoaded = true;
                baseTrack.Sound = s;
                baseTrack.Sound.SetLooping(true);
                baseTrack.ManualDispose = true;
                StartMusic();
            });
            baseTrack.Priority = priority;
        }
    }

    private void StartMusic()
    {
        if (baseLayerLoaded && rustLayerLoaded && kayer2Loaded && erelFightLoaded && !wasStarted)
        {
            capi.StartTrack(baseTrack, 99, EnumSoundType.MusicGlitchunaffected, false);
            baseTrack.Sound.SetVolume(0);
            rustTrack.Sound.SetVolume(0);
            layer2Track.Sound.SetVolume(0);
            bossFightTrack.Sound.SetVolume(0);

            baseTrack.Sound.Start();
            baseTrack.Sound.FadeIn(fadeInDuration, null);

            rustTrack.Sound.Start();
            rustTrack.Sound.FadeIn(fadeInDuration, null);

            layer2Track.Sound.Start();
            bossFightTrack.Sound.Start();

            wasStopped = false;
            wasStarted = true;
            allLayersLoaded = true;

            capi.Event.RegisterCallback((dt) =>
            {
                baseTrack.loading = false;
                rustTrack.loading = false;
                layer2Track.loading = false;
                bossFightTrack.loading = false;
            }, 500, true);
        }
    }

    #endregion

    private void Event_PlayerDimensionChanged(IPlayer byPlayer)
    {
        updateFogState();
    }

    public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
    {
        updateFogState();
    }

    private void updateFogState()
    {
        if (DevaLocationPresent == null)
        {
            towerAmbientPast.FogDensity.Weight = 0;
            towerAmbientPast.FogColor.Weight = 0;
            towerAmbientPresent.FogDensity.Weight = 0;
            towerAmbientPresent.FogColor.Weight = 0;
            return;
        }


        Vec3d offsetToTowerCenter = DevaLocationPresent - capi.World.Player.Entity.Pos.XYZ;
        var presentDist = offsetToTowerCenter.Length();
        if (presentDist > EffectDist)
        {
            capi.Render.ShaderUniforms.FogSphereQuantity = 0;
            towerAmbientPresent.FogDensity.Weight = 0;
            towerAmbientPresent.FogColor.Weight = 0;
        }
        else
        {
            towerAmbientPresent.FogColor.Weight = (float)GameMath.Clamp((1 - (presentDist - EffectRadius / 2) / EffectRadius) * 2f, 0, 1f);
            towerAmbientPresent.FogDensity.Value = 0.05f;
            towerAmbientPresent.FogDensity.Weight = (float)GameMath.Clamp((1 - (presentDist - EffectRadius / 2) / EffectRadius), 0, 1f);

            // Goes from 1 = at deva tower
            // to 0 = 5000 blocks away
            float f = (float)(1 - presentDist / EffectDist);
            f = GameMath.Clamp(1.5f * (f - 0.25f), 0, 1);

            // However, lets blend that fog sphere to ambient fog after some distance
            var fogColor = capi.Ambient.BlendedFogColor;
            var fogDense = capi.Ambient.BlendedFogDensity;

            float w = towerAmbientPresent.FogColor.Weight;
            float l = GameMath.Clamp(fogDense * 100 + (1 - w) - 1, 0, 1);

            var b = capi.Ambient.BlendedFogBrightness * capi.Ambient.BlendedSceneBrightness;

            capi.Render.ShaderUniforms.FogSphereQuantity = 1;
            capi.Render.ShaderUniforms.FogSpheres[0] = (float)offsetToTowerCenter.X;
            capi.Render.ShaderUniforms.FogSpheres[1] = (float)offsetToTowerCenter.Y - 300;
            capi.Render.ShaderUniforms.FogSpheres[2] = (float)offsetToTowerCenter.Z;
            capi.Render.ShaderUniforms.FogSpheres[3] = EffectRadius * 1.6f;
            capi.Render.ShaderUniforms.FogSpheres[4] = 1 / 800f * f;
            capi.Render.ShaderUniforms.FogSpheres[5] = GameMath.Lerp(66 / 255f * b, fogColor.R, l);
            capi.Render.ShaderUniforms.FogSpheres[6] = GameMath.Lerp(45 / 255f * b, fogColor.G, l);
            capi.Render.ShaderUniforms.FogSpheres[7] = GameMath.Lerp(25 / 255f * b, fogColor.B, l);
        }


        Vec3d offsetToTowerCenterPast = DevaLocationPast - capi.World.Player.Entity.Pos.XYZ;
        var pastDist = offsetToTowerCenterPast.Length();
        if (pastDist > EffectDist)
        {
            towerAmbientPast.FogDensity.Weight = 0;
            towerAmbientPast.FogColor.Weight = 0;
        }
        else
        {
            towerAmbientPast.FogColor.Weight = (float)GameMath.Clamp((1 - (pastDist - EffectRadius / 2) / EffectRadius) * 2f, 0, 1f);
            towerAmbientPast.FogDensity.Value = 0.05f;
            towerAmbientPast.FogDensity.Weight = (float)GameMath.Clamp((1 - (pastDist - EffectRadius / 2) / EffectRadius), 0, 1f);
        }
    }

    public override void StartServerSide(ICoreServerAPI api)
    {
        sapi = api;
        api.Event.SaveGameLoaded += Event_SaveGameLoaded;
        api.Event.GameWorldSave += Event_GameWorldSave;
        api.Event.OnTrySpawnEntity += Event_OnTrySpawnEntity;
        api.Event.PlayerJoin += Event_PlayerJoin;
        api.Event.RegisterGameTickListener(OnGameTickServer, 1000);
    }

    private void Event_PlayerJoin(IServerPlayer byPlayer)
    {
        sapi.Network.GetChannel("devastation").SendPacket(new ErelAnnoyedPacket() { Annoyed = this.ErelAnnoyed }, byPlayer);
    }

    private bool Event_OnTrySpawnEntity(IBlockAccessor blockAccessor, ref EntityProperties properties, Vec3d spawnPosition, long herdId)
    {
        if (DevaLocationPresent == null) return true;

        double distance = spawnPosition.HorizontalSquareDistanceTo(DevaLocationPresent);
        if (distance < 800*800 && !allowedInDevaAreaCodes.Contains(properties.Code)) return false;

        return true;
    }

    HashSet<AssetLocation> allowedInDevaAreaCodes = new HashSet<AssetLocation>();

    private void Event_SaveGameLoaded()
    {
        foreach (var val in sapi.World.EntityTypes)
        {
            if (val.Attributes?.IsTrue("allowInDevastationArea") == true)
            {
                allowedInDevaAreaCodes.Add(val.Code);
            }
        }

        mobConfig = sapi.Assets.Get("config/mobextraspawns.json").ToObject<MobExtraSpawnsDeva>().devastationAreaSpawns;
        var rdi = mobConfig.ResolvedVariantGroups = new Dictionary<string, EntityProperties[]>();

        foreach (var val in mobConfig.VariantGroups)
        {
            int i = 0;
            rdi[val.Key] = new EntityProperties[val.Value.Length];
            foreach (var code in val.Value)
            {
                rdi[val.Key][i++] = sapi.World.GetEntityType(code);
            }
        }
    }

    private void OnGameTickServer(float obj)
    {
        if (DevaLocationPresent == null) return;

        double towerMinDistanceXZ = double.MaxValue;

        foreach (var player in sapi.World.AllOnlinePlayers)
        {
            double distance = player.Entity.ServerPos.DistanceTo(DevaLocationPresent);

            towerMinDistanceXZ = Math.Min(towerMinDistanceXZ, player.Entity.ServerPos.HorDistanceTo(DevaLocationPresent));

            var hasEffect = player.Entity.Stats["gliderLiftMax"].ValuesByKey.TryGetValue("deva", out _);
            if (distance < EffectRadius)
            {
                if (!hasEffect)
                {
                    player.Entity.Stats.Set("gliderSpeedMax", "deva", -0.185f);
                    player.Entity.Stats.Set("gliderLiftMax", "deva", -1.01f);
                }

                if (sapi.World.Rand.NextDouble() < 10.15 && distance > 25)
                {
                    trySpawnMobsForPlayer(player);
                }
            }
            else
            {
                if (hasEffect)
                {
                    player.Entity.Stats.Remove("gliderSpeedMax", "deva");
                    player.Entity.Stats.Remove("gliderLiftMax", "deva");
                }
            }
        }

        if (towerMinDistanceXZ < EffectDist)
        {
            if (entityErel == null || sapi.World.GetEntityById(entityErel.EntityId) == null)
            {
                entityErel = null;
                loadErel();
            }

        } else
        {
            if (entityErel != null)
            {
                saveErel();
                sapi.World.DespawnEntity(entityErel, new EntityDespawnData() { Reason = EnumDespawnReason.OutOfRange });
                entityErel = null;
            }
        }
    }




    private void Event_GameWorldSave()
    {
        saveErel();
    }

    private void saveErel()
    {
        if (entityErel == null) return;

        sapi.Logger.VerboseDebug("Unloading erel");

        using (MemoryStream ms = new MemoryStream())
        {
            BinaryWriter writer = new BinaryWriter(ms);

            try
            {
                writer.Write(sapi.ClassRegistry.GetEntityClassName(entityErel.GetType()));
                entityErel.ToBytes(writer, false);

                sapi.WorldManager.SaveGame.StoreData<byte[]>("erelEntity", ms.ToArray());
            }
            catch (Exception e)
            {
                sapi.Logger.Error("Error thrown trying to serialize erel entity with code {0}, will not save, sorry!", entityErel?.Code);
                sapi.Logger.Error(e);
            }
        }
    }

    private void loadErel()
    {
        sapi.Logger.VerboseDebug("Loading erel");

        var erelBytes = sapi.WorldManager.SaveGame.GetData<byte[]>("erelEntity");
        if (erelBytes != null)
        {
            string className = "unknown";
            try
            {
                using (MemoryStream ms = new MemoryStream(erelBytes))
                {
                    BinaryReader reader = new BinaryReader(ms);
                    className = reader.ReadString();
                    Entity entity = sapi.ClassRegistry.CreateEntity(className);

                    entity.FromBytes(reader, false, sapi.World.RemappedEntities);

                    this.entityErel = (EntityErel)entity;

                    int cs = sapi.World.BlockAccessor.ChunkSize;
                    long chunkindex3d = sapi.WorldManager.ChunkIndex3D((int)entity.ServerPos.X / cs, (int)entity.ServerPos.Y / cs, (int)entity.ServerPos.Z / cs);
                    sapi.World.LoadEntity(entityErel, chunkindex3d);
                }
            }
            catch (Exception e)
            {
                sapi.Logger.Error("Failed loading erel entity (type " + className + "). Will create new one. Exception logged to verbose debug.");
                sapi.Logger.VerboseDebug("Failed loading erel entity. Will create new one. Exception: {0}", LoggerBase.CleanStackTrace(e.ToString()));
            }
        }

        if (entityErel == null)
        {
            sapi.Logger.VerboseDebug("Spawned erel");

            EntityProperties type = sapi.World.GetEntityType(new AssetLocation("erel-corrupted"));
            var entity = sapi.World.ClassRegistry.CreateEntity(type);
            entity.ServerPos.SetPos(DevaLocationPresent);
            entity.ServerPos.Y = TerraGenConfig.seaLevel + 90;
            entity.Pos.SetPos(entity.ServerPos);
            sapi.World.SpawnEntity(entity);
            this.entityErel = (EntityErel)entity;
        }

    }

    private void trySpawnMobsForPlayer(IPlayer player)
    {
        var part = sapi.ModLoader.GetModSystem<EntityPartitioning>();
        Dictionary<string, int> spawnCountsByGroup = new Dictionary<string, int>();
        Vec3d spawnPos = new Vec3d();
        BlockPos spawnPosi = new BlockPos();
        int range = 30;
        var rnd = sapi.World.Rand;

        var plrPos = player.Entity.ServerPos.XYZ;
        part.WalkEntities(plrPos, range + 5, (e) =>
        {
            foreach (var vg in mobConfig.VariantGroups)
            {
                if (vg.Value.Contains(e.Code))
                {
                    spawnCountsByGroup.TryGetValue(vg.Key, out int cnt);
                    spawnCountsByGroup[vg.Key] = cnt + 1;
                    break;
                }
            }
            return true;
        }, EnumEntitySearchType.Creatures);

        var keys = mobConfig.VariantGroups.Keys.ToArray().Shuffle(rnd);

        foreach (var groupcode in keys)
        {
            float allowedCount = mobConfig.Quantities[groupcode];
            int nowCount = 0;
            spawnCountsByGroup.TryGetValue(groupcode, out nowCount);

            if (nowCount < allowedCount)
            {
                var variantGroup = mobConfig.ResolvedVariantGroups[groupcode];
                int tries = 15;
                while (tries-- > 0)
                {
                    double typernd = sapi.World.Rand.NextDouble() * variantGroup.Length;
                    int index = GameMath.RoundRandom(sapi.World.Rand, (float)typernd);
                    var type = variantGroup[GameMath.Clamp(index, 0, variantGroup.Length - 1)];

                    int mindist = 18;
                    if (groupcode == "bowtorn") mindist = 32;

                    int rndx = (mindist + rnd.Next(range - 10)) * (1 - 2 * rnd.Next(2));
                    int rndy = (rnd.Next(range - 10)) * (1 - 2 * rnd.Next(2));
                    int rndz = (mindist + rnd.Next(range - 10)) * (1 - 2 * rnd.Next(2));

                    spawnPos.Set((int)plrPos.X + rndx + 0.5, (int)plrPos.Y + rndy + 0.001, (int)plrPos.Z + rndz + 0.5);
                    spawnPosi.Set((int)spawnPos.X, (int)spawnPos.Y, (int)spawnPos.Z);

                    while (sapi.World.BlockAccessor.GetBlockBelow(spawnPosi).Id == 0 && spawnPos.Y > 0)
                    {
                        spawnPosi.Y--;
                        spawnPos.Y--;
                    }

                    if (!sapi.World.BlockAccessor.IsValidPos(spawnPosi)) continue;
                    Cuboidf collisionBox = type.SpawnCollisionBox.OmniNotDownGrowBy(0.1f);
                    if (collisionTester.IsColliding(sapi.World.BlockAccessor, collisionBox, spawnPos, false)) continue;

                    DoSpawn(type, spawnPos);
                    return;
                }
            }
        }
    }

    private void DoSpawn(EntityProperties entityType, Vec3d spawnPosition)
    {
        Entity entity = sapi.ClassRegistry.CreateEntity(entityType);
        EntityAgent agent = entity as EntityAgent;
        entity.ServerPos.SetPosWithDimension(spawnPosition);
        entity.Pos.SetFrom(entity.ServerPos);
        entity.PositionBeforeFalling.Set(entity.ServerPos.X, entity.ServerPos.Y, entity.ServerPos.Z);

        entity.ServerPos.SetYaw((float)sapi.World.Rand.NextDouble() * GameMath.TWOPI);
        entity.Attributes.SetString("origin", "devastation");
        sapi.World.SpawnEntity(entity);
        entity.Attributes.SetBool("ignoreDaylightFlee", true);
    }

    private bool asyncParticleSpawn(float dt, IAsyncParticleManager manager)
    {
        if (DevaLocationPresent == null) return true;
        var plr = capi.World.Player.Entity;

        float intensity = (float)GameMath.Clamp((1.5 - plr.Pos.DistanceTo(DevaLocationPresent) / EffectRadius)*1.5, 0, 1);
        if (intensity <= 0) return true;

        double offsetx = plr.Pos.Motion.X * 200;
        double offsetz = plr.Pos.Motion.Z * 200;

        for (int dx = -60; dx <= 60; dx++)
        {
            for (int dz = -60; dz <= 60; dz++)
            {
                var pos = plr.Pos.XYZ.Add(dx + offsetx, 0, dz + offsetz);

                float hereintensity = (float)GameMath.Clamp((1 - pos.DistanceTo(DevaLocationPresent) / EffectRadius) * 1.5, 0, 1);
                if (capi.World.Rand.NextDouble() > hereintensity * 0.015) continue;

                pos.Y = capi.World.BlockAccessor.GetRainMapHeightAt((int)pos.X, (int)pos.Z) - 8 + capi.World.Rand.NextDouble() * 25;

                Block block = capi.World.BlockAccessor.GetBlock((int)pos.X, (int)pos.Y, (int)pos.Z);
                if (block.FirstCodePart() != "devastatedsoil") continue;

                Vec3f velocity = DevaLocationPresent.Clone().Sub(pos.X, pos.Y, pos.Z).Normalize().Mul(2f * capi.World.Rand.NextDouble()).ToVec3f() / 2f;
                velocity.Y = 1 + Math.Max(0, 40 - pos.DistanceTo(DevaLocationPresent)) / 20.0f;

                dustParticles.MinPos = pos;
                dustParticles.MinVelocity = velocity;

                manager.Spawn(dustParticles);
            }
        }

        return true;
    }

    private void OnDevaLocation(DevaLocation packet)
    {
        DevaLocationPresent = packet.Pos.ToVec3d();
        EffectRadius = packet.Radius;

        DevaLocationPast = packet.Pos.SetDimension(Dimensions.AltWorld).ToVec3d();
    }

    public void SetErelAnnoyed(bool on)
    {
        if (sapi == null) return;
        sapi.Network.GetChannel("devastation").BroadcastPacket(new ErelAnnoyedPacket() { Annoyed = on });
        this.ErelAnnoyed = on;
    }
}
