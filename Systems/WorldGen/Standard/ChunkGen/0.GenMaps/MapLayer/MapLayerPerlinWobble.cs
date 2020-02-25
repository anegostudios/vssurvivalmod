using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API;
using Vintagestory.API.MathTools;

namespace Vintagestory.ServerMods
{
    public class MapLayerPerlinWobble : MapLayerTransformBase
    {
        protected NormalizedSimplexNoise noisegenX;
        protected NormalizedSimplexNoise noisegenY;

        protected float scale;
        protected float intensity;

        public MapLayerPerlinWobble(long seed, MapLayerBase parent, int octaves, float persistence, float scale, float intensity = 1) : base(seed, parent)
        {
            noisegenX = NormalizedSimplexNoise.FromDefaultOctaves(octaves, 1 / scale, persistence, seed);
            noisegenY = NormalizedSimplexNoise.FromDefaultOctaves(octaves, 1 / scale, persistence, seed + 1231296);

            this.scale = scale;
            this.intensity = intensity;
        }

        public override int[] GenLayer(int xCoord, int zCoord, int sizeX, int sizeZ)
        {
            int margin = (int)Math.Ceiling(intensity);
            int paddedSizeX = sizeX + 2 * margin;
            int paddedSizeZ = sizeZ + 2 * margin;


            int[] inData = parent.GenLayer(xCoord - margin, zCoord - margin, paddedSizeX, paddedSizeZ);

            int[] outData = new int[sizeX * sizeZ];

            for (int y = 0; y < sizeZ; ++y)
            {
                for (int x = 0; x < sizeX; ++x)
                {
                    int offsetX = (int)(intensity * noisegenX.Noise(xCoord + x + margin, zCoord + y + margin));
                    int offsetY = (int)(intensity * noisegenY.Noise(xCoord + x + margin, zCoord + y + margin));

                    // We wrap around overflowing coordinates
                    int newXCoord = GameMath.Mod(x + offsetX + margin/2, paddedSizeX);
                    int newYCoord = GameMath.Mod(y + offsetY + margin/2, paddedSizeZ);

                    outData[y * sizeX + x] = inData[newYCoord * paddedSizeX + newXCoord];
                }
            }

            return outData;
        }


    }

}
