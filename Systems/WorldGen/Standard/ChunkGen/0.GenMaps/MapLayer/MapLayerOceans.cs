using System.Collections.Generic;
using Vintagestory.API.MathTools;

namespace Vintagestory.ServerMods
{
    class MapLayerOceans : MapLayerBase
    {
        NormalizedSimplexNoise noisegenX;
        NormalizedSimplexNoise noisegenY;
        float wobbleIntensity;
        NoiseOcean noiseOcean;

        int spawnOffsX, spawnOffsZ;

        public float landFormHorizontalScale = 1f;

        public MapLayerOceans(long seed, float scale, float landCoverRate, List<XZ> requireLandAt) : base(seed)
        {
            noiseOcean = new NoiseOcean(seed, scale, requireLandAt, landCoverRate);

            int woctaves = 4;
            float wscale = 2f * TerraGenConfig.oceanMapScale;
            float wpersistence = 0.9f;
            wobbleIntensity = TerraGenConfig.oceanMapScale * 1.5f * 1.2f;
            noisegenX = NormalizedSimplexNoise.FromDefaultOctaves(woctaves, 1 / wscale, wpersistence, seed + 2);
            noisegenY = NormalizedSimplexNoise.FromDefaultOctaves(woctaves, 1 / wscale, wpersistence, seed + 1231296);

            var spawnCoord = requireLandAt[0];
            var offs = GetNoiseOffsetAt(spawnCoord.X, spawnCoord.Z);
            spawnOffsX = -offs.X;
            spawnOffsZ = -offs.Z;
        }

        public XZ GetNoiseOffsetAt(int xCoord, int zCoord)
        {
            int offsetX = (int)(wobbleIntensity * noisegenX.Noise(xCoord, zCoord) * 1.2f);
            int offsetY = (int)(wobbleIntensity * noisegenY.Noise(xCoord, zCoord) * 1.2f);
            return new XZ(offsetX, offsetY);
        }

        public override int[] GenLayer(int xCoord, int zCoord, int sizeX, int sizeZ)
        {
            xCoord += spawnOffsX;
            zCoord += spawnOffsZ;

            int[] result = new int[sizeX * sizeZ];

            for (int x = 0; x < sizeX; x++)
            {
                for (int z = 0; z < sizeZ; z++)
                {
                    var nx = xCoord + x;
                    var nz = zCoord + z;

                    int offsetX = (int)(wobbleIntensity * noisegenX.Noise(nx, nz));
                    int offsetZ = (int)(wobbleIntensity * noisegenY.Noise(nx, nz));

                    result[z * sizeX + x] = noiseOcean.GetOceanIndexAt(nx + offsetX, nz + offsetZ);
                }
            }

            return result;
        }



    }
}
