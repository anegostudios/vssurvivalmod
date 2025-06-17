using System;

#nullable disable

namespace Vintagestory.ServerMods
{

    public class TerraGenConfig
    {
        #region WorldGen

        public static double CavesPerChunkColumn = 0.37;

        /// <summary>
        /// Amount of octaves used by terrain gen
        /// </summary>
        protected static int terrainGenOctaves = 9;

        public static int GetTerrainOctaveCount(int worldheight)
        {
            return terrainGenOctaves + (worldheight - 256) / 128;
        }

        /// <summary>
        /// Horizontal lerp of the 3d terrain noise
        /// </summary>
        public const int lerpHorizontal = 4;
        /// <summary>
        /// Vertical lerp of the 3d terrain noise
        /// </summary>
        public const int lerpVertical = 8;

        public static int WaterFreezingTempOnGen = -15;


        public static double terrainNoiseVerticalScale = 2.0;

        public static int rockStrataScale = 16;
        public static int rockStrataOctaveScale = 32;

        public static int shrubMapScale = 16;
        public static int forestMapScale = 32;
        public static int geoUpheavelMapScale = 64;
        public static int oceanMapScale = 32;

        public static int blockPatchesMapScale = 32;

        public static int forestMapPadding = 0;

        public static int beachMapScale = 16;
        public static int beachMapPadding = 0;

        public static int climateMapWobbleScale = 192;
        public static int climateMapScale = 32;
        public static int climateMapSubScale = 16;

        public static int oreMapWobbleScale = 110;
        public static int oreMapScale = 16;
        public static int oreMapSubScale = 16; // was 12, but that does not properly align then

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

        public static bool GenerateVegetation = true;
        public static bool GenerateStructures = true;
        public static bool DoDecorationPass = true;

        //internal static int depositDistortionScale = 2;

        #endregion

        public static float SoilThickness(float rainRel, float temp, int distToSealevel, float thicknessVar) {

            float f = 8f * thicknessVar;

            float weight = 1 - Math.Max(0, (4 - f) / 4);

            return  f - distToSealevel / 35 * weight ;
        }
    }
}
