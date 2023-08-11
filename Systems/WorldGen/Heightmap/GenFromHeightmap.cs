using System.IO;
using SkiaSharp;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace Vintagestory.ServerMods
{
    public class GenFromHeightmap : ModSystem
    {
        ICoreServerAPI sapi;

        ushort[] heights;
        //int width;
        //int height;


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


        public unsafe void TryLoadHeightMap(string filename)
        {
            string text = Path.Combine(sapi.GetOrCreateDataPath("Heightmaps"), filename);
            if (!File.Exists(text))
            {
                return;
            }

            SKBitmap bitmap = SKBitmap.Decode(text);
            heights = new ushort[bitmap.Width * bitmap.Height];
            byte* ptr = (byte*)bitmap.GetPixels().ToPointer();
            byte* ptr2 = ptr + bitmap.RowBytes;
            for (int i = 0; i < bitmap.Height; i++)
            {
                ushort* ptr3 = (ushort*)ptr;
                for (int j = 0; j < bitmap.Width; j++)
                {
                    heights[i * bitmap.Width + j] = ptr3[2];
                    ptr3 += 4;
                }

                ptr = ptr2;
                ptr2 += bitmap.RowBytes;
            }

            bitmap.Dispose();
        }
    }
}
