using System;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Vintagestory.ServerMods
{
    public class MapLayerLandforms: MapLayerBase
    {
        private NoiseLandforms noiseLandforms;
        NoiseClimate climateNoise;

        NormalizedSimplexNoise noisegenX;
        NormalizedSimplexNoise noisegenY;
        float wobbleIntensity;

        public float landFormHorizontalScale = 1f;

        public MapLayerLandforms(long seed, NoiseClimate climateNoise, ICoreServerAPI api, float landformScale) : base(seed)
        {
            this.climateNoise = climateNoise;

            float scale = TerraGenConfig.landformMapScale * landformScale;

            scale *= Math.Max(1, api.WorldManager.MapSizeY / 256f);

            noiseLandforms = new NoiseLandforms(seed, api, scale);

            int woctaves = 2;
            float wscale = 2f * TerraGenConfig.landformMapScale;
            float wpersistence = 0.9f;
            wobbleIntensity = TerraGenConfig.landformMapScale * 1.5f;
            noisegenX = NormalizedSimplexNoise.FromDefaultOctaves(woctaves, 1 / wscale, wpersistence, seed + 2);
            noisegenY = NormalizedSimplexNoise.FromDefaultOctaves(woctaves, 1 / wscale, wpersistence, seed + 1231296);
        }


        public override int[] GenLayer(int xCoord, int zCoord, int sizeX, int sizeZ)
        {
            int[] result = new int[sizeX * sizeZ];

            for (int x = 0; x < sizeX; x++)
            {
                for (int z = 0; z < sizeZ; z++)
                {
                    int offsetX = (int)(wobbleIntensity * noisegenX.Noise(xCoord + x, zCoord + z)*1.2f);
                    int offsetY = (int)(wobbleIntensity * noisegenY.Noise(xCoord + x, zCoord + z)*1.2f);

                    int finalX = (xCoord + x + offsetX);
                    int finalZ = (zCoord + z + offsetY);

                    int climate = climateNoise.GetLerpedClimateAt(finalX / TerraGenConfig.climateMapScale, finalZ / TerraGenConfig.climateMapScale);
                    int rain = (climate >> 8) & 0xff;
                    int temp = Climate.GetScaledAdjustedTemperature((climate >> 16) & 0xff, 0);

                    result[z * sizeX + x] = noiseLandforms.GetLandformIndexAt(
                        finalX,
                        finalZ,
                        temp,
                        rain
                    );
                }
            }

            return result;
        }



    }
}
