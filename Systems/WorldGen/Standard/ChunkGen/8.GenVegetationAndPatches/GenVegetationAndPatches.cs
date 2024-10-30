using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.ServerMods.NoObf;

namespace Vintagestory.ServerMods
{

    public class GenVegetationAndPatches : ModStdWorldGen
    {
        ICoreServerAPI sapi;
        LCGRandom rnd;
        IWorldGenBlockAccessor blockAccessor;
        WgenTreeSupplier treeSupplier;
        int worldheight;
        int chunkMapSizeY;
        int regionChunkSize;
        public Dictionary<string, int> RockBlockIdsByType;
        public BlockPatchConfig bpc;
        public Dictionary<string, BlockPatchConfig> StoryStructurePatches;

        float forestMod;
        float shrubMod = 0f;

        public Dictionary<string, MapLayerBase> blockPatchMapGens = new Dictionary<string, MapLayerBase>();

        int noiseSizeDensityMap;
        int regionSize;

        /// <summary>
        /// Vegetation and BlockPatch sub seed
        /// </summary>
        private const int subSeed = 87698;
        public override bool ShouldLoad(EnumAppSide side)
        {
            return side == EnumAppSide.Server;
        }

        public override double ExecuteOrder()
        {
            return 0.5;
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            this.sapi = api;


            if (TerraGenConfig.DoDecorationPass)
            {
                api.Event.InitWorldGenerator(initWorldGen, "standard");
                api.Event.InitWorldGenerator(initWorldGenForSuperflat, "superflat");
                api.Event.ChunkColumnGeneration(OnChunkColumnGen, EnumWorldGenPass.Vegetation, "standard");
                api.Event.MapRegionGeneration(OnMapRegionGen, "standard");
                api.Event.MapRegionGeneration(OnMapRegionGen, "superflat");
                api.Event.GetWorldgenBlockAccessor(OnWorldGenBlockAccessor);
            }
        }

        private void OnMapRegionGen(IMapRegion mapRegion, int regionX, int regionZ, ITreeAttribute chunkGenParams = null)
        {
            int noiseSize = sapi.WorldManager.RegionSize / TerraGenConfig.blockPatchesMapScale;

            foreach (var val in blockPatchMapGens)
            {
                var map = IntDataMap2D.CreateEmpty();

                map.Size = noiseSize + 1;
                map.BottomRightPadding = 1;

                map.Data = val.Value.GenLayer(regionX * noiseSize, regionZ * noiseSize, noiseSize + 1, noiseSize + 1);
                mapRegion.BlockPatchMaps[val.Key] = map;
            }
        }

        private void OnWorldGenBlockAccessor(IChunkProviderThread chunkProvider)
        {
            treeSupplier = new WgenTreeSupplier(sapi);
            blockAccessor = chunkProvider.GetBlockAccessor(true);
        }

        private void initWorldGenForSuperflat()
        {
            treeSupplier.LoadTrees();
        }

        public void initWorldGen()
        {
            regionSize = sapi.WorldManager.RegionSize;
            noiseSizeDensityMap = regionSize / TerraGenConfig.blockPatchesMapScale;

            LoadGlobalConfig(sapi);

            rnd = new LCGRandom(sapi.WorldManager.Seed - subSeed);

            treeSupplier.LoadTrees();

            worldheight = sapi.WorldManager.MapSizeY;
            chunkMapSizeY = sapi.WorldManager.MapSizeY / chunksize;
            regionChunkSize = sapi.WorldManager.RegionSize / chunksize;

            RockBlockIdsByType = new Dictionary<string, int>();
            RockStrataConfig rockstrata = sapi.Assets.Get("worldgen/rockstrata.json").ToObject<RockStrataConfig>();
            for (int i = 0; i < rockstrata.Variants.Length; i++)
            {
                Block block = sapi.World.GetBlock(rockstrata.Variants[i].BlockCode);
                RockBlockIdsByType[block.LastCodePart()] = block.BlockId;
            }
            IAsset asset = sapi.Assets.Get("worldgen/blockpatches.json");
            bpc = asset.ToObject<BlockPatchConfig>();

            var blockpatchesfiles = sapi.Assets.GetMany<BlockPatch[]>(sapi.World.Logger, "worldgen/blockpatches/")
                .OrderBy(b=>b.Key.ToString());
            // order the list so when iterating over it for wgen it is always the same

            var allPatches = new List<BlockPatch>();
            foreach (var patches in blockpatchesfiles)
            {
                allPatches.AddRange(patches.Value);
            }
            bpc.Patches = allPatches.ToArray();

            bpc.ResolveBlockIds(sapi, rockstrata, rnd);
            treeSupplier.treeGenerators.forestFloorSystem.SetBlockPatches(bpc);


            ITreeAttribute worldConfig = sapi.WorldManager.SaveGame.WorldConfiguration;
            forestMod = worldConfig.GetString("globalForestation").ToFloat(0);

            blockPatchMapGens.Clear();
            foreach (var patch in bpc.Patches)
            {
                if (patch.MapCode == null || blockPatchMapGens.ContainsKey(patch.MapCode)) continue;

                int hs = patch.MapCode.GetHashCode();
                int seed = sapi.World.Seed + 112897 + hs;
                blockPatchMapGens[patch.MapCode] = new MapLayerWobbled(seed, 2, 0.9f, TerraGenConfig.forestMapScale, 4000, -2500);
            }

            var genStoryStructures = sapi.World.Config.GetAsString("loreContent", "true").ToBool(true);
            if (!genStoryStructures) return;

            asset = sapi.Assets.Get("worldgen/storystructures.json");
            var stcfg = asset.ToObject<WorldGenStoryStructuresConfig>();
            StoryStructurePatches = new Dictionary<string, BlockPatchConfig>();
            foreach (var storyStructure in stcfg.Structures)
            {
                var path = "worldgen/story/" + storyStructure.Code + "/blockpatches/";

                var storyBlockPatches = sapi.Assets.GetMany<BlockPatch[]>(sapi.World.Logger, path)
                    .OrderBy(b=>b.Key.ToString()).ToList();
                if (storyBlockPatches?.Count > 0)
                {
                    var allLocationPatches = new List<BlockPatch>();
                    foreach (var patch in storyBlockPatches)
                    {
                        allLocationPatches.AddRange(patch.Value);
                    }
                    StoryStructurePatches[storyStructure.Code] = new BlockPatchConfig()
                    {
                        Patches = allLocationPatches.ToArray()
                    };
                    StoryStructurePatches[storyStructure.Code].ResolveBlockIds(sapi, rockstrata, rnd);
                }
            }
        }

        ushort[] heightmap;
        int forestUpLeft;
        int forestUpRight;
        int forestBotLeft;
        int forestBotRight;

        int shrubUpLeft;
        int shrubUpRight;
        int shrubBotLeft;
        int shrubBotRight;

        int climateUpLeft;
        int climateUpRight;
        int climateBotLeft;
        int climateBotRight;

        BlockPos tmpPos = new BlockPos();
        BlockPos chunkBase = new BlockPos();
        BlockPos chunkend = new BlockPos();
        List<Cuboidi> structuresIntersectingChunk = new List<Cuboidi>();

        private void OnChunkColumnGen(IChunkColumnGenerateRequest request)
        {
            var chunks = request.Chunks;
            int chunkX = request.ChunkX;
            int chunkZ = request.ChunkZ;

            blockAccessor.BeginColumn();
            rnd.InitPositionSeed(chunkX, chunkZ);

            IMapChunk mapChunk = chunks[0].MapChunk;

            IntDataMap2D forestMap = mapChunk.MapRegion.ForestMap;
            IntDataMap2D shrubMap = mapChunk.MapRegion.ShrubMap;
            IntDataMap2D climateMap = mapChunk.MapRegion.ClimateMap;
            int rlX = chunkX % regionChunkSize;
            int rlZ = chunkZ % regionChunkSize;

            float facS = (float)shrubMap.InnerSize / regionChunkSize;
            shrubUpLeft = shrubMap.GetUnpaddedInt((int)(rlX * facS), (int)(rlZ * facS));
            shrubUpRight = shrubMap.GetUnpaddedInt((int)(rlX * facS + facS), (int)(rlZ * facS));
            shrubBotLeft = shrubMap.GetUnpaddedInt((int)(rlX * facS), (int)(rlZ * facS + facS));
            shrubBotRight = shrubMap.GetUnpaddedInt((int)(rlX * facS + facS), (int)(rlZ * facS + facS));

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

            heightmap = chunks[0].MapChunk.RainHeightMap;


            structuresIntersectingChunk.Clear();
            sapi.World.BlockAccessor.WalkStructures(chunkBase.Set(chunkX * chunksize, 0, chunkZ * chunksize), chunkend.Set(chunkX * chunksize + chunksize, chunkMapSizeY * chunksize, chunkZ * chunksize + chunksize), (struc) =>
            {
                if (struc.SuppressTreesAndShrubs)
                {
                    structuresIntersectingChunk.Add(struc.Location.Clone().GrowBy(1,1,1));
                }
            });

            if (TerraGenConfig.GenerateVegetation)
            {
                genPatches(chunkX, chunkZ, false);
                genShrubs(chunkX, chunkZ);
                genTrees(chunkX, chunkZ);
                genPatches(chunkX, chunkZ, true);
            }
        }

        void genPatches(int chunkX, int chunkZ, bool postPass)
        {
            int dx, dz, x, z;
            Block liquidBlock;
            int mapsizeY = blockAccessor.MapSizeY;

            var patchIterRandom = new LCGRandom();
            var blockPatchRandom = new LCGRandom();

            // get temp pos to check if we need a story location patch or a regular one
            patchIterRandom.SetWorldSeed(sapi.WorldManager.Seed - subSeed);
            patchIterRandom.InitPositionSeed(chunkX, chunkZ);
            dx = patchIterRandom.NextInt(chunksize);
            dz = patchIterRandom.NextInt(chunksize);
            x = dx + chunkX * chunksize;
            z = dz + chunkZ * chunksize;
            tmpPos.Set(x, 0, z);

            BlockPatch[] bpcPatchesNonTree;
            var isStoryPatch = false;
            var locationCode = GetIntersectingStructure(tmpPos, SkipPatchesgHashCode);
            if (locationCode != null)
            {
                if (StoryStructurePatches.TryGetValue(locationCode, out var blockPatchConfig))
                {
                    bpcPatchesNonTree = blockPatchConfig.PatchesNonTree;
                    isStoryPatch = true;
                }
                else
                {
                    return;
                }
            }
            else
            {
                bpcPatchesNonTree = bpc.PatchesNonTree;
            }

            var mapregion = sapi?.WorldManager.GetMapRegion((chunkX * chunksize) / regionSize, (chunkZ * chunksize) / regionSize);
            for (int i = 0; i < bpcPatchesNonTree.Length; i++)
            {
                var blockPatch = bpcPatchesNonTree[i];
                if (blockPatch.PostPass != postPass) continue;

                patchIterRandom.SetWorldSeed(sapi.WorldManager.Seed - subSeed + i);
                patchIterRandom.InitPositionSeed(chunkX, chunkZ);

                float chance = blockPatch.Chance * bpc.ChanceMultiplier.nextFloat(1f, patchIterRandom);

                while (chance-- > patchIterRandom.NextFloat())
                {
                    dx = patchIterRandom.NextInt(chunksize);
                    dz = patchIterRandom.NextInt(chunksize);
                    x = dx + chunkX * chunksize;
                    z = dz + chunkZ * chunksize;

                    int y = heightmap[dz * chunksize + dx];
                    if (y <= 0 || y >= worldheight - 15) continue;

                    tmpPos.Set(x, y, z);

                    liquidBlock = blockAccessor.GetBlock(tmpPos, BlockLayersAccess.Fluid);

                    // Place according to forest value
                    float forestRel = GameMath.BiLerp(forestUpLeft, forestUpRight, forestBotLeft, forestBotRight, (float)dx / chunksize, (float)dz / chunksize) / 255f;
                    forestRel = GameMath.Clamp(forestRel + forestMod, 0, 1);

                    float shrubRel = GameMath.BiLerp(shrubUpLeft, shrubUpRight, shrubBotLeft, shrubBotRight, (float)dx / chunksize, (float)dz / chunksize) / 255f;
                    shrubRel = GameMath.Clamp(shrubRel + shrubMod, 0, 1);

                    int climate = GameMath.BiLerpRgbColor((float)dx / chunksize, (float)dz / chunksize, climateUpLeft, climateUpRight, climateBotLeft, climateBotRight);

                    if (bpc.IsPatchSuitableAt(blockPatch, liquidBlock, mapsizeY, climate, y, forestRel, shrubRel))
                    {
                        locationCode = GetIntersectingStructure(tmpPos, SkipPatchesgHashCode);
                        if (!isStoryPatch && locationCode != null) continue;

                        if (blockPatch.MapCode != null && patchIterRandom.NextInt(255) > GetPatchDensity(blockPatch.MapCode, x, z, mapregion))
                        {
                            continue;
                        }

                        int firstBlockId = 0;
                        bool found = true;

                        if (blockPatch.BlocksByRockType != null)
                        {
                            found = false;
                            int dy = 1;
                            while (dy < 5 && y - dy > 0)
                            {
                                string lastCodePart = blockAccessor.GetBlock(x, y - dy, z).LastCodePart();
                                if (RockBlockIdsByType.TryGetValue(lastCodePart, out firstBlockId)) { found = true; break; }
                                dy++;
                            }
                        }

                        if (found)
                        {
                            blockPatchRandom.SetWorldSeed(sapi.WorldManager.Seed - subSeed + i);
                            blockPatchRandom.InitPositionSeed(x, z);
                            blockPatch.Generate(blockAccessor, patchIterRandom, x, y, z, firstBlockId, isStoryPatch);
                        }
                    }
                }
            }
        }

        void genShrubs(int chunkX, int chunkZ)
        {
            rnd.InitPositionSeed(chunkX, chunkZ);
            int triesShrubs = (int)treeSupplier.treeGenProps.shrubsPerChunk.nextFloat(1f, rnd);
            int dx, dz, x, z;
            Block block;
            var shrubTryRandom = new LCGRandom();

            while (triesShrubs > 0)
            {
                shrubTryRandom.SetWorldSeed(sapi.World.Seed - subSeed + triesShrubs);
                shrubTryRandom.InitPositionSeed(chunkX, chunkZ);
                triesShrubs--;

                dx = shrubTryRandom.NextInt(chunksize);
                dz = shrubTryRandom.NextInt(chunksize);
                x = dx + chunkX * chunksize;
                z = dz + chunkZ * chunksize;

                int y = heightmap[dz * chunksize + dx];
                if (y <= 0 || y >= worldheight - 15) continue;

                tmpPos.Set(x, y, z);

                block = blockAccessor.GetBlock(tmpPos);
                if (block.Fertility == 0) continue;

                // Place according to forest value
                int climate = GameMath.BiLerpRgbColor((float)dx / chunksize, (float)dz / chunksize, climateUpLeft, climateUpRight, climateBotLeft, climateBotRight);
                float shrubChance = GameMath.BiLerp(shrubUpLeft, shrubUpRight, shrubBotLeft, shrubBotRight, (float)dx / chunksize, (float)dz / chunksize);
                shrubChance = GameMath.Clamp(shrubChance + 255*forestMod, 0, 255);

                if (shrubTryRandom.NextFloat() > (shrubChance / 255f) * (shrubChance / 255f)) continue;
                TreeGenInstance treegenParams = treeSupplier.GetRandomShrubGenForClimate(shrubTryRandom, climate, (int)shrubChance, y);

                if (treegenParams != null)
                {
                    bool canGen = true;
                    for (int i = 0; i < structuresIntersectingChunk.Count; i++)
                    {
                        if (structuresIntersectingChunk[i].Contains(tmpPos)) { canGen = false; break; }
                    }
                    if (!canGen) continue;
                    if (GetIntersectingStructure(tmpPos, SkipShurbsgHashCode) != null) continue;

                    if (blockAccessor.GetBlock(tmpPos.X, tmpPos.Y, tmpPos.Z).Replaceable >= 6000)
                    {
                        tmpPos.Y--;
                    }

                    treegenParams.skipForestFloor = true;

                    treegenParams.GrowTree(blockAccessor, tmpPos, shrubTryRandom);
                }
            }
        }

        void genTrees(int chunkX, int chunkZ)
        {
            rnd.InitPositionSeed(chunkX, chunkZ);
            int climate = GameMath.BiLerpRgbColor((float)0.5f, (float)0.5f, climateUpLeft, climateUpRight, climateBotLeft, climateBotRight);
            float wetrel = Climate.GetRainFall((climate >> 8) & 0xff, heightmap[(chunksize / 2) * chunksize + chunksize/2]) / 255f;
            float temprel = ((climate>>16) & 0xff) / 255f;
            float dryrel = 1 - wetrel;

            float drypenalty = 1 - GameMath.Clamp(2f * (dryrel - 0.5f + 1.5f*Math.Max(temprel - 0.6f, 0)), 0, 0.8f); // Reduce tree generation by up to 70% in low rain places
            float wetboost = 1 + 3 * Math.Max(0, wetrel - 0.75f);

            int triesTrees = (int)(treeSupplier.treeGenProps.treesPerChunk.nextFloat(1f, rnd) * drypenalty * wetboost);
            int dx, dz, x, z;
            Block block;
            int treesGenerated = 0;

            EnumHemisphere hemisphere = sapi.World.Calendar.GetHemisphere(new BlockPos(chunkX * chunksize + chunksize / 2, 0, chunkZ * chunksize + chunksize / 2));

            var treeTryRandom = new LCGRandom();
            while (triesTrees > 0)
            {
                treeTryRandom.SetWorldSeed(sapi.World.Seed - subSeed + triesTrees);
                treeTryRandom.InitPositionSeed(chunkX, chunkZ);
                triesTrees--;

                dx = treeTryRandom.NextInt(chunksize);
                dz = treeTryRandom.NextInt(chunksize);
                x = dx + chunkX * chunksize;
                z = dz + chunkZ * chunksize;

                int y = heightmap[dz * chunksize + dx];
                if (y <= 0 || y >= worldheight - 15) continue;

                bool underwater = false;

                tmpPos.Set(x, y, z);
                block = blockAccessor.GetBlock(tmpPos, BlockLayersAccess.Fluid);

                if (block.IsLiquid()) { underwater = true; tmpPos.Y--; block = blockAccessor.GetBlock(tmpPos, BlockLayersAccess.Fluid); if (block.IsLiquid()) tmpPos.Y--; }

                block = blockAccessor.GetBlock(tmpPos);
                if (block.Fertility == 0) continue;

                // Place according to forest value
                float treeDensity = GameMath.BiLerp(forestUpLeft, forestUpRight, forestBotLeft, forestBotRight, (float)dx / chunksize, (float)dz / chunksize);
                climate = GameMath.BiLerpRgbColor((float)dx / chunksize, (float)dz / chunksize, climateUpLeft, climateUpRight, climateBotLeft, climateBotRight);

                treeDensity = GameMath.Clamp(treeDensity + forestMod*255, 0, 255);

                float treeDensityNormalized = treeDensity / 255f;



                // 1 in 400 chance to always spawn a tree
                // otherwise go by tree density using a quadratic drop off to create clearer forest edges
                if (treeTryRandom.NextFloat() > Math.Max(0.0025f, treeDensityNormalized * treeDensityNormalized) || forestMod <= -1) continue;
                TreeGenInstance treegenParams = treeSupplier.GetRandomTreeGenForClimate(treeTryRandom, climate, (int)treeDensity, y, underwater);

                if (treegenParams != null)
                {
                    bool canGen = true;
                    for (int i = 0; i < structuresIntersectingChunk.Count; i++)
                    {
                        if (structuresIntersectingChunk[i].Contains(tmpPos)) { canGen = false; break; }
                    }
                    if (!canGen) continue;
                    if (GetIntersectingStructure(tmpPos, SkipTreesgHashCode) != null) continue;

                    if (blockAccessor.GetBlock(tmpPos.X, tmpPos.Y, tmpPos.Z).Replaceable >= 6000)
                    {
                        tmpPos.Y--;
                    }

                    treegenParams.skipForestFloor = false;
                    treegenParams.hemisphere = hemisphere;
                    treegenParams.treesInChunkGenerated = treesGenerated;

                    treegenParams.GrowTree(blockAccessor, tmpPos, treeTryRandom);

                    treesGenerated++;
                }
            }
        }

        /// <summary>
        /// Returns 0..255
        /// </summary>
        /// <param name="code"></param>
        /// <param name="posX"></param>
        /// <param name="posZ"></param>
        /// <param name="mapregion"></param>
        /// <returns></returns>
        public int GetPatchDensity(string code, int posX, int posZ, IMapRegion mapregion)
        {
            if (mapregion == null) return 0;
            int lx = posX % regionSize;
            int lz = posZ % regionSize;

            IntDataMap2D map;
            mapregion.BlockPatchMaps.TryGetValue(code, out map);
            if (map != null)
            {
                float posXInRegionOre = GameMath.Clamp((float)lx / regionSize * noiseSizeDensityMap, 0, noiseSizeDensityMap - 1);
                float posZInRegionOre = GameMath.Clamp((float)lz / regionSize * noiseSizeDensityMap, 0, noiseSizeDensityMap - 1);

                int density = map.GetUnpaddedColorLerped(posXInRegionOre, posZInRegionOre);

                return density;
            }

            return 0;
        }
    }
}
