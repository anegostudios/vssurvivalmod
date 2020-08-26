using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API;
using Vintagestory.API.MathTools;
using Vintagestory.ServerMods.NoObf;

namespace Vintagestory.ServerMods
{

    public class TerraGenConfig
    {
        #region WorldGen

        public static double CavesPerChunkColumn = 0.37;

        /// <summary>
        /// Amount of octaves used by terrain gen
        /// </summary>
        public static int terrainGenOctaves = 9;

        /// <summary>
        /// Horizontal lerp of the 3d terrain noise
        /// </summary>
        public const int lerpHorizontal = 4;
        /// <summary>
        /// Vertical lerp of the 3d terrain noise
        /// </summary>
        public const int lerpVertical = 8;


        

        public static double terrainNoiseVerticalScale = 2.0;

        public static int rockStrataScale = 16;
        public static int rockStrataOctaveScale = 32;

        public static int shrubMapScale = 16;
        public static int forestMapScale = 32;
        public static int forestMapPadding = 0;

        public static int beachMapScale = 16;
        public static int beachMapPadding = 0;

        public static int climateMapWobbleScale = 192;
        public static int climateMapScale = 32;
        public static int climateMapSubScale = 16;

        public static int oreMapWobbleScale = 110;
        public static int oreMapScale = 16;
        public static int oreMapSubScale = 12;

        public static int depositVerticalDistortScale = 8;

        public static int oreMapPadding = 0;
        public static int climateMapPadding = 1;

        public static int geoProvMapPadding = 3;
        public static int geoProvMapScale = 64;
        public static int geoProvSmoothingRadius = 2;

        public static int landformMapPadding = 4;
        public static int landformMapScale = 16;
        public static int landFormSmoothingRadius = 3;

        

        public static int seaLevel = 110;

        internal static bool GenerateVegetation = true;
        internal static bool GenerateStructures = true;

        //internal static int depositDistortionScale = 2;

        #endregion


        public static int DescaleTemperature(float temperature)
        {
            return (int)((temperature + 20) * 4.25f);
        }

        // These methods are in various places, need to find an elegant way to make this usable everywhere

        public static int GetRainFall(int rainfall, int y)
        {
            return Math.Min(255, rainfall + (y - seaLevel) / 2 + 5 * GameMath.Clamp(8 + seaLevel - y, 0, 8));
        }


        // The ds/1.5f is also hardcoded in shaderincluds/colormap.vsh
        public static int GetScaledAdjustedTemperature(int unscaledTemp, int distToSealevel)
        {
            return GameMath.Clamp((int)((unscaledTemp - distToSealevel / 1.5f) / 4.25f) - 20, -20, 40);
        }

        public static float GetScaledAdjustedTemperatureFloat(int unscaledTemp, int distToSealevel)
        {
            return GameMath.Clamp(((unscaledTemp - distToSealevel / 1.5f) / 4.25f) - 20, -20, 40);
        }

        public static int GetAdjustedTemperature(int unscaledTemp, int distToSealevel)
        {
            return (int)GameMath.Clamp(unscaledTemp - distToSealevel / 1.5f, 0, 255);
        }

        public static int GetFertility(int rain, float scaledTemp, float posYRel)
        {
            float f = Math.Min(255, rain / 2f + Math.Max(0, rain * DescaleTemperature(scaledTemp) / 512f)); // + Math.Max(0, 100 - 60 * (rock.SoilpH - 6.5f)));

            // Reduce fertility by up to 100 in mountains, but only if fertility is above 100 already
            float weight = 1 - Math.Max(0, (80 - f) / 80);

            return (int)Math.Max(0, f - Math.Max(0, 50 * (posYRel - 0.5f)) * weight);
        }
        

        public static int GetFertilityFromUnscaledTemp(int rain, int unscaledTemp, float posYRel)
        {
            float f = Math.Min(255, rain / 2f + Math.Max(0, rain * unscaledTemp / 512f)); // + Math.Max(0, 100 - 60 * (rock.SoilpH - 6.5f)));

            // Reduce fertility by up to 100 in mountains, but only if fertility is above 100 already
            float weight = 1 - Math.Max(0, (80 - f) / 80);

            return (int)Math.Max(0, f - Math.Max(0, 50 * (posYRel - 0.5f)) * weight);
        }

        public static float SoilThickness(float rainRel, float temp, int distToSealevel, float thicknessVar) {

            float f = 8f * thicknessVar;

            float weight = 1 - Math.Max(0, (4 - f) / 4);

            return  f - distToSealevel / 35 * weight ;
        }


        public static float ErodedMaterialThickness()
        {
            return 0;
        }


        // Ice/Snow buildup from permafrost
        public static float SnowThickness(int temp)
        {
            return Math.Min(0, (30 - temp) / 6);
        }


        // Humus thickness thresholds
        // Below 0.2 = low fertility
        // Below 0.6 = medium fertility
        // Otherwise = hight fertiltiy

        // e.g. 1.2 humus thickness
        // => top block is high fertility soil
        // below is low fertility soil

        

        // Returns 3 values: 
        // float[0]: 0 = low fertility, 1 = medium fertility, 2 = high fertility
        // float[1]: thickness
        public static float[] GetSoilFertility(RockStrataVariant rock, int forest, int climate, int yPos, int worldHeight)
        {
            float SoilpH = rock.SoilpH - 0.8f * forest / 255;
            float NonOrganicMaterialThickness = rock.WeatheringFactor * ((climate & 0xff + ((climate >> 8) & 0xff) + ((climate >> 16) & 0xff))) / 256;
            float RelativeSeaLevelDistance = (yPos - TerraGenConfig.seaLevel) / (float)worldHeight;

            NonOrganicMaterialThickness *= Math.Max(0.2f, 1 - RelativeSeaLevelDistance);

            float HumusThickness = 3 * (SoilpH < 6.5f ? Math.Max(0, SoilpH / 2.5f - 1.6f) : Math.Max(0, 5.33f - SoilpH / 1.5f));
            HumusThickness *= Math.Max(0.2f, 1 - RelativeSeaLevelDistance);

            return new float[] {
                HumusThickness + 1
            };

            /*
            float RockpH = 6.5f;
            float RockWeatheringFactor = 1;

            float SoilpH = RockpH - 0.8f*forestMap[z * chunksize + x]/255;
            float NonOrganicMaterialThickness = RockWeatheringFactor*((climate & 0xff + ((climate >> 8) & 0xff) + ((climate >> 16) & 0xff)))/256;
            float RelativeSeaLevelDistance = (yPos - TerraGenConfig.seaLevel) / (float)worldHeight;

            NonOrganicMaterialThickness *= Math.Max(0.2f, 1 - RelativeSeaLevelDistance);

            float HumusThickness = 3 * (SoilpH < 6.5f ? Math.Max(0, SoilpH / 2.5f - 1.6f) : Math.Max(0, 5.33f - SoilpH / 1.5f));
            HumusThickness *= Math.Max(0.2f, 1 - RelativeSeaLevelDistance);
            */

        }




    }
}
