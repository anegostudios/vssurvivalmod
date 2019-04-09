using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace Vintagestory.ServerMods
{
    public class GenFromHeightmap : ModSystem
    {
        ICoreServerAPI sapi;

        ushort[] heights;
        int width;
        int height;


        public override void StartServerSide(ICoreServerAPI api)
        {
            this.sapi = api;

            //api.Event.ChunkColumnGeneration(OnChunkColumnGen, EnumWorldGenPass.Terrain);
            api.Event.SaveGameLoaded += GameWorldLoaded;

            // Call our loaded method manually if the server is already running (happens when mods are reloaded at runtime)
            if (api.Server.CurrentRunPhase == EnumServerRunPhase.RunGame)
            {
                GameWorldLoaded();
            }
        }

        private void OnChunkColumnGen(IServerChunk[] chunks, int chunkX, int chunkZ)
        {
            
        }

        private void GameWorldLoaded()
        {
            
        }


        public void TryLoadHeightMap(string filename)
        {
            string folderPath = sapi.GetOrCreateDataPath("Heightmaps");
            string filePath = Path.Combine(folderPath, filename);

            if (!File.Exists(filePath))
            {
                return;
            }

            Bitmap bmp = new Bitmap(filePath);
            BitmapData bData1 = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadWrite, bmp.PixelFormat);

            heights = new ushort[bmp.Width * bmp.Height];

            unsafe
            {
                byte* bData1Scan0Ptr = (byte*)bData1.Scan0.ToPointer();
                byte* nextBase = bData1Scan0Ptr + bData1.Stride;

                for (int y = 0; y < bData1.Height; ++y)
                {
                    ushort* pRow = (ushort*)bData1Scan0Ptr;

                    for (int x = 0; x < bData1.Width; ++x)
                    {
                        heights[y * bData1.Width + x] = pRow[2];
                        //var red = pRow[2];
                        //var green = pRow[1];
                        //var blue = pRow[0];

                        pRow += 4;
                    }

                    bData1Scan0Ptr = nextBase;
                    nextBase += bData1.Stride;
                }
            }

            bmp.Dispose();
        }
    }
}
