using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.MathTools;

namespace Vintagestory.ServerMods
{
    public class NoiseOre : NoiseBase
    {
        public NoiseOre(long worldSeed) : base(worldSeed)
        {
        }


        public int GetOreAt(int posX, int posZ)
        {
            InitPositionSeed(posX, posZ);
            return GetRandomOre();
        }

        // Calculates climate value from 4 cached values
        // Use relative positions here
        public int GetLerpedOreValueAt(double posX, double posZ, int[] oreCache, int sizeX, float contrastMul, float sub)
        {
            int posXInt = (int)(posX);
            int posZInt = (int)(posZ);

            byte val = GameMath.BiSerpByte(
                (float)(posX - posXInt),
                (float)(posZ - posZInt),
                0,
                oreCache[posZInt * sizeX + posXInt],
                oreCache[posZInt * sizeX + posXInt + 1],
                oreCache[(posZInt + 1) * sizeX + posXInt],
                oreCache[(posZInt + 1) * sizeX + posXInt + 1]
            );

            val = (byte)GameMath.Clamp((val - 128f) * contrastMul + 128 - sub, 0, 255);

            int richness = Math.Max(
                oreCache[(posZInt + 1) * sizeX + posXInt + 1] & 0xff0000, 
                Math.Max(oreCache[(posZInt + 1) * sizeX + posXInt] & 0xff0000, 
                Math.Max(oreCache[posZInt * sizeX + posXInt] & 0xff0000, oreCache[posZInt * sizeX + posXInt + 1] & 0xff0000))
            );
            int hypercommon = Math.Max(
                oreCache[(posZInt + 1) * sizeX + posXInt + 1] & 0x00ff00, 
                Math.Max(oreCache[(posZInt + 1) * sizeX + posXInt] & 0x00ff00, 
                Math.Max(oreCache[posZInt * sizeX + posXInt] & 0x00ff00, oreCache[posZInt * sizeX + posXInt + 1] & 0x00ff00))
            );

            return val == 0 ? 0 : val | richness | hypercommon;
        }

        int gaussRnd3(int maxint) { return Math.Min(255, (NextInt(maxint) + NextInt(maxint) + NextInt(maxint)) / 3); }
        int gaussRnd2(int maxint) { return Math.Min(255, (NextInt(maxint) + NextInt(maxint)) / 2); }

        int GetRandomOre()
        {
            int rnd = NextInt(1024);

            // 35% chance poor
            // 15% chance rich
            // 50% chance normal
            //int quality = NextInt(2) > 0 ? 160 : (NextInt(50) > 15 ? 85 : 255);
            int quality = (NextInt(2) > 0 ? 1 : (NextInt(50) > 15 ? 0 : 2)) << 10;

            // Very low chance for hyper rich area
            if (rnd < 1)
            {
                return quality | (1 << 8) | 255;
            }

            // Low chance very rich area
            if (rnd < 30)
            {
                return quality | 255;
            }

            // Low chance medium rich area
            if (rnd < 105)
            {
                return quality | (75 + NextInt(100));
            }

            // Low chance unrich area
            if (rnd < 190)
            {
                return quality | (20 + NextInt(20));
            }
            
            return 0;
        }

    }
}
