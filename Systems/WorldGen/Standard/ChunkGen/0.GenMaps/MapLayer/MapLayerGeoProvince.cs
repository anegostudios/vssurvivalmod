using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

#nullable disable

namespace Vintagestory.ServerMods
{
    class MapLayerGeoProvince : MapLayerBase
    {
        private NoiseGeoProvince noiseGeoProvince;

        NormalizedSimplexNoise noisegenX;
        NormalizedSimplexNoise noisegenY;
        float wobbleIntensity;

        public MapLayerGeoProvince(long seed, ICoreServerAPI api) : base(seed)
        {
            noiseGeoProvince = new NoiseGeoProvince(seed, api);

            int woctaves = 4;
            float wscale = 1.5f * TerraGenConfig.geoProvMapScale;
            float wpersistence = 0.9f;
            wobbleIntensity = TerraGenConfig.geoProvMapScale * 1.5f;
            noisegenX = NormalizedSimplexNoise.FromDefaultOctaves(woctaves, 0.4f / wscale, wpersistence, seed + 2);
            noisegenY = NormalizedSimplexNoise.FromDefaultOctaves(woctaves, 0.4f / wscale, wpersistence, seed + 1231296);
        }


        public override int[] GenLayer(int xCoord, int zCoord, int sizeX, int sizeZ)
        {
            int[] result = new int[sizeX * sizeZ];

            for (int x = 0; x < sizeX; x ++)
            {
                for (int z = 0; z < sizeZ; z ++)
                {
                    int offsetX = (int)(wobbleIntensity * noisegenX.Noise(xCoord + x, zCoord + z));
                    int offsetY = (int)(wobbleIntensity * noisegenY.Noise(xCoord + x, zCoord + z));

                    int finalX = (xCoord + x + offsetX) / TerraGenConfig.geoProvMapScale;
                    int finalZ = (zCoord + z + offsetY) / TerraGenConfig.geoProvMapScale;

                    result[z * sizeX + x] = noiseGeoProvince.GetProvinceIndexAt(finalX, finalZ);
                }
            }

            return result;
        }
    }
}
