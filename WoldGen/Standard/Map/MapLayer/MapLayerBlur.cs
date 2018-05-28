using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vintagestory.ServerMods
{
    class MapLayerBlur : MapLayerBase
    {
        private int range;
        private MapLayerBase parent;

        public MapLayerBlur(long seed, MapLayerBase parent, int range) : base(seed)
        {
            if ((range & 1) == 0)
            {
                throw new InvalidOperationException("Range must be odd!");
            }

            this.parent = parent;
            this.range = range;
        }

        public override int[] GenLayer(int xCoord, int zCoord, int sizeX, int sizeZ)
        {
            int[] map = parent.GenLayer(xCoord, zCoord, sizeX, sizeZ);

            BoxBlurHorizontal(map, range, 0, 0, sizeX, sizeZ);
            BoxBlurVertical(map, range, 0, 0, sizeX, sizeZ);

            return map;
        }


        internal unsafe void BoxBlurHorizontal(int[] map, int range, int xStart, int yStart, int xEnd, int yEnd)
        {
            fixed (int* pixelsPre = map)
            {
                uint* pixels = (uint*)pixelsPre;

                int w = xEnd - xStart;
                int h = yEnd - yStart;

                int halfRange = range/2;
                int index = yStart*w;
                uint[] newColors = new uint[w];

                for (int y = yStart; y < yEnd; y++)
                {
                    int hits = 0;
                    int a = 0;
                    int r = 0;
                    int g = 0;
                    int b = 0;
                    for (int x = xStart - halfRange; x < xEnd; x++)
                    {
                        int oldPixel = x - halfRange - 1;
                        if (oldPixel >= xStart)
                        {
                            uint col = pixels[index + oldPixel];
                            if (col != 0)
                            {
                                a -= ((byte) (col >> 24));
                                r -= ((byte) (col >> 16));
                                g -= ((byte) (col >> 8));
                                b -= ((byte) col);
                            }
                            hits--;
                        }

                        int newPixel = x + halfRange;
                        if (newPixel < xEnd)
                        {
                            uint col = pixels[index + newPixel];
                            if (col != 0)
                            {
                                a += ((byte) (col >> 24));
                                r += ((byte) (col >> 16));
                                g += ((byte) (col >> 8));
                                b += ((byte) col);
                            }
                            hits++;
                        }

                        if (x >= xStart)
                        {
                            uint color = (uint) (
                                ((byte) (a/hits) << 24)
                                | ((byte) (r/hits) << 16)
                                | ((byte) (g/hits) << 8)
                                | ((byte) (b/hits))
                                );

                            newColors[x] = color;
                        }
                    }

                    for (int x = xStart; x < xEnd; x++)
                    {
                        pixels[index + x] = newColors[x];
                    }

                    index += w;
                }
            }
        }

        internal unsafe void BoxBlurVertical(int[] map, int range, int xStart, int yStart, int xEnd, int yEnd)
        {
            fixed (int* pixelsPre = map)
            {
                uint* pixels = (uint*)pixelsPre;

                int w = xEnd - xStart;
                int h = yEnd - yStart;

                int halfRange = range/2;

                uint[] newColors = new uint[h];
                int oldPixelOffset = -(halfRange + 1)*w;
                int newPixelOffset = (halfRange)*w;

                for (int x = xStart; x < xEnd; x++)
                {
                    int hits = 0;
                    int a = 0;
                    int r = 0;
                    int g = 0;
                    int b = 0;
                    int index = yStart*w - halfRange*w + x;
                    for (int y = yStart - halfRange; y < yEnd; y++)
                    {
                        int oldPixel = y - halfRange - 1;
                        if (oldPixel >= yStart)
                        {
                            uint col = pixels[index + oldPixelOffset];
                            if (col != 0)
                            {
                                a -= ((byte) (col >> 24));
                                r -= ((byte) (col >> 16));
                                g -= ((byte) (col >> 8));
                                b -= ((byte) col);
                            }
                            hits--;
                        }

                        int newPixel = y + halfRange;
                        if (newPixel < yEnd)
                        {
                            uint col = pixels[index + newPixelOffset];
                            if (col != 0)
                            {
                                a += ((byte) (col >> 24));
                                r += ((byte) (col >> 16));
                                g += ((byte) (col >> 8));
                                b += ((byte) col);
                            }
                            hits++;
                        }

                        if (y >= yStart)
                        {
                            uint color = (uint) (
                                ((byte) (a/hits) << 24)
                                | ((byte) (r/hits) << 16)
                                | ((byte) (g/hits) << 8)
                                | ((byte) (b/hits))
                                );

                            newColors[y] = color;
                        }

                        index += w;
                    }

                    for (int y = yStart; y < yEnd; y++)
                    {
                        pixels[y*w + x] = newColors[y];
                    }
                }
            }
        }

      
    }
}
