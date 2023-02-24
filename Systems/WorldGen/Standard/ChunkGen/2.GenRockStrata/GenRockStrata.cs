using System;
using System.Collections.Generic;
using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.ServerMods.NoObf;

namespace Vintagestory.ServerMods
{
    // Contains some haxy stuff and public fields that shouldn't be public so that the Prospecting Pick can also generate rock strata columns during probing
    public class GenRockStrataNew : ModStdWorldGen
    {
        ICoreServerAPI api;
        int regionSize;
        int regionChunkSize;

        public int rockBlockId;

        internal RockStrataConfig strata;

        internal SimplexNoise distort2dx;
        internal SimplexNoise distort2dz;
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

        internal void setApi(ICoreServerAPI api)
        {
            this.api = api;
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

            // Unpadded region noise size in chunks
            regionChunkSize = regionSize / chunksize;

            // Unpadded region noise size in blocks
            int geoProvRegionNoiseSize = regionSize / TerraGenConfig.geoProvMapScale;

            // Amount of regions in all of the map
            regionMapSize = api.WorldManager.MapSizeX / (chunksize * geoProvRegionNoiseSize);

            rockBlockId = (ushort)api.WorldManager.GetBlockId(new AssetLocation("rock-granite"));
            
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
            api.Logger.VerboseDebug("Initialised GenRockStrata");
        }



        private void OnMapRegionGen(IMapRegion mapRegion, int regionX, int regionZ, ITreeAttribute chunkGenParams = null)
        {
            int noiseSize = api.WorldManager.RegionSize / TerraGenConfig.rockStrataScale;
            int pad = 2;
            
            mapRegion.RockStrata = new IntDataMap2D[strata.Variants.Length];
            for (int i = 0; i < strata.Variants.Length; i++)
            {
                IntDataMap2D intmap = new IntDataMap2D();
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


        // Cheap ugly code for better performance :<
        #region
        float[] rockGroupMaxThickness = new float[4];
        int[] rockGroupCurrentThickness = new int[4];

        IMapChunk mapChunk;
        ushort[] heightMap;
        int rdx;
        int rdz;

        LerpedWeightedIndex2DMap map;
        float lerpMapInv;
        float chunkInRegionX;
        float chunkInRegionZ;

        GeologicProvinces provinces = NoiseGeoProvince.provinces;
        #endregion

        internal void GenChunkColumn(IServerChunk[] chunks, int chunkX, int chunkZ, ITreeAttribute chunkGenParams = null)
        {
            preLoad(chunks, chunkX, chunkZ);
            int chunksize = this.chunksize;

            for (int x = 0; x < chunksize; x++)
            {
                for (int z = 0; z < chunksize; z++)
                {
                    genBlockColumn(chunks, chunkX, chunkZ, x, z);
                }
            }
        }

        public void preLoad(IServerChunk[] chunks, int chunkX, int chunkZ)
        {
            mapChunk = chunks[0].MapChunk;
            heightMap = mapChunk.WorldGenTerrainHeightMap;
            rdx = chunkX % regionChunkSize;
            rdz = chunkZ % regionChunkSize;

            map = GetOrLoadLerpedProvinceMap(chunks[0].MapChunk, chunkX, chunkZ);
            lerpMapInv = 1f / TerraGenConfig.geoProvMapScale;
            chunkInRegionX = (chunkX % regionChunkSize) * lerpMapInv * chunksize;
            chunkInRegionZ = (chunkZ % regionChunkSize) * lerpMapInv * chunksize;

            provinces = NoiseGeoProvince.provinces;
        }

        public void genBlockColumn(IServerChunk[] chunks, int chunkX, int chunkZ, int lx, int lz)
        {
            int chunksize = this.chunksize;
            int surfaceY = heightMap[lz * chunksize + lx];
            int ylower = 1;
            int yupper = surfaceY;
            int rockBlockId = this.rockBlockId;

            rockGroupMaxThickness[0] = rockGroupMaxThickness[1] = rockGroupMaxThickness[2] = rockGroupMaxThickness[3] = 0;
            rockGroupCurrentThickness[0] = rockGroupCurrentThickness[1] = rockGroupCurrentThickness[2] = rockGroupCurrentThickness[3] = 0;

            WeightedIndex[] indices = map[
                chunkInRegionX + lx * lerpMapInv,
                chunkInRegionZ + lz * lerpMapInv
            ];
            for (int i = 0; i < indices.Length; i++)
            {
                float w = indices[i].Weight;

                GeologicProvinceRockStrata[] localstrata = provinces.Variants[indices[i].Index].RockStrataIndexed;

                rockGroupMaxThickness[0] += localstrata[0].ScaledMaxThickness * w;
                rockGroupMaxThickness[1] += localstrata[1].ScaledMaxThickness * w;
                rockGroupMaxThickness[2] += localstrata[2].ScaledMaxThickness * w;
                rockGroupMaxThickness[3] += localstrata[3].ScaledMaxThickness * w;
            }

            float distx = (float)distort2dx.Noise(chunkX * chunksize + lx, chunkZ * chunksize + lz);
            float distz = (float)distort2dz.Noise(chunkX * chunksize + lx, chunkZ * chunksize + lz);
            float thicknessDistort = GameMath.Clamp((distx + distz) / 30f, 0.9f, 1.1f);

            int rockStrataId = -1;
            RockStratum stratum = null;
            IntDataMap2D rockMap;
            float step;
            int grp = 0;
            float strataThickness = 0;

            while (ylower <= yupper)
            {
                if (--strataThickness <= 0f)
                {
                    rockStrataId++;
                    if (rockStrataId >= strata.Variants.Length || rockStrataId >= mapChunk.MapRegion.RockStrata.Length)
                    {
                        break;
                    }
                    stratum = strata.Variants[rockStrataId];
                    rockMap = mapChunk.MapRegion.RockStrata[rockStrataId];
                    step = (float)rockMap.InnerSize / regionChunkSize;

                    grp = (int)stratum.RockGroup;

                    float allowedThickness = rockGroupMaxThickness[grp] * thicknessDistort - rockGroupCurrentThickness[grp];

                    strataThickness = Math.Min(allowedThickness, rockMap.GetIntLerpedCorrectly(rdx * step + step * (float)(lx + distx) / chunksize, rdz * step + step * (float)(lz + distz) / chunksize));

                    if (stratum.RockGroup == EnumRockGroup.Sedimentary) strataThickness -= Math.Max(0, yupper - TerraGenConfig.seaLevel) * 0.5f;

                    if (strataThickness < 2f)
                    {
                        strataThickness = -1f;
                        continue;
                    }

                    if (stratum.BlockId == rockBlockId) // Special case for granite : no need to replace granite with granite!
                    {
                        int thickness = (int)strataThickness;
                        rockGroupCurrentThickness[grp] += thickness;
                        if (stratum.GenDir == EnumStratumGenDir.BottomUp)
                        {
                            ylower += thickness;
                        }
                        else
                        {
                            yupper -= thickness;
                        }
                        continue;
                    }
                }

                rockGroupCurrentThickness[grp]++;

                if (stratum.GenDir == EnumStratumGenDir.BottomUp)
                {
                    int chunkY = ylower / chunksize;
                    int lY = ylower - chunkY * chunksize;
                    int localIndex3D = (chunksize * lY + lz) * chunksize + lx;

                    IChunkBlocks chunkBlockData = chunks[chunkY].Data;
                    if (chunkBlockData.GetBlockIdUnsafe(localIndex3D) == rockBlockId)
                    {
                        chunkBlockData.SetBlockUnsafe(localIndex3D, stratum.BlockId);
                    }

                    ylower++;

                }
                else
                {

                    int chunkY = yupper / chunksize;
                    int lY = yupper - chunkY * chunksize;
                    int localIndex3D = (chunksize * lY + lz) * chunksize + lx;

                    IChunkBlocks chunkBlockData = chunks[chunkY].Data;
                    if (chunkBlockData.GetBlockIdUnsafe(localIndex3D) == rockBlockId)      // Even if we found rock already in this column, it could be overhang or something, so have to keep checking
                    {
                        chunkBlockData.SetBlockUnsafe(localIndex3D, stratum.BlockId);
                    }

                    yupper--;
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

        LerpedWeightedIndex2DMap CreateLerpedProvinceMap(IntDataMap2D geoMap, int regionX, int regionZ)
        {
            int index2d = regionZ * regionMapSize + regionX;

            return ProvinceMapByRegion[index2d] = new LerpedWeightedIndex2DMap(geoMap.Data, geoMap.Size, TerraGenConfig.geoProvSmoothingRadius, geoMap.TopLeftPadding, geoMap.BottomRightPadding);
        }


    }
}
