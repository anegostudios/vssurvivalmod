using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.ServerMods.NoObf;

namespace Vintagestory.ServerMods
{
    public class GenTerra : ModStdWorldGen
    {
        ICoreServerAPI api;

        const double terrainDistortionMultiplier = 4.0;
        const double terrainDistortionThreshold = 40.0;
        const double geoDistortionMultiplier = 10.0;
        const double geoDistortionThreshold = 10.0;
        const double maxDistortionAmount = (55 + 40 + 30 + 10) * SimplexNoiseOctave.MAX_VALUE_2D_WARP;

        int maxThreads;

        LandformsWorldProperty landforms;
        Dictionary<int, LerpedWeightedIndex2DMap> LandformMapByRegion = new Dictionary<int, LerpedWeightedIndex2DMap>(10);
        int regionMapSize;
        float noiseScale;
        int terrainGenOctaves = 9;

        NewNormalizedSimplexFractalNoise terrainNoise;
        SimplexNoise distort2dx;
        SimplexNoise distort2dz;
        NormalizedSimplexNoise geoUpheavalNoise;
        WeightedTaper[] taperMap;

        struct ThreadLocalTempData
        {
            public double[] LerpedAmplitudes;
            public double[] LerpedThresholds;
            public WeightedIndex[] landformWeights;
        }
        ThreadLocal<ThreadLocalTempData> tempDataThreadLocal;

        struct WeightedTaper
        {
            public float TerrainYPos;
            public float Weight;
        }

        struct ColumnResult
        {
            public BitArray ColumnBlockSolidities;
            public int WaterBlockID;
        }
        ColumnResult[] columnResults;
        bool[] layerFullySolid;
        bool[] layerFullyEmpty;
        int[] borderIndicesByCardinal;

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
        }

        private void loadGamePre()
        {
            if (api.WorldManager.SaveGame.WorldType != "standard") return;

            TerraGenConfig.seaLevel = (int)(0.4313725490196078 * api.WorldManager.MapSizeY);
            api.WorldManager.SetSeaLevel(TerraGenConfig.seaLevel);
        }

        public void initWorldGen()
        {
            LoadGlobalConfig(api);
            LandformMapByRegion.Clear();

            /*
             *  For info in 1.19 there are 7 running server threads: the main server ticking thread, chunkdbthread, ServerConsole, Relight, CompressChunks, ai-pathfinding, CheckLeafDecay.  There may also be multi-threaded worldgen adding 1-3 more active threads.  Typically 4 threads (ticking, chunkdbthread, ai-pathfinding and at least 1 worldgen thread) will be almost constantly active if there is worldgen demand
                On a client, meanwhile, there will be at least the main rendering loop and the chunktesselator active.
             */
            maxThreads = Math.Clamp(Environment.ProcessorCount - (api.Server.IsDedicated ? 4 : 6), 1, api.Server.Config.HostedMode ? 4 : 10);  // We leave at least 4-6 threads free to avoid lag spikes due to CPU unavailability

            chunksize = api.WorldManager.ChunkSize;
            regionMapSize = (int)Math.Ceiling((double)api.WorldManager.MapSizeX / api.WorldManager.RegionSize);
            noiseScale = Math.Max(1, api.WorldManager.MapSizeY / 256f);
            terrainGenOctaves = TerraGenConfig.GetTerrainOctaveCount(api.WorldManager.MapSizeY);

            terrainNoise = NewNormalizedSimplexFractalNoise.FromDefaultOctaves(
                terrainGenOctaves, 0.0005 * NewSimplexNoiseLayer.OldToNewFrequency / noiseScale, 0.9, api.WorldManager.Seed
            );
            distort2dx = new SimplexNoise(
                new double[] { 55, 40, 30, 10 },
                scaleAdjustedFreqs(new double[] { 1 / 5.0, 1 / 2.50, 1 / 1.250, 1 / 0.65 }, noiseScale),
                api.World.Seed + 9876 + 0
            );
            distort2dz = new SimplexNoise(
                new double[] { 55, 40, 30, 10 },
                scaleAdjustedFreqs(new double[] { 1 / 5.0, 1 / 2.50, 1 / 1.250, 1 / 0.65 }, noiseScale),
                api.World.Seed + 9876 + 2
            );
            geoUpheavalNoise = new NormalizedSimplexNoise(
                new double[] { 55, 40, 30, 15, 7, 4 },
                scaleAdjustedFreqs(new double[] {
                    1.0 / 5.5,
                    1.1 / 2.75,
                    1.2 / 1.375,
                    1.2 / 0.715,
                    1.2 / 0.45,
                    1.2 / 0.25
                }, noiseScale),
                api.World.Seed + 9876 + 1
            );

            tempDataThreadLocal = new ThreadLocal<ThreadLocalTempData>(() => new ThreadLocalTempData
            {
                LerpedAmplitudes = new double[terrainGenOctaves],
                LerpedThresholds = new double[terrainGenOctaves],
                landformWeights = new WeightedIndex[NoiseLandforms.landforms.LandFormsByIndex.Length]
            });
            columnResults = new ColumnResult[chunksize * chunksize];
            layerFullyEmpty = new bool[api.WorldManager.MapSizeY];
            layerFullySolid = new bool[api.WorldManager.MapSizeY];
            taperMap = new WeightedTaper[chunksize * chunksize];
            for (int i = 0; i < chunksize * chunksize; i++) columnResults[i].ColumnBlockSolidities = new BitArray(api.WorldManager.MapSizeY);

            borderIndicesByCardinal = new int[8];
            borderIndicesByCardinal[Cardinal.NorthEast.Index] = (chunksize - 1) * chunksize + 0;
            borderIndicesByCardinal[Cardinal.SouthEast.Index] = 0 + 0;
            borderIndicesByCardinal[Cardinal.SouthWest.Index] = 0 + chunksize - 1;
            borderIndicesByCardinal[Cardinal.NorthWest.Index] = (chunksize - 1) * chunksize + chunksize - 1;
        }

        private double[] scaleAdjustedFreqs(double[] vs, float horizontalScale)
        {
            for (int i = 0; i < vs.Length; i++)
            {
                vs[i] /= horizontalScale;
            }

            return vs;
        }


       

        private void OnChunkColumnGen(IChunkColumnGenerateRequest request)
        {   
            if (request.RequiresChunkBorderSmoothing)
            {
                var neibHeightMaps = request.NeighbourTerrainHeight;

                // Ignore diagonals if direct adjacent faces are available, otherwise the corners get weighted too strongly
                if (neibHeightMaps[Cardinal.North.Index] != null)
                {
                    neibHeightMaps[Cardinal.NorthEast.Index] = null;
                    neibHeightMaps[Cardinal.NorthWest.Index] = null;
                }
                if (neibHeightMaps[Cardinal.East.Index] != null)
                {
                    neibHeightMaps[Cardinal.NorthEast.Index] = null;
                    neibHeightMaps[Cardinal.SouthEast.Index] = null;
                }
                if (neibHeightMaps[Cardinal.South.Index] != null)
                {
                    neibHeightMaps[Cardinal.SouthWest.Index] = null;
                    neibHeightMaps[Cardinal.SouthEast.Index] = null;
                }
                if (neibHeightMaps[Cardinal.West.Index] != null)
                {
                    neibHeightMaps[Cardinal.SouthWest.Index] = null;
                    neibHeightMaps[Cardinal.NorthWest.Index] = null;
                }

                //Bitmap bmp = new Bitmap(32, 32);

                string sides = "";
                for (int i = 0; i < Cardinal.ALL.Length; i++)
                {
                    var neibMap = neibHeightMaps[i];
                    if (neibMap == null) continue;

                    sides += Cardinal.ALL[i].Code + "_";
                }

                for (int dx = 0; dx < chunksize; dx++)
                {
                    borderIndicesByCardinal[Cardinal.North.Index] = (chunksize - 1) * chunksize + dx;
                    borderIndicesByCardinal[Cardinal.South.Index] = 0 + dx;

                    for (int dz = 0; dz < chunksize; dz++)
                    {
                        double sumWeight = 0;
                        double ypos = 0;
                        float maxWeight = 0;

                        borderIndicesByCardinal[Cardinal.East.Index] = dz * chunksize + 0;
                        borderIndicesByCardinal[Cardinal.West.Index] = dz * chunksize + chunksize - 1;

                        for (int i = 0; i < Cardinal.ALL.Length; i++)
                        {
                            var neibMap = neibHeightMaps[i];
                            if (neibMap == null) continue;

                            float distToEdge=0;

                            switch (i)
                            {
                                case 0: // N: Negative Z
                                    distToEdge = (float)dz / chunksize;
                                    break;
                                case 1: // NE: Positive X, negative Z
                                    distToEdge = (1 - (float)dx / chunksize) + (float)dz / chunksize;
                                    break;
                                case 2: // E: Positive X
                                    distToEdge = 1 - (float)dx / chunksize;
                                    break;
                                case 3: // SE: Positive X, positive Z
                                    distToEdge = (1 - (float)dx / chunksize) + (1 - (float)dz / chunksize);
                                    break;
                                case 4: // S: Positive Z
                                    distToEdge = 1 - (float)dz / chunksize;
                                    break;
                                case 5: // SW: Negative X, positive Z
                                    distToEdge = (float)dx / chunksize + 1 - (float)dz / chunksize;
                                    break;
                                case 6: // W: Negative X
                                    distToEdge = (float)dx / chunksize;
                                    break;
                                case 7: // Negative X, negative Z
                                    distToEdge = (float)dx / chunksize + (float)dz / chunksize;
                                    break;
                            }

                            
                            float cardinalWeight = (float)Math.Pow((float)(1 - GameMath.Clamp(distToEdge, 0, 1)), 2);
                            var neibYPos = neibMap[borderIndicesByCardinal[i]] + 0.5f;

                            ypos += neibYPos * Math.Max(0.0001, cardinalWeight);
                            sumWeight += cardinalWeight;
                            maxWeight = Math.Max(maxWeight, cardinalWeight);
                        }

                        taperMap[dz * chunksize + dx] = new WeightedTaper() { TerrainYPos = (float)(ypos / Math.Max(0.0001, sumWeight)), Weight = maxWeight };

                        /// East: Positive X
                        /// South: Positive Z
                        //bmp.SetPixel(dx, dz, Color.FromArgb(255, (int)(maxWeight * 255), 0, 0));
                    }
                }

                // bmp.Save("chunk-" + request.ChunkX + "-" + request.ChunkZ + "-"+sides+".png");
            }


            generate(request.Chunks, request.ChunkX, request.ChunkZ, request.RequiresChunkBorderSmoothing);
        }

        private void generate(IServerChunk[] chunks, int chunkX, int chunkZ, bool requiresChunkBorderSmoothing)
        {
            landforms = NoiseLandforms.landforms;
            IMapChunk mapchunk = chunks[0].MapChunk;
            const int chunksize = GlobalConstants.ChunkSize;

            int climateUpLeft;
            int climateUpRight;
            int climateBotLeft;
            int climateBotRight;

            int upheavalMapUpLeft = 0;
            int upheavalMapUpRight = 0;
            int upheavalMapBotLeft = 0;
            int upheavalMapBotRight = 0;

            IntDataMap2D climateMap = chunks[0].MapChunk.MapRegion.ClimateMap;
            IntDataMap2D oceanMap = chunks[0].MapChunk.MapRegion.OceanMap;
            int regionChunkSize = api.WorldManager.RegionSize / chunksize;
            float cfac = (float)climateMap.InnerSize / regionChunkSize;
            
            int rlX = chunkX % regionChunkSize;
            int rlZ = chunkZ % regionChunkSize;

            climateUpLeft = climateMap.GetUnpaddedInt((int)(rlX * cfac), (int)(rlZ * cfac));
            climateUpRight = climateMap.GetUnpaddedInt((int)(rlX * cfac + cfac), (int)(rlZ * cfac));
            climateBotLeft = climateMap.GetUnpaddedInt((int)(rlX * cfac), (int)(rlZ * cfac + cfac));
            climateBotRight = climateMap.GetUnpaddedInt((int)(rlX * cfac + cfac), (int)(rlZ * cfac + cfac));

            int oceanUpLeft=0;
            int oceanUpRight=0;
            int oceanBotLeft=0;
            int oceanBotRight=0;
            if (oceanMap != null && oceanMap.Data.Length > 0)
            {
                float ofac = (float)oceanMap.InnerSize / regionChunkSize;
                oceanUpLeft = oceanMap.GetUnpaddedInt((int)(rlX * ofac), (int)(rlZ * ofac));
                oceanUpRight = oceanMap.GetUnpaddedInt((int)(rlX * ofac + ofac), (int)(rlZ * ofac));
                oceanBotLeft = oceanMap.GetUnpaddedInt((int)(rlX * ofac), (int)(rlZ * ofac + ofac));
                oceanBotRight = oceanMap.GetUnpaddedInt((int)(rlX * ofac + ofac), (int)(rlZ * ofac + ofac));
            }

            IntDataMap2D upheavalMap = chunks[0].MapChunk.MapRegion.UpheavelMap;
            if (upheavalMap != null)
            {
                float ufac = (float)upheavalMap.InnerSize / regionChunkSize;
                upheavalMapUpLeft = upheavalMap.GetUnpaddedInt((int)(rlX * ufac), (int)(rlZ * ufac));
                upheavalMapUpRight = upheavalMap.GetUnpaddedInt((int)(rlX * ufac + ufac), (int)(rlZ * ufac));
                upheavalMapBotLeft = upheavalMap.GetUnpaddedInt((int)(rlX * ufac), (int)(rlZ * ufac + ufac));
                upheavalMapBotRight = upheavalMap.GetUnpaddedInt((int)(rlX * ufac + ufac), (int)(rlZ * ufac + ufac));
            }


            int rockID = GlobalConfig.defaultRockId;
            float oceanicityFac = api.WorldManager.MapSizeY / 256 * 0.33333f; // At a mapheight of 255, submerge land by up to 85 blocks

            IntDataMap2D landformMap = mapchunk.MapRegion.LandformMap;
            // # of pixels for each chunk (probably 1, 2, or 4) in the land form map
            float chunkPixelSize = landformMap.InnerSize / regionChunkSize;
            // Start coordinates for the chunk in the region map
            float baseX = (chunkX % regionChunkSize) * chunkPixelSize;
            float baseZ = (chunkZ % regionChunkSize) * chunkPixelSize;

            LerpedWeightedIndex2DMap landLerpMap = GetOrLoadLerpedLandformMap(chunks[0].MapChunk, chunkX / regionChunkSize, chunkZ / regionChunkSize);

            // Terrain octaves
            double[] octNoiseX0, octNoiseX1, octNoiseX2, octNoiseX3;
            double[] octThX0, octThX1, octThX2, octThX3;

            WeightedIndex[] landformWeights = tempDataThreadLocal.Value.landformWeights;
            GetInterpolatedOctaves(landLerpMap.WeightsAt(baseX, baseZ, landformWeights), out octNoiseX0, out octThX0);
            GetInterpolatedOctaves(landLerpMap.WeightsAt(baseX + chunkPixelSize, baseZ, landformWeights), out octNoiseX1, out octThX1);
            GetInterpolatedOctaves(landLerpMap.WeightsAt(baseX, baseZ + chunkPixelSize, landformWeights), out octNoiseX2, out octThX2);
            GetInterpolatedOctaves(landLerpMap.WeightsAt(baseX + chunkPixelSize, baseZ + chunkPixelSize, landformWeights), out octNoiseX3, out octThX3);

            // Store heightmap in the map chunk
            ushort[] rainheightmap = chunks[0].MapChunk.RainHeightMap;
            ushort[] terrainheightmap = chunks[0].MapChunk.WorldGenTerrainHeightMap;

            int mapsizeY = api.WorldManager.MapSizeY;
            int mapsizeYm2 = api.WorldManager.MapSizeY - 2;
            int taperThreshold = (int)(mapsizeY * 0.9f);
            double geoUpheavalAmplitude = 255;

            //int cblockId = api.World.GetBlock(new AssetLocation("creativeblock-35")).Id;

            float chunkBlockDelta = 1.0f / chunksize;
            float chunkPixelBlockStep = chunkPixelSize * chunkBlockDelta;
            double verticalNoiseRelativeFrequency = 0.5 / TerraGenConfig.terrainNoiseVerticalScale;
            for (int y = 0; y < layerFullySolid.Length; y++) layerFullySolid[y] = true;   // Fill with true; later if any block in the layer is non-solid we will set it to false
            for (int y = 0; y < layerFullyEmpty.Length; y++) layerFullyEmpty[y] = true;   // Fill with true; later if any block in the layer is non-solid we will set it to false
            layerFullyEmpty[mapsizeY - 1] = false;  // The top block is always empty (air), leaving space for grass, snowlayer etc.
            var landFormsByIndex = landforms.LandFormsByIndex;

            Parallel.For(0, chunksize * chunksize, new ParallelOptions() { MaxDegreeOfParallelism = maxThreads }, chunkIndex2d => {
                int lX = chunkIndex2d % chunksize;
                int lZ = chunkIndex2d / chunksize;
                int worldX = chunkX * chunksize + lX;
                int worldZ = chunkZ * chunksize + lZ;
                BitArray columnBlockSolidities = columnResults[chunkIndex2d].ColumnBlockSolidities;
                double[] lerpedAmps = tempDataThreadLocal.Value.LerpedAmplitudes;
                double[] lerpedTh = tempDataThreadLocal.Value.LerpedThresholds;

                // calculating these 1024 times is very costly: landLerpMap places on the heap 2 new Dictionary, 1 new SortedDictionary, and 3 new WeightedIndex[]
                WeightedIndex[] columnLandformIndexedWeights = tempDataThreadLocal.Value.landformWeights;
                landLerpMap.WeightsAt(baseX + lX * chunkPixelBlockStep, baseZ + lZ * chunkPixelBlockStep, columnLandformIndexedWeights);
                for (int i = 0; i < terrainGenOctaves; i++)
                {
                    lerpedAmps[i] = GameMath.BiLerp(octNoiseX0[i], octNoiseX1[i], octNoiseX2[i], octNoiseX3[i], lX * chunkBlockDelta, lZ * chunkBlockDelta);
                    lerpedTh[i] = GameMath.BiLerp(octThX0[i], octThX1[i], octThX2[i], octThX3[i], lX * chunkBlockDelta, lZ * chunkBlockDelta);
                }

                // Create that directional compression effect.
                VectorXZ dist = NewDistortionNoise(worldX, worldZ);
                VectorXZ distTerrain = ApplyIsotropicDistortionThreshold(dist * terrainDistortionMultiplier, terrainDistortionThreshold,
                    terrainDistortionMultiplier * maxDistortionAmount);
                VectorXZ distGeo = ApplyIsotropicDistortionThreshold(dist * geoDistortionMultiplier, geoDistortionThreshold,
                    geoDistortionMultiplier * maxDistortionAmount);

                // Get Y distortion from oceanicity and upheaval
                float upHeavalStrength = GameMath.BiLerp(upheavalMapUpLeft, upheavalMapUpRight, upheavalMapBotLeft, upheavalMapBotRight, lX * chunkBlockDelta, lZ * chunkBlockDelta);
                float oceanicity = GameMath.BiLerp(oceanUpLeft, oceanUpRight, oceanBotLeft, oceanBotRight, lX * chunkBlockDelta, lZ * chunkBlockDelta) * oceanicityFac;
                float distY = oceanicity + ComputeOceanAndUpheavalDistY(upHeavalStrength, worldX, worldZ, distGeo);

                columnResults[chunkIndex2d].WaterBlockID = oceanicity > 1 ? GlobalConfig.saltWaterBlockId : GlobalConfig.waterBlockId;

                /*if (Math.Abs(distY) > 1)
                {
                    int chunkIndex = ChunkIndex3d(lX, 250 % 32, lZ);
                    chunks[250 / 32].Data[chunkIndex] = cblockId;
                }*/

                // Prepare the noise for the entire column.
                NewNormalizedSimplexFractalNoise.ColumnNoise columnNoise = terrainNoise.ForColumn(verticalNoiseRelativeFrequency, lerpedAmps, lerpedTh, worldX + distTerrain.X, worldZ + distTerrain.Z);

                WeightedTaper wtaper = taperMap[chunkIndex2d];

                for (int posY = 1; posY < mapsizeY - 1; posY++)
                {
                    // Setup a lerp between threshold values, so that distortY can be applied continuously there.
                    StartSampleDisplacedYThreshold(posY + distY, mapsizeYm2, out int distortedPosYBase, out float distortedPosYSlide);

                    // Value starts as the landform Y threshold.
                    double threshold = 0;
                    for (int i = 0; i < columnLandformIndexedWeights.Length; i++)
                    {
                        float weight = columnLandformIndexedWeights[i].Weight;
                        if (weight == 0) continue;
                        // Sample the two values to lerp between. The value of distortedPosYBase is clamped in such a way that this always works.
                        // Underflow and overflow of distortedPosY result in linear extrapolation.
                        float[] thresholds = landFormsByIndex[columnLandformIndexedWeights[i].Index].TerrainYThresholds;
                        float thresholdValue = ContinueSampleDisplacedYThreshold(distortedPosYBase, distortedPosYSlide, thresholds);
                        threshold += thresholdValue * weight;
                    }

                    // Geo Upheaval modifier for threshold
                    double geoUpheavalTaper = ComputeGeoUpheavalTaper(posY, distY, taperThreshold, geoUpheavalAmplitude, mapsizeY);
                    threshold += geoUpheavalTaper;

                    if (requiresChunkBorderSmoothing)
                    {
                        double th = posY > wtaper.TerrainYPos ? 1 : -1;

                        var ydiff = Math.Abs(posY - wtaper.TerrainYPos);
                        var noise = ydiff > 10 ? 0 : distort2dx.Noise(-(chunkX * chunksize + lX) / 10.0, posY / 10.0, -(chunkZ * chunksize + lZ) / 10.0) / Math.Max(1, ydiff / 2.0);

                        noise *= GameMath.Clamp(2*(1 - wtaper.Weight), 0, 1) * 0.1;

                        threshold = GameMath.Lerp(threshold, th + noise, wtaper.Weight);
                    }

                    // Often we don't need to calculate the noise.
                    // First case also catches NaN if it were to ever happen.
                    double noiseSign;
                    if (!(threshold < columnNoise.BoundMax)) noiseSign = double.NegativeInfinity;
                    else if (threshold <= columnNoise.BoundMin) noiseSign = double.PositiveInfinity;

                    // But sometimes we do.
                    else
                    {
                        noiseSign = -NormalizedSimplexNoise.NoiseValueCurveInverse(threshold);
                        noiseSign = columnNoise.NoiseSign(posY, noiseSign);

                        // If it ever comes up to change the noise formula to one that's less trivial to layer-skip-optimize,
                        // Replace the above-two lines with the one below.
                        //noiseSign = columnNoise.Noise(posY) - threshold;
                    }

                    if (noiseSign > 0)  // solid
                    {
                        columnBlockSolidities[posY] = true;
                        layerFullyEmpty[posY] = false;  // thread safe even when this is parallel
                    }
                    else
                    {
                        columnBlockSolidities[posY] = false;
                        layerFullySolid[posY] = false;  // thread safe even when this is parallel
                    }
                }
            });

            IChunkBlocks chunkBlockData = chunks[0].Data;

            // First set all the fully solid layers in bulk, as much as possible
            chunkBlockData.SetBlockBulk(0, chunksize, chunksize, GlobalConfig.mantleBlockId);
            int yBase = 1;
            for (; yBase < mapsizeY - 1; yBase++)
            {
                if (layerFullySolid[yBase])
                {
                    if (yBase % chunksize == 0)
                    {
                        chunkBlockData = chunks[yBase / chunksize].Data;
                    }

                    chunkBlockData.SetBlockBulk((yBase % chunksize) * chunksize * chunksize, chunksize, chunksize, rockID);
                }
                else break;
            }

            // Now figure out the top of the mixed layers (above yTop we have fully empty layers, i.e. air)
            int seaLevel = TerraGenConfig.seaLevel;
            int surfaceWaterId = 0;
            int yTop = mapsizeY - 2;  // yTop never more than (mapSizey - 1), but leave the top block layer on the map always as air / for grass
            while (yTop >= yBase && layerFullyEmpty[yTop]) yTop--;  // Decrease yTop, we don't need to generate anything for fully empty (air layers)
            if (yTop < seaLevel) yTop = seaLevel;
            yTop++;  // Add back one because this is going to be the loop until limit

            // Then for the rest place blocks column by column (from yBase to yTop only; outside that range layers were already placed below, or are fully air above)
            for (int lZ = 0; lZ < chunksize; lZ++)
            {
                int worldZ = chunkZ * chunksize + lZ;
                int mapIndex = ChunkIndex2d(0, lZ);
                for (int lX = 0; lX < chunksize; lX++)
                {
                    ColumnResult columnResult = columnResults[mapIndex];
                    int waterID = columnResult.WaterBlockID;

                    if (yBase < seaLevel && waterID != GlobalConfig.saltWaterBlockId)     // Finding the surface water / ice id, relevant only for fresh water and only if there is a non-solid block in the column below sea-level
                    {
                        int temp = (GameMath.BiLerpRgbColor(lX * chunkBlockDelta, lZ * chunkBlockDelta, climateUpLeft, climateUpRight, climateBotLeft, climateBotRight) >> 16) & 0xFF;
                        float distort = (float)distort2dx.Noise(chunkX * chunksize + lX, worldZ) / 20f;
                        float tempf = TerraGenConfig.GetScaledAdjustedTemperatureFloat(temp, 0) + distort;
                        surfaceWaterId = (tempf < TerraGenConfig.WaterFreezingTempOnGen) ? GlobalConfig.lakeIceBlockId : waterID;
                    }

                    terrainheightmap[mapIndex] = (ushort)(yBase - 1);   // Initially set the heightmaps to values reflecting the top of the fully solid layers
                    rainheightmap[mapIndex] = (ushort)(yBase - 1);

                    chunkBlockData = chunks[yBase / chunksize].Data;
                    for (int posY = yBase; posY < yTop; posY++)
                    {
                        int lY = posY % chunksize;
                        int chunkIndex = ChunkIndex3d(lX, lY, lZ);

                        if (columnResult.ColumnBlockSolidities[posY])  // if isSolid
                        {
                            terrainheightmap[mapIndex] = (ushort)posY;
                            rainheightmap[mapIndex] = (ushort)posY;
                            chunkBlockData[chunkIndex] = rockID;
                        }
                        else if (posY < seaLevel)
                        {
                            int blockId;
                            if (posY == seaLevel - 1)
                            {
                                rainheightmap[mapIndex] = (ushort)posY;   // We only need to set the rainheightmap on the top block
                                blockId = surfaceWaterId;
                            }
                            else
                            {
                                blockId = waterID;
                            }

                            chunkBlockData.SetFluid(chunkIndex, blockId);
                        }

                        if (lY == chunksize - 1)
                        {
                            chunkBlockData = chunks[(posY + 1) / chunksize].Data;  // Set up the next chunksBlockData value
                        }
                    }

                    mapIndex++;
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
                    float weight = indices[i].Weight;
                    if (weight == 0) continue;
                    LandformVariant l = landforms.LandFormsByIndex[indices[i].Index];
                    amplitude += l.TerrainOctaves[octave] * weight;
                    threshold += l.TerrainOctaveThresholds[octave] * weight;
                }

                amps[octave] = amplitude;
                thresholds[octave] = threshold;
            }
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void StartSampleDisplacedYThreshold(float distortedPosY, int mapSizeYm2, out int yBase, out float ySlide)
        {
            int distortedPosYBase = (int)Math.Floor(distortedPosY);
            yBase = GameMath.Clamp(distortedPosYBase, 0, mapSizeYm2);
            ySlide = distortedPosY - distortedPosYBase;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        float ContinueSampleDisplacedYThreshold(int yBase, float ySlide, float[] thresholds)
        {
            return GameMath.Lerp(thresholds[yBase], thresholds[yBase + 1], ySlide);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        float ComputeOceanAndUpheavalDistY(float upheavalStrength, double worldX, double worldZ, VectorXZ distGeo)
        {
            float upheavalNoiseValue = (float)geoUpheavalNoise.Noise((worldX + distGeo.X) / 400.0, (worldZ + distGeo.Z) / 400.0) * 0.9f;
            float upheavalMultiplier = Math.Min(0, 0.5f - upheavalNoiseValue);
            return upheavalStrength * upheavalMultiplier;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        double ComputeGeoUpheavalTaper(double posY, double distY, double taperThreshold, double geoUpheavalAmplitude, double mapSizeY)
        {
            const double AMPLITUDE_MODIFIER = 40.0;
            if (posY > taperThreshold && distY < -2)
            {
                double upheavalAmount = GameMath.Clamp(-distY, posY - mapSizeY, posY);
                double ceilingDelta = posY - taperThreshold;
                return ceilingDelta * upheavalAmount / (AMPLITUDE_MODIFIER * geoUpheavalAmplitude);
            }
            return 0;
        }

        // Closesly matches the old two-noise distortion in a given seed, but is more fair to all angles.
        VectorXZ NewDistortionNoise(double worldX, double worldZ)
        {
            double noiseX = worldX / 400.0;
            double noiseZ = worldZ / 400.0;
            SimplexNoise.NoiseFairWarpVector(distort2dx, distort2dz, noiseX, noiseZ, out double distX, out double distZ);
            return new VectorXZ { X = distX, Z = distZ };
        }

        // Cuts off the distortion in a circle rather than a square.
        // Between this and the new distortion noise, this makes the bigger difference.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        VectorXZ ApplyIsotropicDistortionThreshold(VectorXZ dist, double threshold, double maximum)
        {
            double distMagnitudeSquared = dist.X * dist.X + dist.Z * dist.Z;
            double thresholdSquared = threshold * threshold;
            if (distMagnitudeSquared <= thresholdSquared) dist.X = dist.Z = 0;
            else
            {
                // `slide` is 0 to 1 between `threshold` and `maximum` (input vector magnitude)
                double baseCurve = (distMagnitudeSquared - thresholdSquared) / distMagnitudeSquared;
                double maximumSquared = maximum * maximum;
                double baseCurveReciprocalAtMaximum = maximumSquared / (maximumSquared - thresholdSquared);
                double slide = baseCurve * baseCurveReciprocalAtMaximum;

                // Let  `slide` be smooth to start.
                slide *= slide;

                // `forceDown` needs to make `dist` zero at `threshold`
                // and `expectedOutputMaximum` at `maximum`.
                double expectedOutputMaximum = maximum - threshold;
                double forceDown = slide * (expectedOutputMaximum / maximum);

                dist *= forceDown;
            }
            return dist;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int ChunkIndex3d(int x, int y, int z)
        {
            return (y * chunksize + z) * chunksize + x;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int ChunkIndex2d(int x, int z)
        {
            return z * chunksize + x;
        }

        struct VectorXZ
        {
            public double X, Z;
            public static VectorXZ operator *(VectorXZ a, double b) => new VectorXZ { X = a.X * b, Z = a.Z * b };
        }
    }
}
