using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.ServerMods
{
    public class MapLayerDoublePerlin : MapLayerBase
    {
        NormalizedSimplexNoise noisegenX;
        NormalizedSimplexNoise noisegenY;

        float multiplier;

        public MapLayerDoublePerlin(long seed, int octaves, float persistence, int scale, int multiplier) : base(seed)
        {
            noisegenX = NormalizedSimplexNoise.FromDefaultOctaves(octaves, 1f / scale, persistence, seed);
            noisegenY = NormalizedSimplexNoise.FromDefaultOctaves(octaves, 1f / scale, persistence, seed + 1232);
            this.multiplier = multiplier;
        }

        public override int[] GenLayer(int xCoord, int zCoord, int sizeX, int sizeZ)
        {
            int[] outData = new int[sizeX * sizeZ];

            for (int z = 0; z < sizeZ; ++z)
            {
                for (int x = 0; x < sizeX; ++x)
                {
                    outData[z * sizeX + x] =
                        (int)GameMath.Clamp(multiplier * noisegenX.Noise(xCoord + x, zCoord + z), 0, 255) |
                        (int)GameMath.Clamp(multiplier * noisegenY.Noise(xCoord + x, zCoord + z), 0, 255) << 8
                    ;
                }
            }

            return outData;
        }
    }
}
