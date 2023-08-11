using SkiaSharp;
using Vintagestory.API.Util;
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
            if (!Debug)
            {
                return;
            }

            SKBitmap bitmap = new SKBitmap(sizeX, sizeZ);
            for (int i = 0; i < sizeX; i++)
            {
                for (int j = 0; j < sizeZ; j++)
                {
                    int num = values[j * sizeX + i];
                    if (mode == DebugDrawMode.FirstByteGrayscale)
                    {
                        int num2 = num & 0xFF;
                        num = num2 | (num2 << 8) | (num2 << 16);
                    }

                    if (mode == DebugDrawMode.LandformRGB)
                    {
                        LandformVariant[] landFormsByIndex = NoiseLandforms.landforms.LandFormsByIndex;
                        if (landFormsByIndex.Length > num)
                        {
                            num = landFormsByIndex[num].ColorInt;
                        }
                    }

                    if (mode == DebugDrawMode.ProvinceRGB)
                    {
                        GeologicProvinceVariant[] variants = NoiseGeoProvince.provinces.Variants;
                        if (variants.Length > num)
                        {
                            num = variants[num].ColorInt;
                        }
                    }

                    bitmap.SetPixel(i, j,
                        new SKColor((byte)((num >> 16) & 0xFF), (byte)((num >> 8) & 0xFF), (byte)(num & 0xFF)));
                }
            }

            bitmap.Save("map-" + name + ".png");
        }

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
    }
}
