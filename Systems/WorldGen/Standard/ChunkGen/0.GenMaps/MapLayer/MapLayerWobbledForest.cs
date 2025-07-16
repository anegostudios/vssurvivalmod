﻿using System;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.ServerMods
{

    public class MapLayerWobbledForest : MapLayerBase
    {
        NormalizedSimplexNoise noisegen;

        float multiplier;
        int offset;

        NormalizedSimplexNoise noisegenX;
        NormalizedSimplexNoise noisegenY;

        public float wobbleIntensity;

        public MapLayerWobbledForest(long seed, int octaves, float persistence, float scale, float multiplier = 255, int offset = 0) : base(seed)
        {
            double[] frequencies = new double[3];
            double[] amplitudes = new double[3];

            for (int i = 0; i < octaves; i++)
            {
                frequencies[i] = Math.Pow(3, i) * 1/scale;
                amplitudes[i] = Math.Pow(persistence, i);
            }

            noisegen = new NormalizedSimplexNoise(amplitudes, frequencies, seed);

            this.offset = offset;
            this.multiplier = multiplier;

            int woctaves = 3;
            float wscale = 128;
            float wpersistence = 0.9f;
            wobbleIntensity = scale / 3f;
            noisegenX = NormalizedSimplexNoise.FromDefaultOctaves(woctaves, 1 / wscale, wpersistence, seed + 2);
            noisegenY = NormalizedSimplexNoise.FromDefaultOctaves(woctaves, 1 / wscale, wpersistence, seed + 1231296);
        }

        public override int[] GenLayer(int xCoord, int zCoord, int sizeX, int sizeZ)
        {
            int[] outData = new int[sizeX * sizeZ];

            for (int z = 0; z < sizeZ; ++z)
            {
                for (int x = 0; x < sizeX; ++x)
                {
                    int offsetX = (int)(wobbleIntensity * noisegenX.Noise(xCoord + x, zCoord + z));
                    int offsetY = (int)(wobbleIntensity * noisegenY.Noise(xCoord + x, zCoord + z));

                    int wobbledX = xCoord + x + offsetX;
                    int wobbledZ = zCoord + z + offsetY;

                    double forestValue = offset + multiplier * noisegen.Noise(wobbledX, wobbledZ);

                    int climate = inputMap.GetUnpaddedInt((x * inputMap.InnerSize) / outputMap.InnerSize, (z * inputMap.InnerSize) / outputMap.InnerSize);

                    float rain = (climate >> 8) & 0xff;
                    float temperature = (climate >> 16) & 0xff;

                    outData[z*sizeX + x] = (int)GameMath.Clamp(forestValue - GameMath.Clamp(128 - rain * temperature / (255 * 255), 0, 128), 0, 255);
                }
            }

            return outData;
        }
    }
}
