using System;
using System.Collections.Generic;
using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.ServerMods.NoObf;

namespace Vintagestory.ServerMods
{
    public class GenRockStrataNew : ModStdWorldGen
    {
        ICoreServerAPI api;
        int regionSize;
        int regionChunkSize;
        float chunkRatio;

        ushort rockBlockId;
        int worldHeight;
        Random rand;

        internal RockStrataConfig strata;

        SimplexNoise distort2dx;
        SimplexNoise distort2dz;
        internal MapLayerCustomPerlin[] strataNoises;

        int regionMapSize;
        Dictionary<int, LerpedWeightedIndex2DMap> ProvinceMapByRegion = new Dictionary<int, LerpedWeightedIndex2DMap>(10);

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
            this.api = api;

            api.Event.InitWorldGenerator(initWorldGen, "standard");
            api.Event.ChunkColumnGeneration(GenChunkColumn, EnumWorldGenPass.Terrain, "standard");

            api.Event.MapRegionGeneration(OnMapRegionGen, "standard");
        }


        public void initWorldGen()
        {
            initWorldGen(0);
        }

        public void initWorldGen(int seedDiff)
        {
            IAsset asset = api.Assets.Get("worldgen/rockstrata.json");
            strata = asset.ToObject<RockStrataConfig>();

            for (int i = 0; i < strata.Variants.Length; i++)
            {
                strata.Variants[i].Init(api.World);
            }

            LoadGlobalConfig(api);

            chunksize = api.WorldManager.ChunkSize;
            regionSize = api.WorldManager.RegionSize;
            chunkRatio = (float)chunksize / regionSize;

            // Unpadded region noise size in chunks
            regionChunkSize = regionSize / chunksize;

            rand = new Random(api.WorldManager.Seed + seedDiff);

            // Unpadded region noise size in blocks
            int geoProvRegionNoiseSize = regionSize / TerraGenConfig.geoProvMapScale;

            // Amount of regions in all of the map
            regionMapSize = api.WorldManager.MapSizeX / (chunksize * geoProvRegionNoiseSize);

            rockBlockId = (ushort)api.WorldManager.GetBlockId(new AssetLocation("rock-granite"));
            worldHeight = api.WorldManager.MapSizeY;
            
            distort2dx = new SimplexNoise(new double[] { 14, 9, 6, 3 }, new double[] { 1 / 100.0, 1 / 50.0, 1 / 25.0, 1 / 12.5 }, api.World.SeaLevel + 9876 + seedDiff);
            distort2dz = new SimplexNoise(new double[] { 14, 9, 6, 3 }, new double[] { 1 / 100.0, 1 / 50.0, 1 / 25.0, 1 / 12.5 }, api.World.SeaLevel + 9877 + seedDiff);

            strataNoises = new MapLayerCustomPerlin[strata.Variants.Length];
            for (int i = 0; i < strataNoises.Length; i++)
            {
                RockStratum stratum = strata.Variants[i];

                double[] ampls = (double[])stratum.Amplitudes.Clone();
                double[] freq = (double[])stratum.Frequencies.Clone();
                double[] th = (double[])stratum.Thresholds.Clone();

                if (ampls.Length != freq.Length || ampls.Length != th.Length)
                {
                    throw new ArgumentException(string.Format("Bug in Rock stratum {0}: The list of amplitudes ({1} elements), frequencies ({2} elements) and thresholds ({3} elements) are not of the same length!", i, ampls.Length, freq.Length, th.Length));
                }

                for (int j = 0; j < freq.Length; j++)
                {
                    freq[j] /= TerraGenConfig.rockStrataOctaveScale;
                    ampls[j] *= api.WorldManager.MapSizeY;
                    th[j] *= api.WorldManager.MapSizeY;
                }

                strataNoises[i] = new MapLayerCustomPerlin(api.World.Seed + 23423 + i + seedDiff, ampls, freq, th);
            }
        }



        private void OnMapRegionGen(IMapRegion mapRegion, int regionX, int regionZ)
        {
            int noiseSize = api.WorldManager.RegionSize / TerraGenConfig.rockStrataScale;
            int pad = 2;
            
            mapRegion.RockStrata = new IntMap[strata.Variants.Length];
            for (int i = 0; i < strata.Variants.Length; i++)
            {
                IntMap intmap = new IntMap();
                mapRegion.RockStrata[i] = intmap;
                intmap.Data = strataNoises[i].GenLayer(
                    regionX * noiseSize - pad,
                    regionZ * noiseSize - pad,
                    noiseSize + 2 * pad,
                    noiseSize + 2 * pad
                );

                intmap.Size = noiseSize + 2 * pad;
                intmap.TopLeftPadding = intmap.BottomRightPadding = pad;
            }
        }


        float[] rockGroupMaxThickness = new float[4];
        float[] rockGroupMaxQuantity = new float[4];
        int[] rockGroupCounter = new int[4];

        internal void GenChunkColumn(IServerChunk[] chunks, int chunkX, int chunkZ)
        {
            IMapChunk mapChunk = chunks[0].MapChunk;
            ushort[] heightMap = mapChunk.WorldGenTerrainHeightMap;
            int rdx = chunkX % regionChunkSize;
            int rdz = chunkZ % regionChunkSize;
            
            int rockStrataId;
            RockStratum stratum = null;
            IntMap rockMap;
            float step = 0;
            float strataThickness = 0;

            LerpedWeightedIndex2DMap map = GetOrLoadLerpedProvinceMap(chunks[0].MapChunk, chunkX, chunkZ);
            float lerpMapInv = 1f / TerraGenConfig.geoProvMapScale;
            float chunkInRegionX = (chunkX % regionChunkSize) * lerpMapInv * chunksize;
            float chunkInRegionZ = (chunkZ % regionChunkSize) * lerpMapInv * chunksize;

            GeologicProvinces provinces = NoiseGeoProvince.provinces;
            

            for (int x = 0; x < chunksize; x++)
            {
                for (int z = 0; z < chunksize; z++)
                {
                    int surfaceY = heightMap[z * chunksize + x];
                    int ylower = 1;
                    int yupper = surfaceY;
                    strataThickness = 0;

                    WeightedIndex[] indices = map[
                        chunkInRegionX + x * lerpMapInv,
                        chunkInRegionZ + z * lerpMapInv
                    ];
                    rockGroupMaxThickness[0] = rockGroupMaxThickness[1] = rockGroupMaxThickness[2] = rockGroupMaxThickness[3] = 0;
                    rockGroupMaxQuantity[0] = rockGroupMaxQuantity[1] = rockGroupMaxQuantity[2] = rockGroupMaxQuantity[3] = 0;
                    rockGroupCounter[0] = rockGroupCounter[1] = rockGroupCounter[2] = rockGroupCounter[3] = 0;

                    for (int i = 0; i < indices.Length; i++)
                    {
                        float w = indices[i].Weight;

                        GeologicProvinceVariant var = provinces.Variants[indices[i].Index];

                        rockGroupMaxThickness[0] += var.RockStrataIndexed[0].MaxThickness * w;
                        rockGroupMaxThickness[1] += var.RockStrataIndexed[1].MaxThickness * w;
                        rockGroupMaxThickness[2] += var.RockStrataIndexed[2].MaxThickness * w;
                        rockGroupMaxThickness[3] += var.RockStrataIndexed[3].MaxThickness * w;

                        rockGroupMaxQuantity[0] += var.RockStrataIndexed[0].MaxQuantity * w;
                        rockGroupMaxQuantity[1] += var.RockStrataIndexed[1].MaxQuantity * w;
                        rockGroupMaxQuantity[2] += var.RockStrataIndexed[2].MaxQuantity * w;
                        rockGroupMaxQuantity[3] += var.RockStrataIndexed[3].MaxQuantity * w;
                    }

                    

                    float distx = (float)distort2dx.Noise(chunkX * chunksize + x, chunkZ * chunksize + z);
                    float distz = (float)distort2dz.Noise(chunkX * chunksize + x, chunkZ * chunksize + z);
                    

                    rockStrataId = -1;

                    while (ylower <= yupper)
                    {
                        if (--strataThickness <= 0)
                        {
                            rockStrataId++;
                            if (rockStrataId >= strata.Variants.Length)
                            {
                                break;
                            }
                            stratum = strata.Variants[rockStrataId];
                            rockMap = mapChunk.MapRegion.RockStrata[rockStrataId];
                            step = (float)rockMap.InnerSize / regionChunkSize;

                            int grp = (int)stratum.RockGroup;
                            float diff = rockGroupMaxQuantity[grp] - rockGroupCounter[grp];
                            if (diff <= 0) continue;

                            rockGroupCounter[grp]++;
                            float dist = 1 + GameMath.Clamp((distx + distz) / 30, 0.9f, 1.1f);
                            strataThickness = Math.Min(rockGroupMaxThickness[grp] * dist * Math.Min(1, diff), rockMap.GetIntLerpedCorrectly(rdx * step + step * (float)(x + distx) / chunksize, rdz * step + step * (float)(z + distz) / chunksize));

                            strataThickness -= (stratum.RockGroup == EnumRockGroup.Sedimentary) ? Math.Max(0, yupper - TerraGenConfig.seaLevel)*0.5f : 0;

                            if (strataThickness <= 0 || strataThickness < 2) continue;
                        }

                        if (stratum.GenDir == EnumStratumGenDir.BottomUp)
                        {
                            int chunkY = ylower / chunksize;
                            int lY = ylower - chunkY * chunksize;
                            int localIndex3D = (chunksize * lY + z) * chunksize + x;

                            if (chunks[chunkY].Blocks[localIndex3D] == rockBlockId)
                            {
                                chunks[chunkY].Blocks[localIndex3D] = stratum.BlockId;
                            }

                            ylower++;
                                
                        } else {

                            int chunkY = yupper / chunksize;
                            int lY = yupper - chunkY * chunksize;
                            int localIndex3D = (chunksize * lY + z) * chunksize + x;

                            if (chunks[chunkY].Blocks[localIndex3D] == rockBlockId)
                            {
                                chunks[chunkY].Blocks[localIndex3D] = stratum.BlockId;
                            }

                            yupper--;
                        }
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

            return ProvinceMapByRegion[index2d] = new LerpedWeightedIndex2DMap(geoMap.Data, geoMap.Size, TerraGenConfig.geoProvSmoothingRadius, geoMap.TopLeftPadding, geoMap.BottomRightPadding);
        }


    }
}
