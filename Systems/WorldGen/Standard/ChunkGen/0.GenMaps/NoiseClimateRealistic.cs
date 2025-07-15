using System;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.ServerMods
{
    public class NoiseClimateRealistic : NoiseClimatePatchy
    {
        double halfRange;
        float geologicActivityInv = 10;

        public float GeologicActivityStrength { set { geologicActivityInv = 1 / value; } }

        /// <summary>
        /// Distance from the north pole
        /// </summary>
        public double ZOffset { get; private set; }

        public NoiseClimateRealistic(long seed, double mapsizeZ, int polarEquatorDistance, int spawnMinTemp, int spawnMaxTemp) : base(seed+1)
        {
            // range must be divided by the climate map scaling
            halfRange = polarEquatorDistance / TerraGenConfig.climateMapScale / TerraGenConfig.climateMapSubScale;

            // Our temperature values are stored in a range from 0..255, so lets descale them
            int minTempDescaled = Climate.DescaleTemperature(spawnMinTemp);
            int maxTempDescaled = Climate.DescaleTemperature(spawnMaxTemp);

            // Chose one random value between min and max temp
            double rndTemp = minTempDescaled + NextInt(maxTempDescaled - minTempDescaled + 1);

            // A change of one degree
            double zPerDegDescaled = halfRange / 255;

            // We need to shift over z by this much to achieve 6-15 degrees
            ZOffset = rndTemp * zPerDegDescaled - mapsizeZ / 2;
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
            int tempRnd = NextInt(51) - 35;

            // https://stackoverflow.com/a/22400799/1873041
            // y = (A / P) * (P - abs(x % (2 * P) - P))
            // A = amplitude
            // P = half length

            double A = 255;
            double P = halfRange;
            double z = posZ + ZOffset;

            // http://fooplot.com/#W3sidHlwZSI6MCwiZXEiOiIyNTUvOTcuNjU2MjUqKDk3LjY1NjI1LWFicyhhYnMoeCklKDIqOTcuNjU2MjUpLTk3LjY1NjI1KSkiLCJjb2xvciI6IiMwMDAwMDAifSx7InR5cGUiOjEwMDAsIndpbmRvdyI6WyItMjAzMC43NjkyMzA3NjkyMzA3IiwiMTk2OS4yMzA3NjkyMzA3NjkzIiwiLTI1NSIsIjI1NSJdfV0-
            int preTemp = (int)((A / P) * (P - Math.Abs(Math.Abs(z) % (2 * P) - P))) + tempRnd;
            int temperature = GameMath.Clamp((int)(preTemp * tempMul), 0, 255);
            int rain = Math.Min(255, (int)(NextInt(256) * rainMul));
            int hereGeologicActivity = (int)Math.Max(0, Math.Pow(NextInt(256)/255f, geologicActivityInv) * 255);

            return (temperature << 16) + (rain << 8) + (hereGeologicActivity);
        }
    }
}
