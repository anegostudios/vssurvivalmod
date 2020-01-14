using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.ServerMods.NoObf;

namespace Vintagestory.ServerMods
{
    public enum DebugDrawMode
    {
        RGB = 0,
        FirstByteGrayscale = 1,
        LandformRGB = 2,
        ProvinceRGB = 3
    }

    public abstract class NoiseBase
    {
        public static bool Debug = false;
        public static int DebugXCoord = 0;
        public static int DebugZCoord = 0;

        internal long worldSeed;
        internal long mapGenSeed;
        internal long currentSeed;

        public NoiseBase(long worldSeed)
        {
            this.worldSeed = worldSeed;

            currentSeed = mapGenSeed;
            currentSeed = currentSeed * 6364136223846793005L + 1442695040888963407L;

            mapGenSeed = worldSeed;
            mapGenSeed *= worldSeed * 6364136223846793005L + 1442695040888963407L;
            mapGenSeed += 1;
            mapGenSeed *= worldSeed * 6364136223846793005L + 1442695040888963407L;
            mapGenSeed += 2;
            mapGenSeed *= worldSeed * 6364136223846793005L + 1442695040888963407L;
            mapGenSeed += 3;
        }

        #region RNG
        public void InitPositionSeed(int xPos, int zPos)
        {
            currentSeed = mapGenSeed;
            currentSeed *= currentSeed*6364136223846793005L + 1442695040888963407L;
            currentSeed += xPos;
            currentSeed *= currentSeed * 6364136223846793005L + 1442695040888963407L;
            currentSeed += zPos;
            currentSeed *= currentSeed * 6364136223846793005L + 1442695040888963407L;
            currentSeed += xPos;
            currentSeed *= currentSeed * 6364136223846793005L + 1442695040888963407L;
            currentSeed += zPos;
        }

        /// <summary>
        /// Returns a pseudo random number from 0 - max (excluding max)
        /// </summary>
        /// <param name="max"></param>
        /// <returns></returns>
        public int NextInt(int max)
        {
            int r = (int)((currentSeed >> 24) % max);

            if (r < 0)
            {
                r += max;
            }

            currentSeed *= currentSeed * 6364136223846793005L + 1442695040888963407L;
            currentSeed += mapGenSeed;

            return r;
        }


        /// <summary>
        /// Returns a pseudo random number masked with given mask. This is a bit faster to calculate and useful if you need number until a max occupying all bits
        /// e.g. 0-31 or 0-255
        /// </summary>
        /// <param name="max"></param>
        /// <returns></returns>
        public int NextIntFast(int mask)
        {
            int r = (int)(currentSeed & mask);
            currentSeed = currentSeed * 6364136223846793005L + 1442695040888963407L;
            return r;
        }
        #endregion

        #region Probably useless?

        public int[] CutMargins(int[] inInts, int sizeX, int sizeZ, int margin)
        {
            int[] resultInts = new int[(sizeX - 2 * margin) * (sizeZ - 2 * margin)];
            int j = 0;

            for (int i = 0; i < inInts.Length; i++)
            {
                int xpos = i % sizeX;
                int zpos = i / sizeX;


                if (xpos >= margin && xpos < sizeX - margin && zpos >= margin && zpos < sizeZ - margin)
                {
                    resultInts[j++] = inInts[i];
                }
            }

            return resultInts;
        }


        public int[] CutRightAndBottom(int[] inInts, int sizeX, int sizeZ, int margin)
        {
            int[] resultInts = new int[(sizeX - margin) * (sizeZ - margin)];
            int j = 0;

            for (int i = 0; i < inInts.Length; i++)
            {
                int xpos = i % sizeX;
                int zpos = i / sizeX;


                if (xpos < sizeX - margin && zpos < sizeZ - margin)
                {
                    resultInts[j++] = inInts[i];
                }
            }

            return resultInts;
        }

        #endregion





        // C# modulo doesn't work on negative numbers
        /*public static int mod(int a, int n)
        {
            int result = a % n;
            if ((a < 0 && n > 0) || (a > 0 && n < 0))
                result += n;
            return result;
        }*/




        // mode = 0   normal rgb image
        // mode = 1   use lowest 8 bits to create a grayscale image
        // mode = 2   decode rgb value into landform rgb value
        // mode = 3   decode rgb value into province rgb value

        public static void DebugDrawBitmap(DebugDrawMode mode, int[] values, int size, string name)
        {
            DebugDrawBitmap(mode, values, size, size, name);
        }

        public static void DebugDrawBitmap(DebugDrawMode mode, int[] values, int sizeX, int sizeZ, string name)
        {
            if (!Debug) return;

            Bitmap bmp = new Bitmap(sizeX, sizeZ);
            int color;
            for (int x = 0; x < sizeX; x++)
            {
                for (int z = 0; z < sizeZ; z++)
                {
                    color = values[z * sizeX + x];

                    if (mode == DebugDrawMode.FirstByteGrayscale)
                    {
                        int val = color & 0xff;
                        color = (val) | (val << 8) | (val << 16);
                    }

                    if (mode == DebugDrawMode.LandformRGB)
                    {
                        LandformVariant[] landformsByIndex = NoiseLandforms.landforms.LandFormsByIndex;

                        if (landformsByIndex.Length > color)
                        {
                            color = landformsByIndex[color].ColorInt;
                        }

                    }

                    if (mode == DebugDrawMode.ProvinceRGB)
                    {
                        GeologicProvinceVariant[] provincesByIndex = NoiseGeoProvince.provinces.Variants;

                        if (provincesByIndex.Length > color)
                        {
                            color = provincesByIndex[color].ColorInt;
                        }

                    }

                    bmp.SetPixel(x, z, Color.FromArgb((color >> 16) & 0xff, (color >> 8) & 0xff, color & 0xff));
                }
            }

            bmp.Save("map-" + name + ".png");
        }



    }
}
