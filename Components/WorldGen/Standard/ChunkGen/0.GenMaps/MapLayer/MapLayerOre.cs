using System;

namespace Vintagestory.ServerMods
{
    class MapLayerOre : MapLayerBase
    {
        private NoiseOre map;
        
        public MapLayerOre(long seed, NoiseOre map) : base(seed)
        {
            this.map = map;
        }

        public override int[] GenLayer(int xCoord, int zCoord, int sizeX, int sizeZ)
        {
            int[] result = new int[sizeX * sizeZ];
            TerraGenConfig.oreMapSubScale = 12;
            int cacheSizeX = (int)Math.Ceiling((float)sizeX / TerraGenConfig.oreMapSubScale) + 1;
            int cacheSizeZ = (int)Math.Ceiling((float)sizeZ / TerraGenConfig.oreMapSubScale) + 1;

            int[] oreCache = getOreCache(xCoord / TerraGenConfig.oreMapSubScale, zCoord / TerraGenConfig.oreMapSubScale, cacheSizeX, cacheSizeZ);

            for (int x = 0; x < sizeX; x++)
            {
                for (int z = 0; z < sizeZ; z++)
                {
                    result[z * sizeX + x] = map.GetLerpedClimateAt(
                        x / (double)TerraGenConfig.oreMapSubScale,
                        z / (double)TerraGenConfig.oreMapSubScale,
                        oreCache,
                        cacheSizeX
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
