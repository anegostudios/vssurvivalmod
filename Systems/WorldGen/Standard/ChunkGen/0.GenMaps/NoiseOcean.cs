using System;
using System.Collections.Generic;

namespace Vintagestory.ServerMods
{
    public struct XZ
    {
        public int X;
        public int Z;
        
        public XZ(int x, int z)
        {
            this.X = x;
            this.Z = z;
        }
    }

    class NoiseOcean : NoiseBase
    {
        float landcover;
        List<XZ> requireLandAt;
        float scale;

        public NoiseOcean(long seed, float scale, List<XZ> requireLandAt, float landcover) : base(seed)
        {
            this.requireLandAt = requireLandAt;
            this.landcover = landcover;
            this.scale = scale;
        }


        public int GetOceanIndexAt(int unscaledXpos, int unscaledZpos)
        {
            float xpos = (float)unscaledXpos / scale;
            float zpos = (float)unscaledZpos / scale;

            int xposInt = (int)xpos;
            int zposInt = (int)zpos;

            InitPositionSeed(xposInt, zposInt);

            double rand = NextInt(10000) / 10000.0;
            if (rand < landcover) return 0;

            for (int i = 0; i < requireLandAt.Count; i++)
            {
                var xz = requireLandAt[i];
                if (Math.Abs(xz.X - unscaledXpos) <= scale/2 && Math.Abs(xz.Z - unscaledZpos) <= scale/2)
                {
                    return 0;
                }
            }

            return 255;
        }


    }
}
