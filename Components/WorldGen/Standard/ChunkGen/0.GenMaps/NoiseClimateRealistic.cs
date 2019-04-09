using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API;
using Vintagestory.API.MathTools;

namespace Vintagestory.ServerMods
{
    public class NoiseClimateRealistic : NoiseClimatePatchy
    {
        double zTempOffset;
        double mapsizeZ;

        public NoiseClimateRealistic(long seed, double mapsizeZ) : base(seed+1)
        {
            //120 = 10 deg
            //125 = 18 deg
            // => 1.6 deg for each num
            zTempOffset = 118 + NextInt(5);
            this.mapsizeZ = mapsizeZ;
        }

        public override int GetClimateAt(int posX, int posZ)
        {
            InitPositionSeed(posX, posZ);
            return GetRandomClimate(posX, posZ);
        }

        // Calculates interpolated climate value from anew
        public override int GetLerpedClimateAt(double posX, double posZ)
        {
            int posXInt = (int)posX;
            int posZInt = (int)posZ;

            InitPositionSeed(posXInt, posZInt);
            int leftTop = GetRandomClimate(posX, posZ);

            InitPositionSeed(posXInt + 1, posZInt);
            int rightTop = GetRandomClimate(posX, posZ);

            InitPositionSeed(posXInt, posZInt + 1);
            int leftBottom = GetRandomClimate(posX, posZ);

            InitPositionSeed(posXInt + 1, posZInt + 1);
            int rightBottom = GetRandomClimate(posX, posZ);

            return GameMath.BiSerpRgbColor((float)(posX - posXInt), (float)(posZ - posZInt), leftTop, rightTop, leftBottom, rightBottom);
        }

        // Calculates climate value from 4 cached values
        // Use relative positions here
        public override int GetLerpedClimateAt(double posX, double posZ, int[] climateCache, int sizeX)
        {
            int posXInt = (int)(posX);
            int posZInt = (int)(posZ);

            return GameMath.BiSerpRgbColor(
                (float)(posX - posXInt), 
                (float)(posZ - posZInt),
                climateCache[posZInt * sizeX + posXInt],
                climateCache[posZInt * sizeX + posXInt + 1],
                climateCache[(posZInt + 1) * sizeX + posXInt], 
                climateCache[(posZInt + 1) * sizeX + posXInt + 1]
            );
        }

        // 16-23 bits = Red = temperature
        // 8-15 bits = Green = rain
        // 0-7 bits = Blue = unused 
        int GetRandomClimate(double posX, double posZ)
        {
            int humidity = 0;
            int temperature;
            int rain;

            double centerZ = zTempOffset + (posZ - mapsizeZ/2);

            // at z==0 we need y = 0.4 - 0.65
            // which is produzed by z = 2.73 to 2.44
            // *1/0.04 is 68.25 to 61

            //gaussRnd2(70) - 15 + 

            temperature = (int)GameMath.Clamp(140 * GameMath.Cos((float)-centerZ * 0.04) + 128, 0, 255);


            //Console.WriteLine(centerZ +": "+ GameMath.Cos((float)-centerZ * 0.04) + " = " + temperature);

            /*int rnd = NextInt(100);
            // 20% very dry
            // 80% random dry - wet
            // 20% very wet 
            if (rnd < 19)
            {
                rain = GameMath.Clamp(gaussRnd2(40), 0, Math.Max(40, 20 + temperature));
            } else if (rnd < 80)
            {
                rain = GameMath.Clamp(gaussRnd3(300), 0, Math.Max(40, 20 + temperature));
            }
            else
            {
                rain = GameMath.Clamp(180 + gaussRnd2(100), 0, Math.Max(40, 20 + temperature));
            }*/

            rain = NextInt(256);
            
            return (temperature << 16) + (rain << 8) + (humidity);
        }
    }
}
