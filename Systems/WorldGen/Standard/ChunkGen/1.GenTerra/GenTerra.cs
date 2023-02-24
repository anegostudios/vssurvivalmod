using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.ServerMods.NoObf;

namespace Vintagestory.ServerMods
{
    public class GenTerra : ModStdWorldGen
    {
        ICoreServerAPI api;
        int regionMapSize;

        private NormalizedSimplexNoise TerrainNoise;

        LandformsWorldProperty landforms;
        Dictionary<int, LerpedWeightedIndex2DMap> LandformMapByRegion = new Dictionary<int, LerpedWeightedIndex2DMap>(10);

        SimplexNoise distort2dx;
        NormalizedSimplexNoise geoUpheavaldy;
        NormalizedSimplexNoise geoOceandy;
        float oceanessStrengthInv;

        SimplexNoise distort2dz;

        // We generate the whole terrain here so we instantly know the heightmap
        const int lerpHor = TerraGenConfig.lerpHorizontal;
        const int lerpVer = TerraGenConfig.lerpVertical;
        int noiseWidth;
        int paddedNoiseWidth;
        int paddedNoiseHeight;
        int noiseHeight;
        const float lerpDeltaHor = 1f / lerpHor;
        const float lerpDeltaVert = 1f / lerpVer;

        double[] noiseTemp;
        float noiseScale;

        float[] terrainThresholdsX0;
        float[] terrainThresholdsX1;
        float[] terrainThresholdsX2;
        float[] terrainThresholdsX3;

        double continentalNoiseOffsetX;
        double continentalNoiseOffsetZ;


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

            api.Event.ServerRunPhase(EnumServerRunPhase.ModsAndConfigReady, loadGamePre);
            api.Event.InitWorldGenerator(initWorldGen, "standard");
            api.Event.ChunkColumnGeneration(OnChunkColumnGen, EnumWorldGenPass.Terrain, "standard");

            api.Event.SaveGameLoaded += Event_SaveGameLoaded;
        }

        private void Event_SaveGameLoaded()
        {
            continentalNoiseOffsetX = api.WorldManager.SaveGame.GetData<double>("continentalNoiseOffsetX");
            continentalNoiseOffsetZ = api.WorldManager.SaveGame.GetData<double>("continentalNoiseOffsetZ");

            ITreeAttribute worldConfig = api.WorldManager.SaveGame.WorldConfiguration;
            float str = worldConfig.GetString("oceanessStrength", "0").ToFloat(0);

            // oceanessStrengthInv = 0       => everywhere ocean
            // oceanessStrengthInv = 0.65    => common oceans
            // oceanessStrengthInv = 0.85    => rare
            // oceanessStrengthInv = 1       => no oceans
            oceanessStrengthInv = GameMath.Clamp(1 - str, 0, 1);
        }

        private void loadGamePre()
        {
            if (api.WorldManager.SaveGame.WorldType != "standard") return;
            
            TerraGenConfig.seaLevel = (int)(0.4313725490196078 * api.WorldManager.MapSizeY);
            api.WorldManager.SetSeaLevel(TerraGenConfig.seaLevel);
        }

        int terrainGenOctaves = 9;
        double[] lerpedAmps;
        double[] lerpedTh;
        int[] disty = new int[4];
        
        double[,][] distxz = new double[2, 2][];
        double[] distxz00, distxz01, distxz10, distxz11;

        public void initWorldGen()
        {
            distxz[0, 0] = distxz00 = new double[2];
            distxz[0, 1] = distxz01 = new double[2];
            distxz[1, 0] = distxz10 = new double[2];
            distxz[1, 1] = distxz11 = new double[2];

            LoadGlobalConfig(api);
            LandformMapByRegion.Clear();

            chunksize = api.WorldManager.ChunkSize;

            regionMapSize = (int)Math.Ceiling((double)api.WorldManager.MapSizeX / api.WorldManager.RegionSize);
            noiseScale = Math.Max(1, api.WorldManager.MapSizeY / 256f);

            terrainGenOctaves = TerraGenConfig.GetTerrainOctaveCount(api.WorldManager.MapSizeY);

            TerrainNoise = NormalizedSimplexNoise.FromDefaultOctaves(
                terrainGenOctaves, 0.002 / noiseScale, 0.9, api.WorldManager.Seed
            );
            lerpedAmps = new double[terrainGenOctaves];
            lerpedTh = new double[terrainGenOctaves];


            noiseWidth = chunksize / lerpHor;
            noiseHeight = api.WorldManager.MapSizeY / lerpVer;

            paddedNoiseWidth = noiseWidth + 1;
            paddedNoiseHeight = noiseHeight + 1;

            noiseTemp = new double[paddedNoiseWidth * paddedNoiseWidth * paddedNoiseHeight];

            distort2dx = new SimplexNoise(
                new double[] { 55, 40, 30, 10 }, 
                scaleAdjustedFreqs(new double[] { 1 / 5.0, 1 / 2.50, 1 / 1.250, 1 / 0.65 }, noiseScale), 
                api.World.Seed + 9876 + 0
            );
            geoUpheavaldy = new NormalizedSimplexNoise(
                new double[] { 55, 40, 30, 10, 2 },
                scaleAdjustedFreqs(new double[] { 1 / 5.0 / 1.1, 1 / 2.50 / 1.1, 1 / 1.250 / 1.1, 1 / 0.65 / 1.1, 4 }, noiseScale), 
                api.World.Seed + 9876 + 1
            );

            geoOceandy = NormalizedSimplexNoise.FromDefaultOctaves(6, 1 / 60.0, 0.8, api.World.Seed + 9856 + 1);

            // Find a non-oceanic spot
            if (api.WorldManager.SaveGame.IsNew && oceanessStrengthInv < 1)
            {
                var rnd = new Random(api.World.Seed + 2837986);
                BlockPos pos = new BlockPos(api.WorldManager.MapSizeX / 2, 0, api.WorldManager.MapSizeZ / 2);
                int tries = 0;

                while (tries++ < 4000)
                {
                    continentalNoiseOffsetX = (10 * tries) / 400.0 * (1 - 2 * rnd.Next(2));
                    continentalNoiseOffsetZ = (10 * tries) / 400.0 * (1 - 2 * rnd.Next(2));

                    var noiseVal = (int)Math.Max(0, 255 * Math.Min(1, 2 * geoOceandy.Noise(
                        continentalNoiseOffsetX + pos.X / 400.0,
                        continentalNoiseOffsetZ + pos.Z / 400.0
                    ) - oceanessStrengthInv));
                    
                    if (noiseVal <= 0)
                    {
                        api.WorldManager.SaveGame.StoreData<double>("continentalNoiseOffsetX", continentalNoiseOffsetX);
                        api.WorldManager.SaveGame.StoreData<double>("continentalNoiseOffsetZ", continentalNoiseOffsetZ);
                        break;
                    }
                }
            }


             distort2dz = new SimplexNoise(
                new double[] { 55, 40, 30, 10 }, 
                scaleAdjustedFreqs(new double[] { 1 / 5.0, 1 / 2.50, 1 / 1.250, 1 / 0.65 }, noiseScale), 
                api.World.Seed + 9876 + 2
            );

            terrainThresholdsX0 = new float[api.WorldManager.MapSizeY];
            terrainThresholdsX1 = new float[api.WorldManager.MapSizeY];
            terrainThresholdsX2 = new float[api.WorldManager.MapSizeY];
            terrainThresholdsX3 = new float[api.WorldManager.MapSizeY];

            api.Logger.VerboseDebug("Initialised GenTerra");
        }

        private double[] scaleAdjustedFreqs(double[] vs, float horizontalScale)
        {
            for (int i = 0; i < vs.Length; i++)
            {
                vs[i] /= horizontalScale;
            }

            return vs;
        }


        

        private void OnChunkColumnGen(IServerChunk[] chunks, int chunkX, int chunkZ, ITreeAttribute chunkGenParams = null)
        {
            landforms = NoiseLandforms.landforms;
            IMapChunk mapchunk = chunks[0].MapChunk;
            int chunksize = this.chunksize;

            int climateUpLeft;
            int climateUpRight;
            int climateBotLeft;
            int climateBotRight;

            int upheavelMapUpLeft = 0;
            int upheavelMapUpRight = 0;
            int upheavelMapBotLeft = 0;
            int upheavelMapBotRight = 0;

            IntDataMap2D climateMap = chunks[0].MapChunk.MapRegion.ClimateMap;
            int regionChunkSize = api.WorldManager.RegionSize / chunksize;
            float cfac = (float)climateMap.InnerSize / regionChunkSize;
            int rlX = chunkX % regionChunkSize;
            int rlZ = chunkZ % regionChunkSize;

            climateUpLeft = climateMap.GetUnpaddedInt((int)(rlX * cfac), (int)(rlZ * cfac));
            climateUpRight = climateMap.GetUnpaddedInt((int)(rlX * cfac + cfac), (int)(rlZ * cfac));
            climateBotLeft = climateMap.GetUnpaddedInt((int)(rlX * cfac), (int)(rlZ * cfac + cfac));
            climateBotRight = climateMap.GetUnpaddedInt((int)(rlX * cfac + cfac), (int)(rlZ * cfac + cfac));

            IntDataMap2D upheavelMap = chunks[0].MapChunk.MapRegion.UpheavelMap;
            if (upheavelMap != null)
            {
                float ufac = (float)upheavelMap.InnerSize / regionChunkSize;
                upheavelMapUpLeft = upheavelMap.GetUnpaddedInt((int)(rlX * ufac), (int)(rlZ * ufac));
                upheavelMapUpRight = upheavelMap.GetUnpaddedInt((int)(rlX * ufac + ufac), (int)(rlZ * ufac));
                upheavelMapBotLeft = upheavelMap.GetUnpaddedInt((int)(rlX * ufac), (int)(rlZ * ufac + ufac));
                upheavelMapBotRight = upheavelMap.GetUnpaddedInt((int)(rlX * ufac + ufac), (int)(rlZ * ufac + ufac));
            }

            int waterID = GlobalConfig.waterBlockId;
            int rockID = GlobalConfig.defaultRockId;


            IntDataMap2D landformMap = mapchunk.MapRegion.LandformMap;
            // Amount of pixels for each chunk (probably 1, 2, or 4) in the land form map
            float chunkPixelSize = landformMap.InnerSize / regionChunkSize;
            // Adjusted lerp for the noiseWidth
            float chunkPixelStep = chunkPixelSize / noiseWidth;
            // Start coordinates for the chunk in the region map
            float baseX = (chunkX % regionChunkSize) * chunkPixelSize;
            float baseZ = (chunkZ % regionChunkSize) * chunkPixelSize;
            

            LerpedWeightedIndex2DMap landLerpMap = GetOrLoadLerpedLandformMap(chunks[0].MapChunk, chunkX / regionChunkSize, chunkZ / regionChunkSize);

            // Terrain octaves
            double[] octNoiseX0, octNoiseX1, octNoiseX2, octNoiseX3;
            double[] octThX0, octThX1, octThX2, octThX3;

            // So it seems we have some kind of off-by-one error here? 
            // When the slope of a mountain goes up (in positive z or x direction), particularly at large word heights (512+)
            // then the last blocks (again in postive x/z dir) are below of where they should be?
            // I have no idea why, but this 0.25f offset seems to greatly mitigate the issue

            // I think its because GetTerrainNoise3D() picks up 4 extra blocks beyond the current chunk bounds, but we did not adjust the retrieval of noise data from GetInterpolatedOctaves() for it
            // we ought to add 4/chunksize * chunkPixelSize to it... which just so happens to match our guessed value of 0.25f, lol.
            float p = (float)lerpHor / chunksize;
            float weirdOffset = p * chunkPixelSize; // 0.25f;
            chunkPixelSize += weirdOffset;

            GetInterpolatedOctaves(landLerpMap[baseX, baseZ], out octNoiseX0, out octThX0);
            GetInterpolatedOctaves(landLerpMap[baseX + chunkPixelSize, baseZ], out octNoiseX1, out octThX1);
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

            

            

            int mapsizeY = api.WorldManager.MapSizeY;
            int mapsizeYm1 = api.WorldManager.MapSizeY - 1;
            int taperThreshold = (int)(mapsizeY * 0.9f);
            double geoUpheavalAmplitude = 255;

            for (int dx = 0; dx <= 1; dx++)
            {
                for (int dz = 0; dz <= 1; dz++)
                {
                    float distx = (float)distort2dx.Noise((chunkX * noiseWidth + dx * paddedNoiseWidth) / 100.0, (chunkZ * noiseWidth + dz * paddedNoiseWidth) / 100.0) * 10;
                    float distz = (float)distort2dz.Noise((chunkX * noiseWidth + dx * paddedNoiseWidth) / 100.0, (chunkZ * noiseWidth + dz * paddedNoiseWidth) / 100.0) * 10;
                    distx = (distx > 0 ? Math.Max(0, distx - 10) : Math.Min(0, distx + 10));
                    distz = (distz > 0 ? Math.Max(0, distz - 10) : Math.Min(0, distz + 10));

                    distxz[dx, dz][0] = distx;
                    distxz[dx, dz][1] = distz;
                }
            }

            for (int xN = 0; xN < noiseWidth; xN++)
            {
                for (int zN = 0; zN < noiseWidth; zN++)
                {
                    // Landform terrain thresholds
                    LoadInterpolatedThresholds(landLerpMap[baseX + xN * chunkPixelStep, baseZ + zN * chunkPixelStep], terrainThresholdsX0);
                    LoadInterpolatedThresholds(landLerpMap[baseX + (xN+1) * chunkPixelStep, baseZ + zN * chunkPixelStep], terrainThresholdsX1);
                    LoadInterpolatedThresholds(landLerpMap[baseX + xN * chunkPixelStep, baseZ + (zN+1) * chunkPixelStep], terrainThresholdsX2);
                    LoadInterpolatedThresholds(landLerpMap[baseX + (xN+1) * chunkPixelStep, baseZ + (zN+1) * chunkPixelStep], terrainThresholdsX3);

                    for (int dx = 0; dx <= 1; dx++)
                    {
                        for (int dz = 0; dz <= 1; dz++)
                        {
                            double distx = GameMath.BiLerp(distxz00[0], distxz10[0], distxz01[0], distxz11[0], (double)(xN + dx) / paddedNoiseWidth, (double)(zN + dz) / paddedNoiseWidth);
                            double distz = GameMath.BiLerp(distxz00[1], distxz10[1], distxz01[1], distxz11[1], (double)(xN + dx) / paddedNoiseWidth, (double)(zN + dz) / paddedNoiseWidth);

                            double oceannes = oceanessStrengthInv >= 1 ? -255 : 255 * Math.Min(1, 2 * geoOceandy.Noise(
                                continentalNoiseOffsetX + (chunkX * chunksize + (xN + dx) * lerpHor) / 400.0,
                                continentalNoiseOffsetZ + (chunkZ * chunksize + (zN + dz) * lerpHor) / 400.0
                            ) - oceanessStrengthInv);

                            int distyhere;

                            if (oceannes < 0)
                            {
                                double upHeavelStrength = GameMath.BiLerp(upheavelMapUpLeft, upheavelMapUpRight, upheavelMapBotLeft, upheavelMapBotRight, (double)(xN + dx) / noiseWidth, (double)(zN + dz) / noiseWidth);

                                double str = Math.Min(1, -oceannes * 10.0 / 255.0);
                                distyhere = -(int)(str * upHeavelStrength * Math.Max(0, geoUpheavaldy.Noise((chunkX * chunksize + (xN + dx) * lerpHor + distx) / 400.0, (chunkZ * chunksize + (zN + dz) * lerpHor + distz) / 400.0) - 0.5));
                            }
                            else
                            {
                                distyhere = (int)oceannes;
                            }

                            // Positive values = submerge terrain
                            // Negative values = raise terrain
                            disty[dz * 2 + dx] = distyhere;
                        }
                    }

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
                            IChunkBlocks chunkBlockData = chunks[chunkY].Data;

                            if (posY == 0)
                            {
                                int chunkIndex = ChunkIndex3d(xN * lerpHor, localY, zN * lerpHor);
                                chunkBlockData.SetBlockBulk(chunkIndex, lerpHor, lerpHor, GlobalConfig.mantleBlockId);
                            }
                            else
                            {
                                // For Terrain noise 
                                double tnoiseX0 = tnoiseY0;
                                double tnoiseX1 = tnoiseY1;

                                double tnoiseGainX0 = (tnoiseY2 - tnoiseY0) * lerpDeltaHor;
                                double tnoiseGainX1 = (tnoiseY3 - tnoiseY1) * lerpDeltaHor;

                                // Landform thresholds lerp
                                int distortedposY0 = GameMath.Clamp(posY + disty[0], 0, mapsizeYm1);
                                thNoiseX0 = terrainThresholdsX0[distortedposY0];
                                int distortedposY2 = GameMath.Clamp(posY + disty[2], 0, mapsizeYm1);
                                thNoiseX1 = terrainThresholdsX2[distortedposY2];

                                int distortedposY1 = GameMath.Clamp(posY + disty[1], 0, mapsizeYm1);
                                double thNoiseY2 = terrainThresholdsX1[distortedposY1];
                                int distortedposY3 = GameMath.Clamp(posY + disty[3], 0, mapsizeYm1);
                                double thNoiseY3 = terrainThresholdsX3[distortedposY3];

                                if (posY >= TerraGenConfig.seaLevel && Math.Max(Math.Max(tnoiseY0, tnoiseY1), Math.Max(tnoiseY2, tnoiseY3)) <= Math.Min(Math.Min(thNoiseX0, thNoiseX1), Math.Min(thNoiseY2, thNoiseY3)))
                                {
                                    // Nothing to do: whole slice is air
                                }
                                else
                                {
                                    thNoiseGainX0 = (thNoiseY2 - thNoiseX0) * lerpDeltaHor;
                                    thNoiseGainX1 = (thNoiseY3 - thNoiseX1) * lerpDeltaHor;

                                    for (int x = 0; x < lerpHor; x++)
                                    {
                                        // For terrain noise
                                        double tnoiseZ0 = tnoiseX0;
                                        double tnoiseGainZ0 = (tnoiseX1 - tnoiseX0) * lerpDeltaHor;

                                        // Landform
                                        thNoiseZ0 = thNoiseX0;
                                        thNoiseGainZ0 = (thNoiseX1 - thNoiseX0) * lerpDeltaHor;

                                        int lX = xN * lerpHor + x;

                                        for (int z = 0; z < lerpHor; z++)
                                        {                                          
                                            int lZ = zN * lerpHor + z;

                                            double geoUpheavelTaper = 0;
                                            if (posY > taperThreshold && disty[0] <- 2)
                                            {
                                                double upheavelness = -(GameMath.BiLerp(distortedposY0, distortedposY1, distortedposY2, distortedposY3, x * lerpDeltaHor, z * lerpDeltaHor) - posY) / geoUpheavalAmplitude;
                                                double ceilingness = (posY - taperThreshold) / 10.0;
                                                geoUpheavelTaper = ceilingness * upheavelness / 4.0;
                                            }

                                            if (tnoiseZ0 > thNoiseZ0 + geoUpheavelTaper)
                                            {
                                                int mapIndex = ChunkIndex2d(lX, lZ);
                                                terrainheightmap[mapIndex] = (ushort)posY;
                                                rainheightmap[mapIndex] = (ushort)posY;

                                                chunkBlockData[ChunkIndex3d(lX, localY, lZ)] = rockID;
                                            }
                                            else if (posY < TerraGenConfig.seaLevel)
                                            {
                                                int mapIndex = ChunkIndex2d(lX, lZ);
                                                rainheightmap[mapIndex] = (ushort)posY;

                                                int blockId;
                                                if (posY == TerraGenConfig.seaLevel - 1)
                                                {
                                                    int temp = (GameMath.BiLerpRgbColor(((float)lX) / chunksize, ((float)lZ) / chunksize, climateUpLeft, climateUpRight, climateBotLeft, climateBotRight) >> 16) & 0xff;
                                                    float distort = (float)distort2dx.Noise(chunkX * chunksize + lX, chunkZ * chunksize + lZ) / 20f;
                                                    float tempf = TerraGenConfig.GetScaledAdjustedTemperatureFloat(temp, 0) + distort;

                                                    blockId = (tempf < TerraGenConfig.WaterFreezingTempOnGen) ? GlobalConfig.lakeIceBlockId : waterID;
                                                }
                                                else
                                                {
                                                    blockId = waterID;
                                                }

                                                chunkBlockData.SetFluid(ChunkIndex3d(lX, localY, lZ), blockId);
                                            }

                                            tnoiseZ0 += tnoiseGainZ0;
                                            thNoiseZ0 += thNoiseGainZ0;
                                        }

                                        tnoiseX0 += tnoiseGainX0;
                                        thNoiseX0 += thNoiseGainX0;

                                        tnoiseX1 += tnoiseGainX1;
                                        thNoiseX1 += thNoiseGainX1;
                                    }
                                }
                            }

                            tnoiseY0 += tnoiseGainY0;
                            tnoiseY1 += tnoiseGainY1;
                            tnoiseY2 += tnoiseGainY2;
                            tnoiseY3 += tnoiseGainY3;
                        }
                    }
                }
            }

            ushort ymax = 0;
            for (int i = 0; i < rainheightmap.Length; i++)
            {
                ymax = Math.Max(ymax, rainheightmap[i]);
            }
            chunks[0].MapChunk.YMax = ymax;
        }


        
        LerpedWeightedIndex2DMap GetOrLoadLerpedLandformMap(IMapChunk mapchunk, int regionX, int regionZ)
        {
            LerpedWeightedIndex2DMap map;
            // 1. Load?
            LandformMapByRegion.TryGetValue(regionZ * regionMapSize + regionX, out map);
            if (map != null) return map;

            IntDataMap2D lmap = mapchunk.MapRegion.LandformMap;
            // 2. Create
            map = LandformMapByRegion[regionZ * regionMapSize + regionX] 
                = new LerpedWeightedIndex2DMap(lmap.Data, lmap.Size, TerraGenConfig.landFormSmoothingRadius, lmap.TopLeftPadding, lmap.BottomRightPadding);

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
                    threshold += landforms.LandFormsByIndex[indices[i].Index].TerrainYThresholds[y] * indices[i].Weight;
                }

                values[y] = threshold;
            }
        }




        private void GetInterpolatedOctaves(WeightedIndex[] indices, out double[] amps, out double[] thresholds)
        {
            amps = new double[terrainGenOctaves];
            thresholds = new double[terrainGenOctaves];

            for (int octave = 0; octave < terrainGenOctaves; octave++)
            {
                double amplitude = 0;
                double threshold = 0;
                for (int i = 0; i < indices.Length; i++)
                {
                    LandformVariant l = landforms.LandFormsByIndex[indices[i].Index];
                    amplitude += l.TerrainOctaves[octave] * indices[i].Weight;
                    threshold += l.TerrainOctaveThresholds[octave] * indices[i].Weight;
                }

                amps[octave] = amplitude;
                thresholds[octave] = threshold;
            }    
        }

        
        double[] GetTerrainNoise3D(double[] octX0, double[] octX1, double[] octX2, double[] octX3, double[] octThX0, double[] octThX1, double[] octThX2, double[] octThX3, int xPos, int yPos, int zPos)
        {
            for (int x = 0; x < paddedNoiseWidth; x++)
            {
                for (int z = 0; z < paddedNoiseWidth; z++)
                {
                    for (int i = 0; i < terrainGenOctaves; i++)
                    {
                        lerpedAmps[i] = GameMath.BiLerp(octX0[i], octX1[i], octX2[i], octX3[i], (double)x / paddedNoiseWidth, (double)z / paddedNoiseWidth);
                        lerpedTh[i] = GameMath.BiLerp(octThX0[i], octThX1[i], octThX2[i], octThX3[i], (double)x / paddedNoiseWidth, (double)z / paddedNoiseWidth);
                    }

                    double nx = (xPos + x) / 100.0;
                    double nz = (zPos + z) / 100.0;
                    float distx = (float)distort2dx.Noise(nx, nz);
                    float distz = (float)distort2dz.Noise(nx, nz);

                    for (int y = 0; y < paddedNoiseHeight; y++)
                    {
                        noiseTemp[NoiseIndex3d(x, y, z)] = TerrainNoise.Noise(
                            (xPos + x) + (distx > 0 ? Math.Max(0, distx - 10) : Math.Min(0, distx + 10)),
                            (yPos + y) / (TerraGenConfig.terrainNoiseVerticalScale),
                            (zPos + z) + (distz > 0 ? Math.Max(0, distz - 10) : Math.Min(0, distz + 10)),
                            lerpedAmps,
                            lerpedTh
                        );
                    }
                }
            }

            return noiseTemp;
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
