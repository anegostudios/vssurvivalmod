using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.ServerMods
{
    public class MapLayerCustomPerlin : MapLayerBase
    {
        SimplexNoise noisegen;

        double[] thresholds;

        public int clampMin = 0;
        public int clampMax = 255;

        
        public MapLayerCustomPerlin(long seed, double[] amplitudes, double[] frequencies, double[] thresholds) : base(seed)
        {
            noisegen = new SimplexNoise(amplitudes, frequencies, seed + 12321);
            this.thresholds = thresholds;
        }

        public override int[] GenLayer(int xCoord, int zCoord, int sizeX, int sizeZ)
        {
            int[] outData = new int[sizeX * sizeZ];

            for (int z = 0; z < sizeZ; ++z)
            {
                for (int x = 0; x < sizeX; ++x)
                {
                    outData[z * sizeX + x] = (int)GameMath.Clamp(noisegen.Noise(xCoord + x, zCoord + z, thresholds), clampMin, clampMax);
                }
            }

            return outData;
        }


        /*public int[] GenLayerMax0(int xCoord, int zCoord, int sizeX, int sizeZ)
        {
            int[] outData = new int[sizeX * sizeZ];

            for (int z = 0; z < sizeZ; ++z)
            {
                for (int x = 0; x < sizeX; ++x)
                {
                    outData[z * sizeX + x] = (int)GameMath.Clamp(noisegen.NoiseMax0(xCoord + x, zCoord + z, thresholds), clampMin, clampMax);
                }
            }

            return outData;
        }*/
    }
}
