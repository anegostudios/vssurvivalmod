using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.ServerMods.NoObf;

namespace Vintagestory.ServerMods
{
    public class GenStructuresPosPass : ModStdWorldGen
    {
        public override double ExecuteOrder() { return 0.5; }

        public ChunkColumnGenerationDelegate handler;

        public override void StartServerSide(ICoreServerAPI api)
        {
            if (DoDecorationPass)
            {
                api.Event.ChunkColumnGeneration(handler, EnumWorldGenPass.TerrainFeatures, "standard");
            }
        }
    }

    public class GenStructures : ModStdWorldGen
    {
        ICoreServerAPI api;

        int worldheight;
        int chunkMapSizeY;
        int regionChunkSize;

        ushort[] heightmap;
        int forestUpLeft;
        int forestUpRight;
        int forestBotLeft;
        int forestBotRight;
        int climateUpLeft;
        int climateUpRight;
        int climateBotLeft;
        int climateBotRight;

        WorldGenStructuresConfig scfg;

        WorldGenVillageConfig vcfg;

        LCGRandom strucRand; // Deterministic random

        
        IWorldGenBlockAccessor worldgenBlockAccessor;

        public override double ExecuteOrder() { return 0.3; }

        public override void StartServerSide(ICoreServerAPI api)
        {
            this.api = api;
            base.StartServerSide(api);

            if (DoDecorationPass)
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

       


        internal void initWorldGen()
        {
            chunksize = api.WorldManager.ChunkSize;
            worldheight = api.WorldManager.MapSizeY;
            chunkMapSizeY = api.WorldManager.MapSizeY / chunksize;
            regionChunkSize = api.WorldManager.RegionSize / chunksize;

            strucRand = new LCGRandom(api.WorldManager.Seed + 1090);

            IAsset asset = api.Assets.Get("worldgen/structures.json");
            scfg = asset.ToObject<WorldGenStructuresConfig>();
            scfg.Init(api);

            asset = api.Assets.Get("worldgen/villages.json");
            vcfg = asset.ToObject<WorldGenVillageConfig>();
            vcfg.Init(api);
        }

        private void OnChunkColumnGenPostPass(IServerChunk[] chunks, int chunkX, int chunkZ, ITreeAttribute chunkGenParams = null)
        {
            if (!TerraGenConfig.GenerateStructures) return;

            IMapRegion region = chunks[0].MapChunk.MapRegion;

            DoGenStructures(region, chunkX, chunkZ, true, chunkGenParams);
            DoGenVillages(region, chunkX, chunkZ, true, chunkGenParams);
        }

        private void OnChunkColumnGen(IServerChunk[] chunks, int chunkX, int chunkZ, ITreeAttribute chunkGenParams = null)
        {
            if (!TerraGenConfig.GenerateStructures) return;

            IMapRegion region = chunks[0].MapChunk.MapRegion;

            IntDataMap2D forestMap = region.ForestMap;
            IntDataMap2D climateMap = region.ClimateMap;
            int rlX = chunkX % regionChunkSize;
            int rlZ = chunkZ % regionChunkSize;


            // A region has 16 chunks
            // Size of the forest map is RegionSize / TerraGenConfig.forestMapScale  => 32*16 / 32  = 16 pixel
            // rlX, rlZ goes from 0..16 pixel
            // facF = 16/16 = 1
            // Get 4 pixels for chunkx, chunkz, chunkx+1 and chunkz+1 inside the map
            float facF = (float)forestMap.InnerSize / regionChunkSize;
            forestUpLeft = forestMap.GetUnpaddedInt((int)(rlX * facF), (int)(rlZ * facF));
            forestUpRight = forestMap.GetUnpaddedInt((int)(rlX * facF + facF), (int)(rlZ * facF));
            forestBotLeft = forestMap.GetUnpaddedInt((int)(rlX * facF), (int)(rlZ * facF + facF));
            forestBotRight = forestMap.GetUnpaddedInt((int)(rlX * facF + facF), (int)(rlZ * facF + facF));

            float facC = (float)climateMap.InnerSize / regionChunkSize;
            climateUpLeft = climateMap.GetUnpaddedInt((int)(rlX * facC), (int)(rlZ * facC));
            climateUpRight = climateMap.GetUnpaddedInt((int)(rlX * facC + facC), (int)(rlZ * facC));
            climateBotLeft = climateMap.GetUnpaddedInt((int)(rlX * facC), (int)(rlZ * facC + facC));
            climateBotRight = climateMap.GetUnpaddedInt((int)(rlX * facC + facC), (int)(rlZ * facC + facC));

            heightmap = chunks[0].MapChunk.WorldGenTerrainHeightMap;
            
            
            DoGenStructures(region, chunkX, chunkZ, false, chunkGenParams);
            DoGenVillages(region, chunkX, chunkZ, false, chunkGenParams);
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

            scfg.Structures.Shuffle(strucRand);

            for (int i = 0; i < scfg.Structures.Length; i++)
            {
                WorldGenStructure struc = scfg.Structures[i];
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


                while (chance-- > strucRand.NextDouble() && toGenerate > 0)
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
                            pos.Set(chunkX * chunksize + dx, 8 + strucRand.NextInt(ySurface - 8 - 5), chunkZ * chunksize + dz);
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

                        toGenerate--;
                    }
                }
            }
        }






        private void DoGenVillages(IMapRegion region, int chunkX, int chunkZ, bool postPass, ITreeAttribute chunkGenParams = null)
        {
            BlockPos pos = new BlockPos();

            strucRand.InitPositionSeed(chunkX, chunkZ);

            for (int i = 0; i < vcfg.VillageTypes.Length; i++)
            {
                WorldGenVillage struc = vcfg.VillageTypes[i];
                if (struc.PostPass != postPass) continue;

                float chance = struc.Chance * vcfg.ChanceMultiplier;

                while (chance-- > strucRand.NextDouble())
                {
                    int dx = strucRand.NextInt(chunksize);
                    int dz = strucRand.NextInt(chunksize);
                    int ySurface = heightmap[dz * chunksize + dx];
                    if (ySurface <= 0 || ySurface >= worldheight - 15) continue;

                    pos.Set(chunkX * chunksize + dx, ySurface, chunkZ * chunksize + dz);

                    struc.TryGenerate(worldgenBlockAccessor, api.World, pos, climateUpLeft, climateUpRight, climateBotLeft, climateBotRight, (loc, schematic) =>
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
    }
}
