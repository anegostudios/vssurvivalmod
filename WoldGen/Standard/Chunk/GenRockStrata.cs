using System;
using System.Collections.Generic;
using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.ServerMods.NoObf;

namespace Vintagestory.ServerMods
{
    public class GenRockStrata : ModStdWorldGen
    {
        NormalizedPerlinNoise noiseGen;
        RockstrataWorldProperty rockstrata;
        GeologicProvinces provinces;

        NormalizedPerlinNoise distort2dX;
        NormalizedPerlinNoise distort2dZ;

        ICoreServerAPI api;
        int regionSize;
        int regionNoiseSize;
        int regionChunkSize;
        int regionMapSize;
        float chunkRatio;

        // Temporary storage to prevent allocating them hundreds or thousands of times
        ushort[][] layerBlockIds;
        List<ushort> finalBlockIds;
        RockStrataAtPos[] interpolated = new RockStrataAtPos[4];
        List<WeightedIndex> tempWeightedIndices = new List<WeightedIndex>(4);
        int[] indices = new int[4];
        //WeightedIndex[] weightedIndicesTopBottom;


        Dictionary<int, LerpedWeightedIndex2DMap> ProvinceMapByRegion = new Dictionary<int, LerpedWeightedIndex2DMap>(10);

        ushort rockBlockId;
        int worldHeight;

        Random rand;

        public override bool ShouldLoad(EnumAppSide side)
        {
            return side == EnumAppSide.Server;
        }

        public override double ExecuteOrder()
        {
            return 0.1;
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            IAsset asset = api.Assets.Get("worldgen/terrain/standard/rockstrata.json");
            rockstrata = asset.ToObject<RockstrataWorldProperty>();

            asset = api.Assets.Get("worldgen/terrain/standard/geologicprovinces.json");
            provinces = asset.ToObject<GeologicProvinces>();

            this.api = api;


            // Call our loaded method manually if the server is already running (happens when mods are reloaded at runtime)
            if (api.Server.CurrentRunPhase == EnumServerRunPhase.RunGame)
            {
                OnGameWorldLoaded();
            }

            api.Event.ChunkColumnGeneration(GenChunkColumn, EnumWorldGenPass.Terrain);
            api.Event.SaveGameLoaded(OnGameWorldLoaded);
        }


        public void OnGameWorldLoaded()
        {
            LoadGlobalConfig(api);

            chunksize = api.WorldManager.ChunkSize;
            regionSize = api.WorldManager.RegionSize;
            chunkRatio = (float)chunksize / regionSize;

            // Unpadded region noise size in blocks
            regionNoiseSize = regionSize / TerraGenConfig.geoProvMapScale;

            // Unpadded region noise size in chunks
            regionChunkSize = regionSize / chunksize;

            // Amount of regions in all of the map (actually the square of that - regionMapSize*regionMapSize)
            regionMapSize = api.WorldManager.MapSizeX / (chunksize * regionNoiseSize);


            rand = new Random(api.WorldManager.Seed);

            noiseGen = NormalizedPerlinNoise.FromDefaultOctaves(1, 0.0005, 1, api.WorldManager.Seed);
            distort2dX = new NormalizedPerlinNoise(new double[] { 0.9, 0.5 }, new double[] { 0.01/3, 0.05/3 }, api.WorldManager.Seed + 7123);
            distort2dZ = new NormalizedPerlinNoise(new double[] { 0.9, 0.5 }, new double[] { 0.01/3, 0.05/3 }, api.WorldManager.Seed + 7123);

            Random rnd = new Random(api.WorldManager.Seed);
            Dictionary<EnumRockGroup, List<RockStrataVariant>> variantsByRockGroup = new Dictionary<EnumRockGroup, List<RockStrataVariant>>();
            var values = Enum.GetValues(typeof(EnumRockGroup));

            foreach (EnumRockGroup rockgroup in values)
            {
                variantsByRockGroup[rockgroup] = new List<RockStrataVariant>();
            }
            foreach (RockStrataVariant variant in rockstrata.Variants)
            {
                variantsByRockGroup[variant.RockGroup].Add(variant);
            }

            foreach (GeologicProvinceVariant variant in provinces.Variants)
            {
                variant.InitRockStrataGen(api, rnd, variantsByRockGroup);
            }

            rockBlockId = (ushort)api.WorldManager.GetBlockId(new AssetLocation("rock-granite"));
            worldHeight = api.WorldManager.MapSizeY;

            layerBlockIds = new ushort[][]
            {
                new ushort[worldHeight],
                new ushort[worldHeight],
                new ushort[worldHeight],
                new ushort[worldHeight]
            };

            finalBlockIds = new List<ushort>(api.WorldManager.MapSizeY);
        }



        internal void GenChunkColumn(IServerChunk[] chunks, int chunkX, int chunkZ)
        {
            ushort[] heightMap = chunks[0].MapChunk.RainHeightMap;

            float lerpMapInv = 1f / TerraGenConfig.geoProvMapScale;

            float chunkInRegionX = (chunkX % regionChunkSize) * lerpMapInv * chunksize;
            float chunkInRegionZ = (chunkZ % regionChunkSize) * lerpMapInv * chunksize;

            LerpedWeightedIndex2DMap map = GetOrLoadLerpedProvinceMap(chunks[0].MapChunk, chunkX, chunkZ);


            int localIndex3D;

            float chunkSizeFloat = chunksize;

            int absX, absZ;

            for (int x = 0; x < chunksize; x++)
            {
                for (int z = 0; z < chunksize; z++)
                {
                    int surfaceY = heightMap[z * chunksize + x];

                    absX = chunkX * chunksize + x;
                    absZ = chunkZ * chunksize + z;

                    // Todo: lerp these 3 noise gens for performance
                    double distortX = (float)distort2dX.Noise(absX, absZ) * 160f;
                    double distortZ = (float)distort2dZ.Noise(absX, absZ) * 160f;
                    double noiseValue = noiseGen.Noise(
                        absX + distortX, 
                        absZ + distortZ
                    );

                    WeightedIndex[] indices = map[
                        TerraGenConfig.geoProvMapPadding + chunkInRegionX + x * lerpMapInv, 
                        TerraGenConfig.geoProvMapPadding + chunkInRegionZ + z * lerpMapInv
                    ];

                    LoadInterpolatedLayerBlockIds(noiseValue, indices, surfaceY);

                    int i = 0;

                    while (surfaceY >= 0)
                    {
                        int chunkY = surfaceY / chunksize;
                        int lY = Math.Min(chunksize - 1, surfaceY - chunkY * chunksize);

                        localIndex3D = (chunksize * lY + z) * chunksize + x;

                        if (chunks[chunkY].Blocks[localIndex3D] == rockBlockId && finalBlockIds.Count > i)
                        {
                            chunks[chunkY].Blocks[localIndex3D] = finalBlockIds[i++];
                        }

                        surfaceY--;
                    }
                }
            }
        }


        LerpedWeightedIndex2DMap GetOrLoadLerpedProvinceMap(IMapChunk mapchunk, int chunkX, int chunkZ)
        {
            int index2d = chunkZ / regionChunkSize * regionMapSize + chunkX / regionChunkSize;

            LerpedWeightedIndex2DMap map;
            ProvinceMapByRegion.TryGetValue(index2d, out map);
            if (map != null) return map;

            return CreateLerpedProvinceMap(mapchunk.MapRegion.GeologicProvinceMap, chunkX / regionChunkSize, chunkZ / regionChunkSize);
        }

        LerpedWeightedIndex2DMap CreateLerpedProvinceMap(IntMap geoMap, int regionX, int regionZ)
        {
            int index2d = regionZ * regionMapSize + regionX;

            return ProvinceMapByRegion[index2d] = new LerpedWeightedIndex2DMap(geoMap.Data, geoMap.Size, TerraGenConfig.geoProvSmoothingRadius);
        }



        private void LoadInterpolatedLayerBlockIds(double noiseValue, WeightedIndex[] indices, int surfaceY)
        {
            finalBlockIds.Clear();
            for (int i = 0; i < indices.Length; i++)
            {
                provinces.Variants[indices[i].index].LoadRockStratas(noiseValue, finalBlockIds, surfaceY, indices[i].weight);
            }
        }

        
    }
}
