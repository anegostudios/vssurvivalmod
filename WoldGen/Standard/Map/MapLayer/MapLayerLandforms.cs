using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Vintagestory.ServerMods
{
    class MapLayerLandforms: MapLayerBase
    {
        private NoiseLandforms noiseLandforms;
        NoiseClimate climateNoise;

        NormalizedSimplexNoise noisegenX;
        NormalizedSimplexNoise noisegenY;
        float wobbleIntensity;


        public MapLayerLandforms(long seed, NoiseClimate climateNoise, ICoreServerAPI api) : base(seed)
        {
            this.climateNoise = climateNoise;
            noiseLandforms = new NoiseLandforms(seed, api);

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
                    int offsetX = (int)(wobbleIntensity * noisegenX.Noise(xCoord + x, zCoord + z));
                    int offsetY = (int)(wobbleIntensity * noisegenY.Noise(xCoord + x, zCoord + z));

                    int finalX = (xCoord + x + offsetX);
                    int finalZ = (zCoord + z + offsetY);

                    result[z * sizeX + x] = noiseLandforms.GetLandformIndexAt(
                        finalX, 
                        finalZ, 
                        climateNoise.GetLerpedClimateAt(finalX / TerraGenConfig.climateMapScale, finalZ / TerraGenConfig.climateMapScale)
                    );

                    /*float baseX = (float)finalX / TerraGenConfig.landformMapScale;
                    float baseY = (float)finalZ / TerraGenConfig.landformMapScale;

                    result[z * sizeX + x] = noiseLandforms.GetParentLandformIndexAt(
                        (int)baseX,
                        (int)baseY,
                        climateNoise.GetLerpedClimateAt(finalX / TerraGenConfig.climateMapScale, finalZ / TerraGenConfig.climateMapScale),
                        baseX - (int)baseX,
                        baseY - (int)baseY
                    );*/
                }
            }

            return result;
        }


      
    }
}
