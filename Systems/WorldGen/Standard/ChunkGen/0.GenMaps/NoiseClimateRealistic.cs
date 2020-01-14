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
        double zOffset;
        double mapsizeZ;

        double halfRange;
        
        public NoiseClimateRealistic(long seed, double mapsizeZ) : base(seed+1)
        {
            // We want the full range (from min temp to max temp) to pass every 50.000 blocks 
            double climateBandWaveLength = 50000;
            
            // range must be divided by the climate map scaling
            halfRange = climateBandWaveLength / TerraGenConfig.climateMapScale / TerraGenConfig.climateMapSubScale;

            // We want the player to spawn in an area of temperature from 6 to 15 degrees
            float minTemp = 6;
            float maxTemp = 14;

            // Our temperature values are stored in a range from 0..255, so lets descale them
            int minTempDescaled = TerraGenConfig.DescaleTemperature(minTemp);
            int maxTempDescaled = TerraGenConfig.DescaleTemperature(maxTemp);

            // Chose one random value between min and max temp
            double rndTemp = minTempDescaled + NextInt(maxTempDescaled - minTempDescaled + 1);

            // A change of one degree 
            double zPerDegDescaled = halfRange / 255;
           
            // We need to shift over z by this much to achieve 6-15 degrees
            zOffset = rndTemp * zPerDegDescaled;  

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
            int tempRnd = NextInt(51) - 30;

            // https://stackoverflow.com/a/22400799/1873041
            // y = (A / P) * (P - abs(x % (2 * P) - P))
            // A = amplitude
            // P = half length

            double A = 255;
            double P = halfRange;
            double z = posZ - mapsizeZ / 2 + zOffset;

            // http://fooplot.com/#W3sidHlwZSI6MCwiZXEiOiIyNTUvOTcuNjU2MjUqKDk3LjY1NjI1LWFicyhhYnMoeCklKDIqOTcuNjU2MjUpLTk3LjY1NjI1KSkiLCJjb2xvciI6IiMwMDAwMDAifSx7InR5cGUiOjEwMDAsIndpbmRvdyI6WyItMjAzMC43NjkyMzA3NjkyMzA3IiwiMTk2OS4yMzA3NjkyMzA3NjkzIiwiLTI1NSIsIjI1NSJdfV0-
            int preTemp = (int)((A / P) * (P - Math.Abs(Math.Abs(z) % (2 * P) - P))) + tempRnd;
            int temperature = GameMath.Clamp((int)(preTemp * tempMul), 0, 255);
            int rain = Math.Min(255, (int)(NextInt(256) * rainMul));
            int humidity = 0;
            
            return (temperature << 16) + (rain << 8) + (humidity);
        }
    }
}
