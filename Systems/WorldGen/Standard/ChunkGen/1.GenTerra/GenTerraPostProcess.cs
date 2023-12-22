using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Vintagestory.ServerMods
{
    public class GenTerraPostProcess : ModStdWorldGen
    {
        ICoreServerAPI api;
        IWorldGenBlockAccessor blockAccessor;

        HashSet<int> chunkVisitedNodes = new HashSet<int>();
        List<int> solidNodes = new List<int>(40);

        public override bool ShouldLoad(EnumAppSide side)
        {
            return side == EnumAppSide.Server;
        }


        public override double ExecuteOrder()
        {
            return 0.01;
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            this.api = api;

            if (TerraGenConfig.DoDecorationPass)
            {
                api.Event.ChunkColumnGeneration(OnChunkColumnGen, EnumWorldGenPass.TerrainFeatures, "standard");
                api.Event.GetWorldgenBlockAccessor(OnWorldGenBlockAccessor);
            }
        }
        

        private void OnWorldGenBlockAccessor(IChunkProviderThread chunkProvider)
        {
            blockAccessor = chunkProvider.GetBlockAccessor(true);
        }



        private void OnChunkColumnGen(IChunkColumnGenerateRequest request)
        {
            var chunks = request.Chunks;
            int chunkX = request.ChunkX;
            int chunkZ = request.ChunkZ;

            blockAccessor.BeginColumn();
            int seaLevel = TerraGenConfig.seaLevel - 1;
            const int chunksize = GlobalConstants.ChunkSize;
            const int chunksizeSquared = chunksize * chunksize;
            int chunkY = seaLevel / chunksize;
            int yMax = chunks[0].MapChunk.YMax;
            int cyMax = Math.Min(yMax / chunksize + 1, api.World.BlockAccessor.MapSizeY / chunksize);
            chunkVisitedNodes.Clear();

            for (int cy = chunkY; cy < cyMax; cy++)
            {
                IChunkBlocks chunkdata = chunks[cy].Data;

                int yStart = cy == 0 ? 1 : 0;  // Prevents attempts to get a blockbelow with y == -1 - impossible unless seaLeavel is set to 0
                int baseY = cy * chunksize;
                if (baseY < seaLevel)
                {
                    yStart = seaLevel - baseY;  // Can't be more than chunksize below seaLevel, because of chunkY's initial value
                }
                int yEnd = chunksize - 1;
                if (baseY + yEnd > yMax)
                {
                    yEnd = yMax - baseY;
                }

                for (int baseindex3d = 0; baseindex3d < chunksizeSquared; baseindex3d++)
                {
                    int blockIdBelow;
                    int index3d = baseindex3d + (yStart - 1) * chunksizeSquared;

                    if (yStart == 0)
                    {
                        blockIdBelow = chunks[cy - 1].Data.GetBlockIdUnsafe(index3d + chunksize * chunksizeSquared);
                    }
                    else
                    {
                        blockIdBelow = chunkdata.GetBlockIdUnsafe(index3d);
                    }

                    for (int y = yStart; y <= yEnd; y++)
                    {
                        index3d += chunksizeSquared;
                        int blockId = chunkdata.GetBlockIdUnsafe(index3d);
                        if (blockId != 0 && blockIdBelow == 0)
                        {
                            int x = baseindex3d % chunksize;
                            int z = baseindex3d / chunksize;
                            if (!chunkVisitedNodes.Contains(index3d)) deletePotentialFloatingBlocks(chunkX * chunksize + x, baseY + y, chunkZ * chunksize + z);
                        }
                        blockIdBelow = blockId;  // ready for the next iteration in the loop
                    }
                }
            }
        }



        QueueOfInt bfsQueue = new QueueOfInt();
        const int ARRAYSIZE = 41;  // Note if this constant is increased beyond 64, the bitshifts for compressedPos in the bfsQueue.Enqueue() and .Dequeue() calls may need updating
        readonly int[] currentVisited = new int[ARRAYSIZE * ARRAYSIZE * ARRAYSIZE];
        int iteration = 0;

        /// <summary>
        /// Very similar to RoomRegistry room search code: we will search for contiguous islands of blocks, surrounded by air on all sides
        /// </summary>
        private void deletePotentialFloatingBlocks(int X, int Y, int Z)
        {
            int halfSize = (ARRAYSIZE - 1) / 2;
            solidNodes.Clear();
            bfsQueue.Clear();
            int compressedPos = halfSize << 12 | halfSize << 6 | halfSize;
            bfsQueue.Enqueue(compressedPos);
            solidNodes.Add(compressedPos);

            int iteration = ++this.iteration;
            int visitedIndex = (halfSize * ARRAYSIZE + halfSize) * ARRAYSIZE + halfSize; // Center node
            currentVisited[visitedIndex] = iteration;

            int baseX = X - halfSize;
            int baseY = Y - halfSize;
            int baseZ = Z - halfSize;
            BlockPos npos = new BlockPos();
            int dx, dy, dz;

            int worldHeight = api.World.BlockAccessor.MapSizeY;
            int curVisitedNodes = 1;
            while (bfsQueue.Count > 0)
            {
                compressedPos = bfsQueue.Dequeue();
                dx = compressedPos >> 12;
                dy = (compressedPos >> 6) & 0x3f;
                dz = compressedPos & 0x3f;
                npos.Set(baseX + dx, baseY + dy, baseZ + dz);

                foreach (BlockFacing facing in BlockFacing.ALLFACES)
                {
                    facing.IterateThruFacingOffsets(npos);  // This must be the first command in the loop, to ensure all facings will be properly looped through regardless of any 'continue;' statements
                    if (npos.Y >= worldHeight) continue;

                    // Compute the new dx, dy, dz offsets for npos
                    dx = npos.X - baseX;
                    dy = npos.Y - baseY;
                    dz = npos.Z - baseZ;

                    visitedIndex = (dx * ARRAYSIZE + dy) * ARRAYSIZE + dz;
                    if (currentVisited[visitedIndex] == iteration) continue;   // continue if block position was already visited
                    currentVisited[visitedIndex] = iteration;  // mark this as visited so we don't check it more than once

                    int nBlock = blockAccessor.GetBlockId(npos.X, npos.Y, npos.Z);
                    if (nBlock == 0)
                    {
                        continue;   // continue if air, we are looking for solid blocks only  (NOTE: accessing outside generating chunks will also look like air here)
                    }

                    int newCompressedPos = dx << 12 | dy << 6 | dz;

                    // If more than 40 solid blocks together, either it is connected to base terrain or it's a large floating island which is OK
                    if (++curVisitedNodes > 40)
                    {
                        // Cheap test on block below - no point in adding to chunkVisitedNodes if solid block below, as chunkVisitedNodes will be tested only when there is air below
                        if (!solidNodes.Contains(newCompressedPos - 64))
                        {
                            AddToChunkVisitedNodesIfSameChunk(npos.X, npos.Y, npos.Z, X, Y, Z);
                        }
                        foreach (int compPos in solidNodes)
                        {
                            // Again, no point in adding to chunkVisitedNodes if solid block below
                            if (!solidNodes.Contains(compPos - 64))
                            {
                                dx = compPos >> 12;
                                dy = (compPos >> 6) & 0x3f;
                                dz = compPos & 0x3f;
                                AddToChunkVisitedNodesIfSameChunk(baseX + dx, baseY + dy, baseZ + dz, X, Y, Z);
                            }
                        }
                        return;
                    }

                    // Solid block: continue iterating, and add this block for further action
                    solidNodes.Add(newCompressedPos);
                    bfsQueue.Enqueue(newCompressedPos);
                }
            }


            // We found a free floating section of blocks that's less that 20 blocks
            foreach (int compPos in solidNodes)
            {
                dx = compPos >> 12;
                dy = (compPos >> 6) & 0x3f;
                dz = compPos & 0x3f;

                npos.Set(baseX + dx, baseY + dy, baseZ + dz);
                blockAccessor.SetBlock(0, npos);
            }
        }

        private void AddToChunkVisitedNodesIfSameChunk(int nposX, int nposY, int nposZ, int origX, int origY, int origZ)
        {
            // Only if this solid block is above x,y,z position, or on same level and north or east, add to chunkVisitedNodes to be filtered out of future testing
            if (nposY < origY) return;
            if (nposY == origY)
            {
                if (nposZ < origZ) return;
                if (nposZ == origZ && nposX < origX) return;
            }

            // Don't add anything unless it is in the current chunk
            const int chunksize = GlobalConstants.ChunkSize;
            const int chunkMask = ~(chunksize - 1);
            if (((nposX ^ origX) & chunkMask) != 0) return;
            if (((nposZ ^ origZ) & chunkMask) != 0) return;
            if (((nposY ^ origY) & chunkMask) != 0) return;

            const int inChunkMask = chunksize - 1;
            int index3d = ((nposY & inChunkMask) * chunksize + (nposZ & inChunkMask)) * chunksize + (nposX & inChunkMask);
            chunkVisitedNodes.Add(index3d);
        }

        //struct VisitNode : IEquatable<VisitNode>
        //{
        //    public int X, Y, Z;

        //    public VisitNode(int x, int y, int z)
        //    {
        //        this.X = x;
        //        this.Y = y;
        //        this.Z = z;
        //    }

        //    public bool Equals(VisitNode other)
        //    {
        //        return X == other.X && Y == other.Y && Z == other.Z;
        //    }
        //}
    }
}
