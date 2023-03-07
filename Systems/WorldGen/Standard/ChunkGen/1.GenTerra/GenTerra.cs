using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
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

        const double terrainDistortionMultiplier = 4.0;
        const double terrainDistortionThreshold = 40.0;
        const double geoDistortionMultiplier = 10.0;
        const double geoDistortionThreshold = 10.0;
        const double maxDistortionAmount = (55 + 40 + 30 + 10) * SimplexNoiseOctave.MAX_VALUE_2D_WARP;

        LandformsWorldProperty landforms;
        Dictionary<int, LerpedWeightedIndex2DMap> LandformMapByRegion = new Dictionary<int, LerpedWeightedIndex2DMap>(10);
        int regionMapSize;
        float noiseScale;
        int terrainGenOctaves = 9;

        float oceanicityStrengthInv;

        NormalizedSimplexNoise terrainNoise;
        SimplexNoise distort2dx;
        SimplexNoise distort2dz;
        NormalizedSimplexNoise geoUpheavalNoise;

        double[] lerpedAmps;
        double[] lerpedTh;

        // Used only by GridTrilerp mode
        const int lerpHor = TerraGenConfig.lerpHorizontal;
        const int lerpVer = TerraGenConfig.lerpVertical;
        const float lerpDeltaHor = 1f / lerpHor;
        const float lerpDeltaVert = 1f / lerpVer;
        int noiseWidth;
        int noiseHeight;
        int paddedNoiseWidth;
        int paddedNoiseHeight;
        double[] noiseTemp;
        float[] distY;


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
            ITreeAttribute worldConfig = api.WorldManager.SaveGame.WorldConfiguration;
            float str = worldConfig.GetString("oceanessStrength", "0").ToFloat(0);

            // oceanicityStrengthInv = 0       => everywhere ocean
            // oceanicityStrengthInv = 0.65    => common oceans
            // oceanicityStrengthInv = 0.85    => rare
            // oceanicityStrengthInv = 1       => no oceans
            oceanicityStrengthInv = GameMath.Clamp(1 - str, 0, 1);
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

            chunksize = api.WorldManager.ChunkSize;
            regionMapSize = (int)Math.Ceiling((double)api.WorldManager.MapSizeX / api.WorldManager.RegionSize);
            noiseScale = Math.Max(1, api.WorldManager.MapSizeY / 256f);
            terrainGenOctaves = TerraGenConfig.GetTerrainOctaveCount(api.WorldManager.MapSizeY);

            lerpedAmps = new double[terrainGenOctaves];
            lerpedTh = new double[terrainGenOctaves];


            terrainNoise = NormalizedSimplexNoise.FromDefaultOctaves(
                terrainGenOctaves, 0.0005 / noiseScale, 0.9, api.WorldManager.Seed
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

            int upheavalMapUpLeft = 0;
            int upheavalMapUpRight = 0;
            int upheavalMapBotLeft = 0;
            int upheavalMapBotRight = 0;

            IntDataMap2D climateMap = chunks[0].MapChunk.MapRegion.ClimateMap;
            IntDataMap2D oceanMap = chunks[0].MapChunk.MapRegion.OceanMap;
            int regionChunkSize = api.WorldManager.RegionSize / chunksize;
            float cfac = (float)climateMap.InnerSize / regionChunkSize;
            float ofac = (float)oceanMap.InnerSize / regionChunkSize;
            int rlX = chunkX % regionChunkSize;
            int rlZ = chunkZ % regionChunkSize;

            climateUpLeft = climateMap.GetUnpaddedInt((int)(rlX * cfac), (int)(rlZ * cfac));
            climateUpRight = climateMap.GetUnpaddedInt((int)(rlX * cfac + cfac), (int)(rlZ * cfac));
            climateBotLeft = climateMap.GetUnpaddedInt((int)(rlX * cfac), (int)(rlZ * cfac + cfac));
            climateBotRight = climateMap.GetUnpaddedInt((int)(rlX * cfac + cfac), (int)(rlZ * cfac + cfac));

            int oceanUpLeft = oceanMap.GetUnpaddedInt((int)(rlX * ofac), (int)(rlZ * ofac));
            int oceanUpRight = oceanMap.GetUnpaddedInt((int)(rlX * ofac + ofac), (int)(rlZ * ofac));
            int oceanBotLeft = oceanMap.GetUnpaddedInt((int)(rlX * ofac), (int)(rlZ * ofac + ofac));
            int oceanBotRight = oceanMap.GetUnpaddedInt((int)(rlX * ofac + ofac), (int)(rlZ * ofac + ofac));


            IntDataMap2D upheavalMap = chunks[0].MapChunk.MapRegion.UpheavelMap;
            if (upheavalMap != null)
            {
                float ufac = (float)upheavalMap.InnerSize / regionChunkSize;
                upheavalMapUpLeft = upheavalMap.GetUnpaddedInt((int)(rlX * ufac), (int)(rlZ * ufac));
                upheavalMapUpRight = upheavalMap.GetUnpaddedInt((int)(rlX * ufac + ufac), (int)(rlZ * ufac));
                upheavalMapBotLeft = upheavalMap.GetUnpaddedInt((int)(rlX * ufac), (int)(rlZ * ufac + ufac));
                upheavalMapBotRight = upheavalMap.GetUnpaddedInt((int)(rlX * ufac + ufac), (int)(rlZ * ufac + ufac));
            }

            int waterID = GlobalConfig.waterBlockId;
            int rockID = GlobalConfig.defaultRockId;

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

            GetInterpolatedOctaves(landLerpMap[baseX, baseZ], out octNoiseX0, out octThX0);
            GetInterpolatedOctaves(landLerpMap[baseX + chunkPixelSize, baseZ], out octNoiseX1, out octThX1);
            GetInterpolatedOctaves(landLerpMap[baseX, baseZ + chunkPixelSize], out octNoiseX2, out octThX2);
            GetInterpolatedOctaves(landLerpMap[baseX + chunkPixelSize, baseZ + chunkPixelSize], out octNoiseX3, out octThX3);

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

            for (int lZ = 0; lZ < chunksize; lZ++)
            {
                int worldZ = chunkZ * chunksize + lZ;
                for (int lX = 0; lX < chunksize; lX++)
                {
                    int worldX = chunkX * chunksize + lX;

                    WeightedIndex[] columnWeightedIndices = landLerpMap[baseX + lX * chunkPixelBlockStep, baseZ + lZ * chunkPixelBlockStep];
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
                    float oceanicity = GameMath.BiLerp(oceanUpLeft, oceanUpRight, oceanBotLeft, oceanBotRight, lX * chunkBlockDelta, lZ * chunkBlockDelta) / 2f;
                    float distY = oceanicity + ComputeOceanAndUpheavalDistY(upHeavalStrength, worldX, worldZ, distGeo);

                    /*if (Math.Abs(distY) > 10)
                    {
                        int chunkIndex = ChunkIndex3d(lX, 250 % 32, lZ);
                        chunks[250 / 32].Data[chunkIndex] = cblockId;
                    }*/

                    // Prepare the noise for the entire column.
                    NormalizedSimplexNoise.ColumnNoise columnNoise = terrainNoise.ForColumn(verticalNoiseRelativeFrequency, lerpedAmps, lerpedTh, worldX + distTerrain.X, worldZ + distTerrain.Z);

                    int chunkY = 0;
                    int lY = 1;
                    IChunkBlocks chunkBlockData = chunks[chunkY].Data;
                    chunks[0].Data[ChunkIndex3d(lX, 0, lZ)] = GlobalConfig.mantleBlockId;
                    for (int posY = 1; posY < mapsizeY - 1; posY++)
                    {
                        // Setup a lerp between threshold values, so that distortY can be applied continuously there.
                        StartSampleDisplacedYThreshold(posY + distY, mapsizeYm2, out int distortedPosYBase, out float distortedPosYSlide);

                        // Value starts as the landform Y threshold.
                        double threshold = 0;
                        for (int i = 0; i < columnWeightedIndices.Length; i++)
                        {
                            // Sample the two values to lerp between. The value of distortedPosYBase is clamped in such a way that this always works.
                            // Underflow and overflow of distortedPosY result in linear extrapolation.
                            float[] thresholds = landforms.LandFormsByIndex[columnWeightedIndices[i].Index].TerrainYThresholds;
                            float thresholdValue = ContinueSampleDisplacedYThreshold(distortedPosYBase, distortedPosYSlide, thresholds);
                            threshold += thresholdValue * columnWeightedIndices[i].Weight;
                        }

                        // Geo Upheaval modifier for threshold
                        double geoUpheavalTaper = ComputeGeoUpheavalTaper(posY, distY, taperThreshold, geoUpheavalAmplitude, mapsizeY);
                        threshold += geoUpheavalTaper;

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

                        int mapIndex = ChunkIndex2d(lX, lZ);
                        int chunkIndex = ChunkIndex3d(lX, lY, lZ);

                        if (noiseSign > 0)
                        {
                            terrainheightmap[mapIndex] = (ushort)posY;
                            rainheightmap[mapIndex] = (ushort)posY;
                            chunkBlockData[chunkIndex] = rockID;
                        }
                        else if (posY < TerraGenConfig.seaLevel)
                        {
                            rainheightmap[mapIndex] = (ushort)posY;

                            int blockId;
                            if (posY == TerraGenConfig.seaLevel - 1)
                            {
                                int temp = (GameMath.BiLerpRgbColor(lX * chunkBlockDelta, lZ * chunkBlockDelta, climateUpLeft, climateUpRight, climateBotLeft, climateBotRight) >> 16) & 0xFF;
                                float distort = (float)distort2dx.Noise(worldX, worldZ) / 20f;
                                float tempf = TerraGenConfig.GetScaledAdjustedTemperatureFloat(temp, 0) + distort;
                                blockId = (tempf < TerraGenConfig.WaterFreezingTempOnGen) ? GlobalConfig.lakeIceBlockId : waterID;
                            }
                            else
                            {
                                blockId = waterID;
                            }

                            chunkBlockData.SetFluid(chunkIndex, blockId);
                        }

                        lY++;
                        if (lY == chunksize)
                        {
                            lY = 0;
                            chunkY++;
                            chunkBlockData = chunks[chunkY].Data;
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
                return (ceilingDelta * upheavalAmount) / (AMPLITUDE_MODIFIER * geoUpheavalAmplitude);
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
