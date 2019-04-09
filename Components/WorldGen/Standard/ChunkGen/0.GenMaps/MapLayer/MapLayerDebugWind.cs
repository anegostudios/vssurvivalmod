using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vintagestory.ServerMods
{
    class MapLayerDebugWind : MapLayerBase
    {
        private NoiseWind windmap;

        public MapLayerDebugWind(long seed) : base(seed)
        {
            windmap = new NoiseWind(seed);

            //windmap.getWindAt(0.2, 0.1);
            //windmap.getWindAt(1.2, 0.1);
        }

        public override int[] GenLayer(int xCoord, int zCoord, int sizeX, int sizeZ)
        {
            int[] result = new int[sizeX*sizeZ];
            int drawLatticeSize = 16;
            float windZoom = 128;

            for (int x = 0; x < sizeX + drawLatticeSize; x += drawLatticeSize)
            {
                for (int z = 0; z < sizeZ + drawLatticeSize; z += drawLatticeSize)
                {
                    PolarVector vec = windmap.getWindAt(((float)xCoord + x)/ windZoom, ((float)zCoord + z)/ windZoom);

                    int dx = (int) (vec.length*Math.Cos(vec.angle));
                    int dz = (int) (vec.length*Math.Sin(vec.angle));

                    plotLine(result, sizeX, x, z, x + dx, z + dz);
                    if (x < sizeX && z < sizeZ)
                    {
                        result[z*sizeX + x] = 255 << 16;
                    }

                    
                    
                }
            }

            return result;
        }

        void plotLine(int[] map, int sizeX, int x0, int y0, int x1, int y1)
        {
            int dx = Math.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
            int dy = -Math.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
            int err = dx + dy, e2; /* error value e_xy */

            for (;;)
            {  /* loop */
                //setPixel(x0, y0);
                if (x0 >= 0 && x0 < sizeX && y0 >= 0 && y0 < sizeX)
                {
                    map[y0 * sizeX + x0] = 120 + (120 << 8) + (120 << 16);
                }


                if (x0 == x1 && y0 == y1) break;
                e2 = 2 * err;
                if (e2 >= dy) { err += dy; x0 += sx; } /* e_xy+e_x > 0 */
                if (e2 <= dx) { err += dx; y0 += sy; } /* e_xy+e_y < 0 */
            }
        }
    }
}
