using System;

namespace Vintagestory.ServerMods
{
    class MapLayerOre : MapLayerBase
    {
        private NoiseOre map;
        float zoomMul;
        float contrastMul;
        float sub;

        public MapLayerOre(long seed, NoiseOre map, float zoomMul, float contrastMul, float sub) : base(seed)
        {
            this.map = map;
            this.zoomMul = zoomMul;
            this.contrastMul = contrastMul;
            this.sub = sub;
        }

        public override int[] GenLayer(int xCoord, int zCoord, int sizeX, int sizeZ)
        {
            int[] result = new int[sizeX * sizeZ];

            float scale = TerraGenConfig.oreMapSubScale * zoomMul;

            int cacheSizeX = (int)Math.Ceiling((float)sizeX / scale) + 1;
            int cacheSizeZ = (int)Math.Ceiling((float)sizeZ / scale) + 1;

            int[] oreCache = getOreCache((int)(xCoord / scale), (int)(zCoord / scale), cacheSizeX, cacheSizeZ);

            for (int x = 0; x < sizeX; x++)
            {
                for (int z = 0; z < sizeZ; z++)
                {
                    result[z * sizeX + x] = map.GetLerpedOreValueAt(
                        x / (double)scale,
                        z / (double)scale,
                        oreCache,
                        cacheSizeX,
                        contrastMul,
                        sub
                    );
                }
            }

            return result;
        }

        // Using default values this should generate just 4 numbers
        int[] getOreCache(int coordX, int coordZ, int oreCacheSizeX, int oreCacheSizeZ)
        {
            int[] climateCache = new int[oreCacheSizeX * oreCacheSizeZ];

            for (int x = 0; x < oreCacheSizeX; x++)
            {
                for (int z = 0; z < oreCacheSizeZ; z++)
                {
                    climateCache[z * oreCacheSizeX + x] = map.GetOreAt(coordX + x, coordZ + z);
                }
            }

            return climateCache;
        }
    }
}
