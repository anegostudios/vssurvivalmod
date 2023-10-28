using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

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

    public delegate bool PeventSchematicAtDelegate(BlockPos pos, Cuboidi schematicLocation);

    public class GenStructures : ModStdWorldGen
    {
        ICoreServerAPI api;

        int worldheight;
        int regionChunkSize;

        ushort[] heightmap;
        int climateUpLeft;
        int climateUpRight;
        int climateBotLeft;
        int climateBotRight;

        public event PeventSchematicAtDelegate OnPreventSchematicPlaceAt;

        WorldGenStructuresConfig scfg;

        public WorldGenVillageConfig vcfg;

        LCGRandom strucRand; // Deterministic random

        
        IWorldGenBlockAccessor worldgenBlockAccessor;

        WorldGenStructure[] shuffledStructures;

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

        public bool WouldSchematicOverlapAt(BlockPos pos, Cuboidi schematicLocation)
        {
            if (OnPreventSchematicPlaceAt != null)
            {
                var deles = OnPreventSchematicPlaceAt.GetInvocationList();
                foreach (PeventSchematicAtDelegate dele in deles)
                {
                    if (dele(pos, schematicLocation)) return true;
                }
            }

            return false;
        }


        public void initWorldGen()
        {
            chunksize = api.WorldManager.ChunkSize;
            worldheight = api.WorldManager.MapSizeY;
            regionChunkSize = api.WorldManager.RegionSize / chunksize;

            strucRand = new LCGRandom(api.WorldManager.Seed + 1090);

            IAsset asset = api.Assets.Get("worldgen/structures.json");
            scfg = asset.ToObject<WorldGenStructuresConfig>();

            shuffledStructures = new WorldGenStructure[scfg.Structures.Length];

            scfg.Init(api);

            asset = api.Assets.Get("worldgen/villages.json");
            vcfg = asset.ToObject<WorldGenVillageConfig>();
            vcfg.Init(api, scfg.resolvedRocktypeRemapGroups, scfg.SchematicYOffsets);
        }

        private void OnChunkColumnGenPostPass(IChunkColumnGenerateRequest request)
        {
            if (!TerraGenConfig.GenerateStructures) return;

            var chunks = request.Chunks;
            int chunkX = request.ChunkX;
            int chunkZ = request.ChunkZ;

            worldgenBlockAccessor.BeginColumn();

            IMapRegion region = chunks[0].MapChunk.MapRegion;

            DoGenStructures(region, chunkX, chunkZ, true, request.ChunkGenParams);
            TryGenVillages(region, chunkX, chunkZ, true, request.ChunkGenParams);
        }

        private void OnChunkColumnGen(IChunkColumnGenerateRequest request)
        {
            if (!TerraGenConfig.GenerateStructures) return;

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
            
            
            DoGenStructures(region, chunkX, chunkZ, false, request.ChunkGenParams);
            TryGenVillages(region, chunkX, chunkZ, false, request.ChunkGenParams);
        }

        private void DoGenStructures(IMapRegion region, int chunkX, int chunkZ, bool postPass, ITreeAttribute chunkGenParams = null)
        {
            BlockPos pos = new BlockPos();

            ITreeAttribute chanceModTree = null;
            ITreeAttribute maxQuantityModTree = null;
            if (chunkGenParams?["structureChanceModifier"] != null)
            {
                chanceModTree = chunkGenParams["structureChanceModifier"] as TreeAttribute;
            }
            if (chunkGenParams?["structureMaxCount"] != null)
            {
                maxQuantityModTree = chunkGenParams["structureMaxCount"] as TreeAttribute;
            }


            strucRand.InitPositionSeed(chunkX, chunkZ);

            // We need to make a copy each time to preserve determinism
            // which is crucial for the translocator to find an exit point
            for (int i = 0; i < shuffledStructures.Length; i++) shuffledStructures[i] = scfg.Structures[i];

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
                            pos.Set(chunkX * chunksize + dx, ySurface - (int)struc.Depth.nextFloat(1, strucRand), chunkZ * chunksize + dz);
                        }
                        else
                        {
                            pos.Set(chunkX * chunksize + dx, 8 + strucRand.NextInt(Math.Max(1, ySurface - 8 - 5)), chunkZ * chunksize + dz);
                        }
                    }
                    else
                    {
                        pos.Set(chunkX * chunksize + dx, ySurface, chunkZ * chunksize + dz);
                    }

                    if (struc.TryGenerate(worldgenBlockAccessor, api.World, pos, climateUpLeft, climateUpRight, climateBotLeft, climateBotRight))
                    {
                        Cuboidi loc = struc.LastPlacedSchematicLocation;
                        
                        string code = struc.Code + (struc.LastPlacedSchematic == null ? "" : "/" + struc.LastPlacedSchematic.FromFileName);

                        region.GeneratedStructures.Add(new GeneratedStructure() { Code = code, Group = struc.Group, Location = loc.Clone(), SuppressTreesAndShrubs = struc.SuppressTrees, SuppressRivulets = struc.SuppressWaterfalls });
                        region.DirtyForSaving = true;

                        if (struc.BuildProtected)
                        {
                            api.World.Claims.Add(new LandClaim()
                            {
                                Areas = new List<Cuboidi>() { loc.Clone() },
                                Description = struc.BuildProtectionDesc,
                                ProtectionLevel = 10,
                                LastKnownOwnerName = struc.BuildProtectionName,
                                AllowUseEveryone = true
                            });
                        }

                        toGenerate--;
                    }
                }
            }
        }






        public void TryGenVillages(IMapRegion region, int chunkX, int chunkZ, bool postPass, ITreeAttribute chunkGenParams = null)
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

            int dx = strucRand.NextInt(chunksize);
            int dz = strucRand.NextInt(chunksize);
            int ySurface = heightmap[dz * chunksize + dx];
            if (ySurface <= 0 || ySurface >= worldheight - 15) return false;

            pos.Set(chunkX * chunksize + dx, ySurface, chunkZ * chunksize + dz);

            return struc.TryGenerate(blockAccessor, api.World, pos, climateUpLeft, climateUpRight, climateBotLeft, climateBotRight, (loc, schematic) =>
            {
                string code = struc.Code + (schematic == null ? "" : "/" + schematic.FromFileName);

                region.GeneratedStructures.Add(new GeneratedStructure() { Code = code, Group = struc.Group, Location = loc.Clone() });
                region.DirtyForSaving = true;

                if (struc.BuildProtected)
                {
                    api.World.Claims.Add(new LandClaim()
                    {
                        Areas = new List<Cuboidi>() { loc.Clone() },
                        Description = struc.BuildProtectionDesc,
                        ProtectionLevel = 10,
                        LastKnownOwnerName = struc.BuildProtectionName,
                        AllowUseEveryone = true
                    });
                }
            });
        }

    }
}
