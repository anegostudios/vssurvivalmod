using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API;
using Vintagestory.API.MathTools;

namespace Vintagestory.ServerMods
{
    public abstract class NoiseClimate : NoiseBase
    {
        public NoiseClimate(long worldSeed) : base(worldSeed)
        {
        }

        public abstract int GetClimateAt(int posX, int posZ);
        
        public abstract int GetLerpedClimateAt(double posX, double posZ);

        public abstract int GetLerpedClimateAt(double posX, double posZ, int[] climateCache, int sizeX);
    }

    public class NoiseClimatePatchy : NoiseClimate
    {

        public NoiseClimatePatchy(long seed) : base(seed)
        {
            
        }

        public override int GetClimateAt(int posX, int posZ)
        {
            InitPositionSeed(posX, posZ);
            return GetRandomClimate();
        }

        // Calculates interpolated climate value from anew
        public override int GetLerpedClimateAt(double posX, double posZ)
        {
            int posXInt = (int)posX;
            int posZInt = (int)posZ;

            InitPositionSeed(posXInt, posZInt);
            int leftTop = GetRandomClimate();

            InitPositionSeed(posXInt + 1, posZInt);
            int rightTop = GetRandomClimate();

            InitPositionSeed(posXInt, posZInt + 1);
            int leftBottom = GetRandomClimate();

            InitPositionSeed(posXInt + 1, posZInt + 1);
            int rightBottom = GetRandomClimate();

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

        protected int gaussRnd3(int maxint) { return Math.Min(255, (NextInt(maxint) + NextInt(maxint) + NextInt(maxint)) / 3); }
        protected int gaussRnd2(int maxint) { return Math.Min(255, (NextInt(maxint) + NextInt(maxint)) / 2); }

        // 16-23 bits = Red = temperature
        // 8-15 bits = Green = rain
        // 0-7 bits = Blue = unused 
        protected virtual int GetRandomClimate()
        {
            int rnd = NextIntFast(127);
            int humidity = 0;
            int temperature;
            int rain;

            
            // Low chance for very cold areas
            if (rnd < 20)
            {
                temperature = gaussRnd3(60);
                rain = gaussRnd3(130);

                return (temperature << 16) + (rain << 8) + (humidity);
            }

            // Low chance for very hot and dry areas
            if (rnd < 40)
            {
                temperature = Math.Min(255, 220 + gaussRnd3(75));
                rain = gaussRnd3(20);
                return (temperature << 16) + (rain << 8) + (humidity);
            }

            // Low chance for very hot and very wet areas
            if (rnd < 50)
            {
                temperature = Math.Min(255, 220 + gaussRnd3(75));
                rain = 220 + NextInt(35);
                return (temperature << 16) + (rain << 8) + (humidity);
            }

            // Very low chance for temperate very wet
            if (rnd < 55)
            {
                temperature = 120 + NextInt(60);
                rain = 200 + NextInt(50);
                return (temperature << 16) + (rain << 8) + (humidity);
            }



            // Otherwise Temperate
            temperature = Math.Min(255, 100 + gaussRnd2(165));
            rain = gaussRnd3(210 - (150 - temperature));

            return (temperature << 16) + (rain << 8) + (humidity);
        }
    }
}
