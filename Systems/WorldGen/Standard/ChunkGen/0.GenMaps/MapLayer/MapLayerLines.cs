namespace Vintagestory.ServerMods
{
    class MapLayerLines : MapLayerBase
    {
        public MapLayerLines(long seed) : base(seed)
        {
            
        }

        public override int[] GenLayer(int xCoord, int zCoord, int sizeX, int sizeZ)
        {
            int[] result = new int[sizeX*sizeX];

            for (int x = 0; x < sizeX; x++)
            {
                for (int z = 0; z < sizeZ; z++)
                {
                    result[z*sizeX + x] = z%20 <= 2 ? 255 : 0;
                }
            }

            return result;
        }
    }
}
