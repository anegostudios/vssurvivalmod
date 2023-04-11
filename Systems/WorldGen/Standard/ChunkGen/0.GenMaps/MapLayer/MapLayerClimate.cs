using System;

namespace Vintagestory.ServerMods
{
    class MapLayerClimate : MapLayerBase
    {
        public NoiseClimate noiseMap;


        public MapLayerClimate(long seed, NoiseClimate map) : base(seed)
        {
            this.noiseMap = map;
        }

        public override int[] GenLayer(int xCoord, int zCoord, int sizeX, int sizeZ)
        {
            int[] result = new int[sizeX * sizeZ];

            int cacheSizeX = (int)Math.Ceiling((float)sizeX / TerraGenConfig.climateMapSubScale) + 1;
            int cacheSizeZ = (int)Math.Ceiling((float)sizeZ / TerraGenConfig.climateMapSubScale) + 1;

            int[] climateCache = getClimateCache (
                (int)Math.Floor((float)xCoord / TerraGenConfig.climateMapSubScale),
                (int)Math.Floor((float)zCoord / TerraGenConfig.climateMapSubScale),
                cacheSizeX, cacheSizeZ
            );

            for (int x = 0; x < sizeX; x++)
            {
                for (int z = 0; z < sizeZ; z++)
                {
                    result[z*sizeX + x] = noiseMap.GetLerpedClimateAt(
                        x / (double)TerraGenConfig.climateMapSubScale, 
                        z / (double)TerraGenConfig.climateMapSubScale, 
                        climateCache, 
                        cacheSizeX
                    );
                }
            }

            return result;
        }

        // Using default values this should generate just 4 numbers
        int[] getClimateCache(int coordX, int coordZ, int climateCacheSizeX, int climateCacheSizeZ)
        {
            int[] climateCache = new int[climateCacheSizeX * climateCacheSizeZ];

            for (int x = 0; x < climateCacheSizeX; x++)
            {
                for (int z = 0; z < climateCacheSizeZ; z++)
                {
                    climateCache[z * climateCacheSizeX + x] = noiseMap.GetClimateAt(coordX + x, coordZ + z);
                }
            }

            return climateCache;
        }
    }
}
