using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    // We probably want to just transmit these maps as ushort[] blockids through the mapchunks (maybe rainheightmap suffices already?)
    // make a property block.BlockColor for the blocks color
    // and have the chunk intmap cached client side

    public class ChunkMapLayer : RGBMapLayer
    {
        int[] texDataTmp;
        int chunksize;
        IWorldChunk[] chunksTmp;

        object chunksToGenLock = new object();
        UniqueQueue<Vec2i> chunksToGen = new UniqueQueue<Vec2i>();
        Dictionary<Vec2i, MapComponent> loadedMapData = new Dictionary<Vec2i, MapComponent>();


        public override MapLegendItem[] LegendItems => throw new NotImplementedException();
        public override EnumMinMagFilter MinFilter => EnumMinMagFilter.Linear;
        public override EnumMinMagFilter MagFilter => EnumMinMagFilter.Nearest;
        public override string Title => "Terrain";
        public override EnumMapAppSide DataSide => EnumMapAppSide.Client;

        public ChunkMapLayer(ICoreAPI api, IWorldMapManager mapSink) : base(api, mapSink)
        {
            api.Event.OnChunkDirty += Event_OnChunkDirty;
        }

        private void Event_OnChunkDirty(Vec3i chunkCoord, IWorldChunk chunk, bool isNewChunk)
        {
            if (isNewChunk || !mapSink.IsOpened) return;

            if (!loadedMapData.ContainsKey(new Vec2i(chunkCoord.X, chunkCoord.Z))) return;

            lock (chunksToGenLock)
            {
                chunksToGen.Enqueue(new Vec2i(chunkCoord.X, chunkCoord.Z));
            }
        }

        public override void OnLoaded()
        {
            chunksize = api.World.BlockAccessor.ChunkSize;
            texDataTmp = new int[chunksize * chunksize];
            chunksTmp = new IWorldChunk[api.World.BlockAccessor.MapSizeY / chunksize];
        }

        public override void OnMapClosedClient()
        {
            foreach (MapComponent cmp in loadedMapData.Values)
            {
                cmp.Dispose();
            }

            loadedMapData.Clear();
            lock (chunksToGenLock)
            {
                chunksToGen.Clear();
            }
        }

        public override void OnOffThreadTick()
        {
            int quantityToGen = chunksToGen.Count;
            while (quantityToGen > 0)
            {
                quantityToGen--;
                Vec2i cord;

                lock (chunksToGenLock)
                {
                    if (chunksToGen.Count == 0) break;
                    cord = chunksToGen.Dequeue();
                }

                IMapChunk mc = api.World.BlockAccessor.GetMapChunk(cord);
                if (mc == null)
                {
                    lock (chunksToGenLock)
                    {
                        chunksToGen.Enqueue(cord);
                    }
                    continue;
                }

                int[] pixels = (int[])GenerateChunkImage(cord, mc)?.Clone();

                if (pixels == null)
                {
                    lock (chunksToGenLock)
                    {
                        chunksToGen.Enqueue(cord);
                    }
                    continue;
                }

                api.Event.EnqueueMainThreadTask(() =>
                {
                    if (loadedMapData.ContainsKey(cord))
                    {
                        mapSink.RemoveMapData(loadedMapData[cord]);
                    }

                    mapSink.AddMapData(loadedMapData[cord] = LoadMapData(cord, pixels));
                }, "chunkmaplayerready");
            }
        }

        public override void OnViewChangedClient(List<Vec2i> nowVisible, List<Vec2i> nowHidden)
        {
            lock (chunksToGenLock)
            {
                foreach (Vec2i cord in nowVisible)
                {
                    if (loadedMapData.ContainsKey(cord))
                    {
                        mapSink.AddMapData(loadedMapData[cord]);
                        continue;
                    }

                    chunksToGen.Enqueue(cord.Copy());
                }
            }

            foreach (Vec2i cord in nowHidden)
            {
                MapComponent mc = null;
                if (loadedMapData.TryGetValue(cord, out mc))
                {
                    mapSink.RemoveMapData(mc);
                }
            }
        }

        

        public MapComponent LoadMapData(Vec2i chunkCoord, int[] pixels)
        {
            ICoreClientAPI capi = api as ICoreClientAPI;
            int chunksize = api.World.BlockAccessor.ChunkSize;
            int textureId = capi.Render.LoadTextureFromRgba(
                pixels,
                api.World.BlockAccessor.ChunkSize,
                api.World.BlockAccessor.ChunkSize,
                false,
                0
            );
            
            ChunkMapComponent cmp = new ChunkMapComponent(capi, chunkCoord.Copy());
            cmp.Texture = new LoadedTexture(capi, textureId, chunksize, chunksize);

            return cmp;
        }


        public int[] GenerateChunkImage(Vec2i chunkPos, IMapChunk mc)
        {
            ICoreClientAPI capi = api as ICoreClientAPI;

            BlockPos tmpPos = new BlockPos();
            Vec2i localpos = new Vec2i();

            // Prefetch chunks
            for (int cy = 0; cy < chunksTmp.Length; cy++)
            {
                chunksTmp[cy] = capi.World.BlockAccessor.GetChunk(chunkPos.X, cy, chunkPos.Y);
                if (chunksTmp[cy] == null) return null;
            }


            for (int i = 0; i < texDataTmp.Length; i++)
            {
                int y = mc.RainHeightMap[i];
                
                MapUtil.PosInt2d(i, chunksize, localpos);

                chunksTmp[y / chunksize].Unpack();
                ushort blockId = chunksTmp[y / chunksize].Blocks[MapUtil.Index3d(localpos.X, y % chunksize, localpos.Y, chunksize, chunksize)];
                Block block = api.World.Blocks[blockId];

                tmpPos.Set(chunksize * chunkPos.X + localpos.X, y, chunksize * chunkPos.Y + localpos.Y);
                //Block block = api.World.BlockAccessor.GetBlock(tmpPos);
                
                texDataTmp[i] = block.GetBlockColor(capi, tmpPos) | 255 << 24;
            }

            for (int cy = 0; cy < chunksTmp.Length; cy++) chunksTmp[cy] = null;

            return texDataTmp;
        }

    }
}
