using System;
using System.Collections.Generic;
using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.ServerMods.NoObf;

namespace Vintagestory.ServerMods
{
    public class GenTerra : ModStdWorldGen
    {
        ICoreServerAPI api;
        int regionChunkSize;
        int regionMapSize;

        private NormalizedSimplexNoise TerrainNoise;

        GenRockStrata rockstrataGen;

        LandformsWorldProperty landforms;
        Dictionary<int, LerpedWeightedIndex2DMap> LandformMapByRegion = new Dictionary<int, LerpedWeightedIndex2DMap>(10);


        public override bool ShouldLoad(EnumAppSide side)
        {
            return side == EnumAppSide.Server;
        }


        public override double ExecuteOrder()
        {
            return 0;
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            this.api = api;

            api.Event.ChunkColumnGeneration(OnChunkColumnGen, EnumWorldGenPass.Terrain);
            api.Event.SaveGameLoaded += GameWorldLoaded;

            rockstrataGen = new GenRockStrata();
            rockstrataGen.StartServerSide(api);

            // Call our loaded method manually if the server is already running (happens when mods are reloaded at runtime)
            if (api.Server.CurrentRunPhase == EnumServerRunPhase.RunGame)
            {
                GameWorldLoaded();
            }
        }

        // We generate the whole terrain here so we instantly know the heightmap
        int lerpHor;
        int lerpVer;
        int noiseWidth;
        int paddedNoiseWidth;
        int paddedNoiseHeight;
        int noiseHeight;
        float lerpDeltaHor;
        float lerpDeltaVert;

        double[] noiseTemp;


        public void GameWorldLoaded()
        {
            LoadGlobalConfig(api);
            LandformMapByRegion.Clear();

            chunksize = api.WorldManager.ChunkSize;
            // Unpadded region noise size in blocks
            int landFormRegionNoiseSize = api.WorldManager.RegionSize / TerraGenConfig.landformMapScale;
            // Unpadded region noise size in chunks
            regionChunkSize = api.WorldManager.RegionSize / chunksize;
            // Amount of landform regions in all of the map 
            regionMapSize = api.WorldManager.MapSizeX / api.WorldManager.RegionSize;


            TerrainNoise = NormalizedSimplexNoise.FromDefaultOctaves(
                TerraGenConfig.terrainGenOctaves, 0.001, 0.9, api.WorldManager.Seed
            );

            // We generate the whole terrain here so we instantly know the heightmap
            lerpHor = TerraGenConfig.lerpHorizontal;
            lerpVer = TerraGenConfig.lerpVertical;

            noiseWidth = chunksize / lerpHor;
            noiseHeight = api.WorldManager.MapSizeY / lerpVer;

            paddedNoiseWidth = noiseWidth + 1;
            paddedNoiseHeight = noiseHeight + 1;

            lerpDeltaHor = 1f / lerpHor;
            lerpDeltaVert = 1f / lerpVer;

            rockstrataGen.OnGameWorldLoaded();

            noiseTemp = new double[paddedNoiseWidth * paddedNoiseWidth * paddedNoiseHeight];

            if (api.WorldManager.SaveGame.WorldPlayStyle != EnumPlayStyle.CreativeBuilding)
            {
                TerraGenConfig.seaLevel = (int)(0.4313725490196078 * api.WorldManager.MapSizeY);
                api.WorldManager.SetSealLevel(TerraGenConfig.seaLevel);
            }
        }






        private void OnChunkColumnGen(IServerChunk[] chunks, int chunkX, int chunkZ)
        {
            landforms = NoiseLandforms.landforms;

            IntMap regionMap = chunks[0].MapChunk.MapRegion.LandformMap;
            // Amount of pixels for each chunk (probably 1, 2, or 4)
            float chunkPixelSize = regionMap.InnerSize / regionChunkSize;
            // Adjusted lerp for the noiseWidth
            float chunkPixelStep = chunkPixelSize / noiseWidth;
            // Start coordinates for the chunk in the region map
            float baseX = regionMap.TopLeftPadding + (chunkX % regionChunkSize) * chunkPixelSize;
            float baseZ = regionMap.TopLeftPadding + (chunkZ % regionChunkSize) * chunkPixelSize;


            LerpedWeightedIndex2DMap landLerpMap = GetOrLoadLerpedLandformMap(chunks[0].MapChunk, chunkX / regionChunkSize, chunkZ / regionChunkSize);

            // Terrain octaves
            double[] octNoiseX0, octNoiseX1, octNoiseX2, octNoiseX3;
            double[] octThX0, octThX1, octThX2, octThX3;

            GetInterpolatedOctaves(landLerpMap[baseX, baseZ], out octNoiseX0, out octThX0);
            GetInterpolatedOctaves(landLerpMap[baseX + chunkPixelSize , baseZ], out octNoiseX1, out octThX1);
            GetInterpolatedOctaves(landLerpMap[baseX, baseZ + chunkPixelSize], out octNoiseX2, out octThX2);
            GetInterpolatedOctaves(landLerpMap[baseX + chunkPixelSize, baseZ + chunkPixelSize], out octNoiseX3, out octThX3);


            double[] terrainNoise3d = GetTerrainNoise3D(octNoiseX0, octNoiseX1, octNoiseX2, octNoiseX3, octThX0, octThX1, octThX2, octThX3, chunkX * noiseWidth, 0, chunkZ * noiseWidth);

            // Store heightmap in the map chunk
            ushort[] rainheightmap = chunks[0].MapChunk.RainHeightMap;
            ushort[] terrainheightmap = chunks[0].MapChunk.WorldGenTerrainHeightMap;
            

            // Terrain thresholds
            double tnoiseY0;
            double tnoiseY1;
            double tnoiseY2;
            double tnoiseY3;
            double tnoiseGainY0;
            double tnoiseGainY1;
            double tnoiseGainY2;
            double tnoiseGainY3;


            double thNoiseX0;
            double thNoiseX1;
            double thNoiseGainX0;
            double thNoiseGainX1;
            double thNoiseGainZ0;
            double thNoiseZ0;

            float[] terrainThresholdsX0 = new float[api.WorldManager.MapSizeY];
            float[] terrainThresholdsX1 = new float[api.WorldManager.MapSizeY];
            float[] terrainThresholdsX2 = new float[api.WorldManager.MapSizeY];
            float[] terrainThresholdsX3 = new float[api.WorldManager.MapSizeY];



            for (int xN = 0; xN < noiseWidth; xN++)
            {
                for (int zN = 0; zN < noiseWidth; zN++)
                {
                    // Landform thresholds
                    LoadInterpolatedThresholds(landLerpMap[baseX + xN * chunkPixelStep, baseZ + zN * chunkPixelStep], terrainThresholdsX0);
                    LoadInterpolatedThresholds(landLerpMap[baseX + (xN+1) * chunkPixelStep, baseZ + zN * chunkPixelStep], terrainThresholdsX1);
                    LoadInterpolatedThresholds(landLerpMap[baseX + xN * chunkPixelStep, baseZ + (zN+1) * chunkPixelStep], terrainThresholdsX2);
                    LoadInterpolatedThresholds(landLerpMap[baseX + (xN+1) * chunkPixelStep, baseZ + (zN+1) * chunkPixelStep], terrainThresholdsX3);

                    for (int yN = 0; yN < noiseHeight; yN++)
                    {
                        // Terrain noise
                        tnoiseY0 = terrainNoise3d[NoiseIndex3d(xN, yN, zN)];
                        tnoiseY1 = terrainNoise3d[NoiseIndex3d(xN, yN, zN + 1)];
                        tnoiseY2 = terrainNoise3d[NoiseIndex3d(xN + 1, yN, zN)];
                        tnoiseY3 = terrainNoise3d[NoiseIndex3d(xN + 1, yN, zN + 1)];

                        tnoiseGainY0 = (terrainNoise3d[NoiseIndex3d(xN, yN + 1, zN)] - tnoiseY0) * lerpDeltaVert;
                        tnoiseGainY1 = (terrainNoise3d[NoiseIndex3d(xN, yN + 1, zN + 1)] - tnoiseY1) * lerpDeltaVert;
                        tnoiseGainY2 = (terrainNoise3d[NoiseIndex3d(xN + 1, yN + 1, zN)] - tnoiseY2) * lerpDeltaVert;
                        tnoiseGainY3 = (terrainNoise3d[NoiseIndex3d(xN + 1, yN + 1, zN + 1)] - tnoiseY3) * lerpDeltaVert;

                        

                        for (int y = 0; y < lerpVer; y++)
                        {
                            int posY = yN * lerpVer + y;
                            int chunkY = posY / chunksize;
                            int localY = posY % chunksize;

                            // For Terrain noise 
                            double tnoiseX0 = tnoiseY0;
                            double tnoiseX1 = tnoiseY1;

                            double tnoiseGainX0 = (tnoiseY2 - tnoiseY0) * lerpDeltaHor;
                            double tnoiseGainX1 = (tnoiseY3 - tnoiseY1) * lerpDeltaHor;

                            // Landform thresholds lerp
                            thNoiseX0 = terrainThresholdsX0[posY];
                            thNoiseX1 = terrainThresholdsX2[posY];

                            thNoiseGainX0 = (terrainThresholdsX1[posY] - thNoiseX0) * lerpDeltaHor;
                            thNoiseGainX1 = (terrainThresholdsX3[posY] - thNoiseX1) * lerpDeltaHor;


                            for (int x = 0; x < lerpHor; x++)
                            {
                                // For terrain noise
                                double tnoiseZ0 = tnoiseX0;
                                double tnoiseGainZ0 = (tnoiseX1 - tnoiseX0) * lerpDeltaHor;

                                // Landform
                                thNoiseZ0 = thNoiseX0;
                                thNoiseGainZ0 = (thNoiseX1 - thNoiseX0) * lerpDeltaHor;

                                for (int z = 0; z < lerpHor; z++)
                                {
                                    int lX = xN * lerpHor + x;
                                    int lZ = zN * lerpHor + z;
                                    
                                    int mapIndex = ChunkIndex2d(lX, lZ);
                                    int chunkIndex = ChunkIndex3d(lX, localY, lZ);

                                    chunks[chunkY].Blocks[chunkIndex] = 0;

                                    if (posY == 0)
                                    {
                                        chunks[chunkY].Blocks[chunkIndex] = GlobalConfig.mantleBlockId;
                                        continue;
                                    }

                                    

                                    if (tnoiseZ0 > thNoiseZ0)
                                    {
                                        terrainheightmap[mapIndex] = rainheightmap[mapIndex] = (ushort)Math.Max(rainheightmap[mapIndex], posY);

                                        chunks[chunkY].Blocks[chunkIndex] = GlobalConfig.defaultRockId;
                                    }
                                    else
                                    {
                                        if (posY < TerraGenConfig.seaLevel)
                                        {
                                            terrainheightmap[mapIndex] = rainheightmap[mapIndex] = (ushort)Math.Max(rainheightmap[mapIndex], posY);

                                            chunks[chunkY].Blocks[chunkIndex] = GlobalConfig.waterBlockId;
                                        }
                                        else
                                        {
                                            chunks[chunkY].Blocks[chunkIndex] = 0;
                                        }
                                    }
                                    
                                    tnoiseZ0 += tnoiseGainZ0;
                                    thNoiseZ0 += thNoiseGainZ0;
                                }

                                tnoiseX0 += tnoiseGainX0;
                                tnoiseX1 += tnoiseGainX1;

                                thNoiseX0 += thNoiseGainX0;
                                thNoiseX1 += thNoiseGainX1;
                            }

                            tnoiseY0 += tnoiseGainY0;
                            tnoiseY1 += tnoiseGainY1;
                            tnoiseY2 += tnoiseGainY2;
                            tnoiseY3 += tnoiseGainY3;
                        }
                    }
                }
            }

            int ymax = 0;
            for (int i = 0; i < rainheightmap.Length; i++)
            {
                ymax = Math.Max(ymax, rainheightmap[i]);
            }
            chunks[0].MapChunk.YMax = (ushort)ymax;
        }


        
        LerpedWeightedIndex2DMap GetOrLoadLerpedLandformMap(IMapChunk mapchunk, int regionX, int regionZ)
        {
            LerpedWeightedIndex2DMap map;
            // 1. Load?
            LandformMapByRegion.TryGetValue(regionZ * regionMapSize + regionX, out map);
            if (map != null) return map;

            // 2. Create
            map = LandformMapByRegion[regionZ * regionMapSize + regionX] 
                = new LerpedWeightedIndex2DMap(mapchunk.MapRegion.LandformMap.Data, mapchunk.MapRegion.LandformMap.Size, TerraGenConfig.landFormSmoothingRadius);

            return map;
        }


        // Can be called only once per x/z coordinate to get a list of all thresholds for this column
        private void LoadInterpolatedThresholds(WeightedIndex[] indices, float[] values)
        {
            for (int y = 0; y < values.Length; y++)
            {
                float threshold = 0;
                for (int i = 0; i < indices.Length; i++)
                {
                    threshold += landforms.LandFormsByIndex[indices[i].index].TerrainYThresholds[y] * indices[i].weight;
                }

                values[y] = threshold;
            }
        }


        private float LoadOneInterpolatedThreshold(WeightedIndex[] indices, int y)
        {
            float threshold = 0;
            for (int i = 0; i < indices.Length; i++)
            {
                threshold += landforms.LandFormsByIndex[indices[i].index].TerrainYThresholds[y] * indices[i].weight;
            }

            return threshold;
        }


        private void GetInterpolatedOctaves(WeightedIndex[] indices, out double[] amps, out double[] thresholds)
        {
            amps = new double[TerraGenConfig.terrainGenOctaves];
            thresholds = new double[TerraGenConfig.terrainGenOctaves];

            for (int octave = 0; octave < TerraGenConfig.terrainGenOctaves; octave++)
            {
                double amplitude = 0;
                double threshold = 0;
                for (int i = 0; i < indices.Length; i++)
                {
                    LandformVariant l = landforms.LandFormsByIndex[indices[i].index];
                    amplitude += l.TerrainOctaves[octave] * indices[i].weight;
                    threshold += l.TerrainOctaveThresholds[octave] * indices[i].weight;
                }

                amps[octave] = amplitude;
                thresholds[octave] = threshold;
            }    
        }

        
        double[] lerpedAmps = new double[TerraGenConfig.terrainGenOctaves];
        double[] lerpedTh = new double[TerraGenConfig.terrainGenOctaves];

        double[] GetTerrainNoise3D(double[] octX0, double[] octX1, double[] octX2, double[] octX3, double[] octThX0, double[] octThX1, double[] octThX2, double[] octThX3, int xPos, int yPos, int zPos)
        {
            for (int x = 0; x < paddedNoiseWidth; x++)
            {
                for (int z = 0; z < paddedNoiseWidth; z++)
                {
                    for (int i = 0; i < TerraGenConfig.terrainGenOctaves; i++)
                    {
                        lerpedAmps[i] = GameMath.BiLerp(octX0[i], octX1[i], octX2[i], octX3[i], (double)x / paddedNoiseWidth, (double)z / paddedNoiseWidth);
                        lerpedTh[i] = GameMath.BiLerp(octThX0[i], octThX1[i], octThX2[i], octThX3[i], (double)x / paddedNoiseWidth, (double)z / paddedNoiseWidth);
                    }

                    for (int y = 0; y < paddedNoiseHeight; y++)
                    {
                        noiseTemp[NoiseIndex3d(x, y, z)] = TerrainNoise.Noise(
                            (xPos + x),
                            (yPos + y) / TerraGenConfig.terrainNoiseVerticalScale,
                            (zPos + z),
                            lerpedAmps,
                            lerpedTh
                        );
                    }
                }
            }

            return noiseTemp;
        }


        int Index3d(int x, int y, int z, int width, int length)
        {
            return (y * length + z) * width + x;
        }

        private int ChunkIndex3d(int x, int y, int z)
        {
            return (y * chunksize + z) * chunksize + x;
        }

        private int ChunkIndex2d(int x, int z)
        {
            return z * chunksize + x;
        }

        private int NoiseIndex3d(int x, int y, int z)
        {
            return (y * paddedNoiseWidth + z) * paddedNoiseWidth + x;
        }
    }
}
