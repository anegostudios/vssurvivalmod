using System.Collections.Generic;

#nullable disable

namespace Vintagestory.ServerMods
{
    public struct XZ
    {
        public int X;
        public int Z;

        public XZ(int x, int z)
        {
            X = x;
            Z = z;
        }
    }

    class NoiseOcean : NoiseBase
    {
        float landcover;
        float scale;

        public NoiseOcean(long seed, float scale, float landcover) : base(seed)
        {
            this.landcover = landcover;
            this.scale = scale;
        }

        public int GetOceanIndexAt(int unscaledXpos, int unscaledZpos)
        {
            var xpos = (int)(unscaledXpos / scale);
            var zpos = (int)(unscaledZpos / scale);

            InitPositionSeed(xpos, zpos);

            var rand = NextInt(10000) / 10000.0;
            if (rand < landcover) return 0;

            return 255;
        }
    }
}
