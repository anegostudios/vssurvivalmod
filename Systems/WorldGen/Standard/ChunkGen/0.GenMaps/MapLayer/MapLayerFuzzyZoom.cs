using System;

namespace Vintagestory.ServerMods
{
    class MapLayerFuzzyZoom : MapLayerBase
    {
        MapLayerBase parent;

        public MapLayerFuzzyZoom(long currentSeed, MapLayerBase parent) : base(currentSeed)
        {
            this.parent = parent;
        }

        public override int[] GenLayer(int xPos, int zPos, int xSize, int zSize)
        {
            int xCoord = xPos >> 1;
            int zCoord = zPos >> 1;

            int newXSize = (xSize >> 1) + 2;
            int newZSize = (zSize >> 1) + 2;

            int[] parentInts = parent.GenLayer(xCoord, zCoord, newXSize, newZSize);

            int outXsize = newXSize - 1 << 1;
            int outZSize = newZSize - 1 << 1;

            int[] fuzzyZoom = new int[outXsize * outZSize];
            int index;

            for (int z = 0; z < newZSize - 1; ++z)
            {
                index = (z << 1) * outXsize;

                for (int x = 0; x < newXSize - 1; ++x)
                {
                    InitPositionSeed(xCoord + x, zCoord + z);

                    int valTopLeft = parentInts[x + (z + 0) * newXSize];
                    int valTopRight = parentInts[x + 1 + (z + 0) * newXSize];
                    int valBottomLeft = parentInts[x + (z + 1) * newXSize];
                    int valBottomRight = parentInts[x + 1 + (z + 1) * newXSize];

                    fuzzyZoom[index] = valTopLeft;
                    fuzzyZoom[index + outXsize] = selectRandom(valTopLeft, valBottomLeft);
                    fuzzyZoom[index + 1] = selectRandom(valTopLeft, valTopRight);
                    fuzzyZoom[index + 1 + outXsize] = selectRandom(valTopLeft, valTopRight, valBottomLeft, valBottomRight);

                    index += 2;
                }
            }

            int[] outCache = new int[xSize * zSize];

            for (int z = 0; z < zSize; ++z)
            {
                int srcPos = (z + (zPos & 1)) * outXsize + (xPos & 1);

                Array.Copy(fuzzyZoom, srcPos, outCache, z * xSize, xSize);
            }

            return outCache;
        }


        protected int selectRandom(params int[] numbers)
        {
            return numbers[NextInt(numbers.Length)];
        }

        public static MapLayerBase magnify(long seed, MapLayerBase parent, int zoomLevels)
        {
            MapLayerBase genlayer = parent;

            for (int i = 0; i < zoomLevels; ++i)
            {
                genlayer = new MapLayerFuzzyZoom(seed + i, genlayer);
            }


            return genlayer;
        }
    }
}
