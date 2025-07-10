using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.ServerMods.NoObf
{
    public class TreeGenProperties
    {
        public NatFloat treesPerChunk;
        public NatFloat shrubsPerChunk;


        public float vinesMinRain;
        public int vinesMinTemp;

        public float descVineMinTempRel;

        public TreeVariant[] TreeGens;
        public TreeVariant[] ShrubGens;
    }
}
