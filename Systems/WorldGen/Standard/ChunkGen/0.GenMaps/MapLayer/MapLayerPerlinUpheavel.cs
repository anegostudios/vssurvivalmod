using System.Collections.Generic;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.ServerMods
{
    public class MapLayerPerlinUpheavel : MapLayerBase
    {
        NormalizedSimplexNoise superLowResNoiseGen;

        float multiplier;
        int offset;
        public float noiseOffset;
        public MapLayerPerlinUpheavel(long seed, float noiseOffset, float scale, float multiplier = 255, int offset = 0) : base(seed)
        {
            superLowResNoiseGen = new NormalizedSimplexNoise(new double[] { 1, 0.5, 0.25, 0.15 }, new double[] { 0.5 / scale, 1 / scale, 2 / scale, 4 / scale }, seed + 1685);

            this.noiseOffset = 1 - noiseOffset;
            this.offset = offset;
            this.multiplier = multiplier;
        }

        public override int[] GenLayer(int xCoord, int zCoord, int sizeX, int sizeZ)
        {
            int[] outData = new int[sizeX * sizeZ];

            for (int z = 0; z < sizeZ; ++z)
            {
                for (int x = 0; x < sizeX; ++x)
                {
                    double lowresvalue = GameMath.Clamp((superLowResNoiseGen.Noise(xCoord + x, zCoord + z) - noiseOffset) * 15, 0, 1);
                    double outvalue = offset + multiplier;
                    outData[z * sizeX + x] = (int)GameMath.Clamp(lowresvalue * outvalue, 0, 255);
                }
            }

            return outData;
        }
    }
}
