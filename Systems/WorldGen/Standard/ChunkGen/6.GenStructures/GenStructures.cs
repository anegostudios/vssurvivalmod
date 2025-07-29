using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

#nullable disable

namespace Vintagestory.ServerMods
{
    public class GenStructuresPosPass : ModStdWorldGen
    {
        public override double ExecuteOrder() { return 0.5; }   // This "PostPass" of GenStructures is done after GenPonds, which has an ExecuteOrder of 0.5; normal (pre-pass) GenStructures has 0.3

        public ChunkColumnGenerationDelegate handler;

        public override void StartServerSide(ICoreServerAPI api)
        {
            if (TerraGenConfig.DoDecorationPass)
            {
                api.Event.ChunkColumnGeneration(handler, EnumWorldGenPass.TerrainFeatures, "standard");
            }
        }
    }

    public delegate bool PeventSchematicAtDelegate(IBlockAccessor blockAccessor, BlockPos pos, Cuboidi schematicLocation, string locationCode);

    public class GenStructures : ModStdWorldGen
    {
        public static bool ReplaceMetaBlocks = true;
        ICoreServerAPI api;

        int worldheight;
        int regionChunkSize;

        ushort[] heightmap;
        int climateUpLeft;
        int climateUpRight;
        int climateBotLeft;
        int climateBotRight;

        public event PeventSchematicAtDelegate OnPreventSchematicPlaceAt;

        internal WorldGenStructuresConfig scfg;

        public WorldGenVillageConfig vcfg;

        LCGRandom strucRand; // Deterministic random


        IWorldGenBlockAccessor worldgenBlockAccessor;

        WorldGenStructure[] shuffledStructures;
        private Dictionary<string, WorldGenStructure[]> StoryStructures;

        public override double ExecuteOrder() { return 0.3; }

        public override void StartServerSide(ICoreServerAPI api)
        {
            this.api = api;
            base.StartServerSide(api);

            if (TerraGenConfig.DoDecorationPass)
            {
                api.Event.InitWorldGenerator(initWorldGen, "standard");
                api.Event.ChunkColumnGeneration(OnChunkColumnGen, EnumWorldGenPass.TerrainFeatures, "standard");
                api.Event.GetWorldgenBlockAccessor(OnWorldGenBlockAccessor);

                api.ModLoader.GetModSystem<GenStructuresPosPass>().handler = OnChunkColumnGenPostPass;
            }
        }

        private void OnWorldGenBlockAccessor(IChunkProviderThread chunkProvider)
        {
            worldgenBlockAccessor = chunkProvider.GetBlockAccessor(false);
        }

        public bool WouldSchematicOverlapAt(IBlockAccessor blockAccessor, BlockPos pos, Cuboidi schematicLocation, string locationCode)
        {
            if (OnPreventSchematicPlaceAt != null)
            {
                var deles = OnPreventSchematicPlaceAt.GetInvocationList();
                foreach (PeventSchematicAtDelegate dele in deles)
                {
                    if (dele(blockAccessor, pos, schematicLocation, locationCode)) return true;
                }
            }

            return false;
        }


        BlockPos spawnPos;

        public void initWorldGen()
        {
            LoadGlobalConfig(api);

            var fillerBlock = api.World.BlockAccessor.GetBlock(new AssetLocation("meta-filler"));
            var pathwayBlock = api.World.BlockAccessor.GetBlock(new AssetLocation("meta-pathway"));
            var undergroundBlock = api.World.BlockAccessor.GetBlock(new AssetLocation("meta-underground"));
            var abovegroundBlock = api.World.BlockAccessor.GetBlock(new AssetLocation("meta-aboveground"));
            BlockSchematic.FillerBlockId = fillerBlock.Id;
            BlockSchematic.PathwayBlockId = pathwayBlock.Id;
            BlockSchematic.UndergroundBlockId = undergroundBlock.Id;
            BlockSchematic.AbovegroundBlockId = abovegroundBlock.Id;

            worldheight = api.WorldManager.MapSizeY;
            regionChunkSize = api.WorldManager.RegionSize / chunksize;

            strucRand = new LCGRandom(api.WorldManager.Seed + 1090);

            LoadStructures();
            LoadVillages();

            var genStoryStructures = api.World.Config.GetAsString("loreContent", "true").ToBool(true);
            if (!genStoryStructures) return;

            var genStorySys = api.ModLoader.GetModSystem<GenStoryStructures>();
            var stcfg = genStorySys.scfg;
            StoryStructures = new Dictionary<string, WorldGenStructure[]>();

            foreach (var storyStructure in stcfg.Structures)
            {
                var path = "worldgen/story/" + storyStructure.Code + "/structures.json";
                var storyLocStructures = api.Assets.GetMany<WorldGenStructuresConfig>(api.Logger, path);
                var structures = new List<WorldGenStructure>();
                foreach (var (_, storyStructuresConfig) in storyLocStructures)
                {
                    storyStructuresConfig.Init(api);
                    structures.AddRange(storyStructuresConfig.Structures);
                }

                if (structures.Count > 0)
                {
                    StoryStructures[storyStructure.Code] = structures.ToArray();
                }
            }


            var df = api.WorldManager.SaveGame.DefaultSpawn;
            if (df != null)
            {
                spawnPos = new BlockPos(df.x, df.y ?? 0, df.z);
            } else
            {
                spawnPos = api.World.BlockAccessor.MapSize.AsBlockPos / 2;
            }
        }

        private void LoadStructures()
        {
            var assets = api.Assets.GetMany<WorldGenStructuresConfig>(api.Logger, "worldgen/structures.json");

            scfg = new WorldGenStructuresConfig();
            scfg.ChanceMultiplier = assets.First(v => v.Key.Domain == "game").Value.ChanceMultiplier;
            scfg.SchematicYOffsets = new Dictionary<string, int>();
            scfg.RocktypeRemapGroups = new Dictionary<string, Dictionary<AssetLocation, AssetLocation>>();
            var structures = new List<WorldGenStructure>();

            foreach (var (_, conf) in assets)
            {
                foreach (var remap in conf.RocktypeRemapGroups)
                {
                    if (scfg.RocktypeRemapGroups.TryGetValue(remap.Key, out var remapGroup))
                    {
                        foreach (var (source, target) in remap.Value)
                        {
                            remapGroup.TryAdd(source, target);
                        }
                    }
                    else
                    {
                        scfg.RocktypeRemapGroups.TryAdd(remap.Key, remap.Value);
                    }
                }
                foreach (var remap in conf.SchematicYOffsets)
                {
                    scfg.SchematicYOffsets.TryAdd(remap.Key, remap.Value);
                }
                structures.AddRange(conf.Structures);
            }

            scfg.Structures = structures.ToArray();

            shuffledStructures = new WorldGenStructure[scfg.Structures.Length];

            scfg.Init(api);
        }

        private void LoadVillages()
        {
            var assets = api.Assets.GetMany<WorldGenVillageConfig>(api.Logger, "worldgen/villages.json");

            vcfg = new WorldGenVillageConfig();
            vcfg.ChanceMultiplier = assets.First(v => v.Key.Domain == "game").Value.ChanceMultiplier;
            var villages = new List<WorldGenVillage>();

            foreach (var (_, conf) in assets)
            {
                villages.AddRange(conf.VillageTypes);
            }

            vcfg.VillageTypes = villages.ToArray();

            vcfg.Init(api, scfg);
        }

        private void OnChunkColumnGenPostPass(IChunkColumnGenerateRequest request)
        {
            if (!TerraGenConfig.GenerateStructures) return;

            var locationCode = GetIntersectingStructure(request.ChunkX * chunksize + chunksize / 2, request.ChunkZ * chunksize + chunksize / 2, SkipStructuresgHashCode);

            var chunks = request.Chunks;
            int chunkX = request.ChunkX;
            int chunkZ = request.ChunkZ;

            worldgenBlockAccessor.BeginColumn();

            IMapRegion region = chunks[0].MapChunk.MapRegion;

            DoGenStructures(region, chunkX, chunkZ, true, locationCode, request.ChunkGenParams);
            TryGenVillages(region, chunkX, chunkZ, true, request.ChunkGenParams);
        }

        private void OnChunkColumnGen(IChunkColumnGenerateRequest request)
        {
            if (!TerraGenConfig.GenerateStructures) return;

            var locationCode = GetIntersectingStructure(request.ChunkX * chunksize + chunksize / 2, request.ChunkZ * chunksize + chunksize / 2, SkipStructuresgHashCode);

            var chunks = request.Chunks;
            int chunkX = request.ChunkX;
            int chunkZ = request.ChunkZ;
            worldgenBlockAccessor.BeginColumn();

            IMapRegion region = chunks[0].MapChunk.MapRegion;
            IntDataMap2D climateMap = region.ClimateMap;
            int rlX = chunkX % regionChunkSize;
            int rlZ = chunkZ % regionChunkSize;


            // A region has 16 chunks
            // Size of the forest map is RegionSize / TerraGenConfig.forestMapScale  => 32*16 / 32  = 16 pixel
            // rlX, rlZ goes from 0..16 pixel
            // facF = 16/16 = 1
            // Get 4 pixels for chunkx, chunkz, chunkx+1 and chunkz+1 inside the map

            float facC = (float)climateMap.InnerSize / regionChunkSize;
            climateUpLeft = climateMap.GetUnpaddedInt((int)(rlX * facC), (int)(rlZ * facC));
            climateUpRight = climateMap.GetUnpaddedInt((int)(rlX * facC + facC), (int)(rlZ * facC));
            climateBotLeft = climateMap.GetUnpaddedInt((int)(rlX * facC), (int)(rlZ * facC + facC));
            climateBotRight = climateMap.GetUnpaddedInt((int)(rlX * facC + facC), (int)(rlZ * facC + facC));

            heightmap = chunks[0].MapChunk.WorldGenTerrainHeightMap;

            DoGenStructures(region, chunkX, chunkZ, false, locationCode, request.ChunkGenParams);
            if (locationCode == null)
            {
                TryGenVillages(region, chunkX, chunkZ, false, request.ChunkGenParams);
            }
        }

        private void DoGenStructures(IMapRegion region, int chunkX, int chunkZ, bool postPass,
            string locationCode, ITreeAttribute chunkGenParams = null)
        {
            // We need to make a copy each time to preserve determinism
            // which is crucial for the translocator to find an exit point
            if (locationCode != null)
            {
                if (StoryStructures.TryGetValue(locationCode, out var storyStructures))
                {
                    shuffledStructures = new WorldGenStructure[storyStructures.Length];
                    for (int i = 0; i < storyStructures.Length; i++) shuffledStructures[i] = storyStructures[i];
                }
                else
                {
                    return;
                }
            }
            else
            {
                shuffledStructures = new WorldGenStructure[scfg.Structures.Length];
                for (int i = 0; i < shuffledStructures.Length; i++) shuffledStructures[i] = scfg.Structures[i];
            }
            BlockPos startPos = new BlockPos();

            ITreeAttribute chanceModTree = null;
            ITreeAttribute maxQuantityModTree = null;
            StoryStructureLocation location = null;
            if (chunkGenParams?["structureChanceModifier"] != null)
            {
                chanceModTree = chunkGenParams["structureChanceModifier"] as TreeAttribute;
            }
            if (chunkGenParams?["structureMaxCount"] != null)
            {
                maxQuantityModTree = chunkGenParams["structureMaxCount"] as TreeAttribute;
            }


            strucRand.InitPositionSeed(chunkX, chunkZ);

            shuffledStructures.Shuffle(strucRand);

            for (int i = 0; i < shuffledStructures.Length; i++)
            {
                WorldGenStructure struc = shuffledStructures[i];
                if (struc.PostPass != postPass) continue;

                float chance = struc.Chance * scfg.ChanceMultiplier;
                int toGenerate = 9999;
                if (chanceModTree != null)
                {
                    chance *= chanceModTree.GetFloat(struc.Code, 0);
                }

                if (maxQuantityModTree != null)
                {
                    toGenerate = maxQuantityModTree.GetInt(struc.Code, 9999);
                }


                while (chance-- > strucRand.NextFloat() && toGenerate > 0)
                {
                    int dx = strucRand.NextInt(chunksize);
                    int dz = strucRand.NextInt(chunksize);
                    int ySurface = heightmap[dz * chunksize + dx];
                    if (ySurface <= 0 || ySurface >= worldheight - 15) continue;

                    if (struc.Placement == EnumStructurePlacement.Underground)
                    {
                        if (struc.Depth != null)
                        {
                            startPos.Set(chunkX * chunksize + dx, ySurface - (int)struc.Depth.nextFloat(1, strucRand), chunkZ * chunksize + dz);
                        }
                        else
                        {
                            startPos.Set(chunkX * chunksize + dx, 8 + strucRand.NextInt(Math.Max(1, ySurface - 8 - 5)), chunkZ * chunksize + dz);
                        }
                    }
                    else
                    {
                        startPos.Set(chunkX * chunksize + dx, ySurface, chunkZ * chunksize + dz);
                    }

                    if (startPos.Y <= 0) continue;

                    if (!BlockSchematicStructure.SatisfiesMinSpawnDistance(struc.MinSpawnDistance, startPos, spawnPos))
                    {
                        continue;
                    }

                    // check if in storylocation and if we can still generate this structure
                    if (locationCode != null)
                    {
                        location = GetIntersectingStructure(chunkX * chunksize + chunksize / 2, chunkZ * chunksize + chunksize / 2);
                        if (location.SchematicsSpawned?.TryGetValue(struc.Group, out var spawnedSchematics) == true && spawnedSchematics >= struc.StoryLocationMaxAmount)
                        {
                            continue;
                        }

                        if (struc.StoryMaxFromCenter != 0 &&  !startPos.InRangeHorizontally(location.CenterPos.X, location.CenterPos.Z, struc.StoryMaxFromCenter))
                        {
                            continue;
                        }
                    }

                    if (struc.TryGenerate(worldgenBlockAccessor, api.World, startPos, climateUpLeft, climateUpRight, climateBotLeft, climateBotRight, locationCode))
                    {
                        if(locationCode != null && location != null)
                        {
                            if (location.SchematicsSpawned?.TryGetValue(struc.Group, out var spawnedSchematics) == true)
                            {
                                location.SchematicsSpawned[struc.Group] = spawnedSchematics + 1;
                            }
                            else
                            {
                                location.SchematicsSpawned ??= new Dictionary<string, int>();
                                location.SchematicsSpawned[struc.Group] = 1;
                            }
                        }
                        Cuboidi loc = struc.LastPlacedSchematicLocation;

                        string code = (struc.LastPlacedSchematic == null ? "" : struc.LastPlacedSchematic.FromFileName) + "/" + struc.Code;

                        region.AddGeneratedStructure(new GeneratedStructure() { Code = code, Group = struc.Group, Location = loc.Clone(), SuppressTreesAndShrubs = struc.SuppressTrees, SuppressRivulets = struc.SuppressWaterfalls });

                        if (struc.BuildProtected)
                        {
                            api.World.Claims.Add(new LandClaim()
                            {
                                Areas = new List<Cuboidi>() { loc.Clone() },
                                Description = struc.BuildProtectionDesc,
                                ProtectionLevel = struc.ProtectionLevel,
                                LastKnownOwnerName = struc.BuildProtectionName,
                                AllowUseEveryone = struc.AllowUseEveryone,
                                AllowTraverseEveryone = struc.AllowTraverseEveryone
                            });
                        }

                        toGenerate--;
                    }
                }
            }
        }






        public void TryGenVillages(IMapRegion region, int chunkX, int chunkZ, bool postPass,
             ITreeAttribute chunkGenParams = null)
        {
            strucRand.InitPositionSeed(chunkX, chunkZ);

            for (int i = 0; i < vcfg.VillageTypes.Length; i++)
            {
                WorldGenVillage struc = vcfg.VillageTypes[i];
                if (struc.PostPass != postPass) continue;
                float chance = struc.Chance * vcfg.ChanceMultiplier;
                while (chance-- > strucRand.NextFloat())
                {
                    GenVillage(worldgenBlockAccessor, region, struc, chunkX, chunkZ);
                }
            }
        }

        public bool GenVillage(IBlockAccessor blockAccessor, IMapRegion region, WorldGenVillage struc, int chunkX, int chunkZ)
        {
            BlockPos pos = new BlockPos();

            int dx = chunksize / 2;
            int dz = chunksize / 2;
            int ySurface = heightmap[dz * chunksize + dx];
            if (ySurface <= 0 || ySurface >= worldheight - 15) return false;

            pos.Set(chunkX * chunksize + dx, ySurface, chunkZ * chunksize + dz);

            return struc.TryGenerate(blockAccessor, api.World, pos, climateUpLeft, climateUpRight, climateBotLeft, climateBotRight, (loc, schematic) =>
            {
                string code = (schematic == null ? "" : schematic.FromFileName) + "/" + struc.Code;

                region.AddGeneratedStructure(new GeneratedStructure() { Code = code, Group = struc.Group, Location = loc.Clone() });

                if (struc.BuildProtected)
                {
                    api.World.Claims.Add(new LandClaim()
                    {
                        Areas = new List<Cuboidi>() { loc.Clone() },
                        Description = struc.BuildProtectionDesc,
                        ProtectionLevel = struc.ProtectionLevel,
                        LastKnownOwnerName = struc.BuildProtectionName,
                        AllowUseEveryone = struc.AllowUseEveryone,
                        AllowTraverseEveryone = struc.AllowTraverseEveryone
                    });
                }
            }, spawnPos);
        }
    }
}
