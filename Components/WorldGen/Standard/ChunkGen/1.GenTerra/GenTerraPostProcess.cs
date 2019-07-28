using System;
using System.Collections.Generic;
using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.ServerMods.NoObf;

namespace Vintagestory.ServerMods
{
    public class GenTerraPostProcess : ModStdWorldGen
    {
        ICoreServerAPI api;
        IBlockAccessor blockAccessor;

        HashSet<VisitNode> chunkVisitedNodes = new HashSet<VisitNode>();
        HashSet<VisitNode> curVisitedNodes = new HashSet<VisitNode>();

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

            if (DoDecorationPass)
            {
                api.Event.ChunkColumnGeneration(OnChunkColumnGen, EnumWorldGenPass.TerrainFeatures, "standard");
                api.Event.GetWorldgenBlockAccessor(OnWorldGenBlockAccessor);
            }
        }
        

        private void OnWorldGenBlockAccessor(IChunkProviderThread chunkProvider)
        {
            blockAccessor = chunkProvider.GetBlockAccessor(true);
            chunksize = api.WorldManager.ChunkSize;
        }



        private void OnChunkColumnGen(IServerChunk[] chunks, int chunkX, int chunkZ, ITreeAttribute chunkGenParams = null)
        {
            int seaLevel = TerraGenConfig.seaLevel - 1;
            int chunkY = seaLevel / chunksize;
            chunkVisitedNodes.Clear();

            for (int cy = chunkY; cy < chunks.Length; cy++)
            {
                IServerChunk chunk = chunks[cy];
                int index3d = 0;
                if (cy * chunksize < seaLevel)
                {
                    index3d = (seaLevel - cy * chunksize) * chunksize * chunksize;
                }

                for (; index3d < chunk.Blocks.Length; index3d++)
                {
                    int blockId = chunk.Blocks[index3d];
                    if (blockId == 0) continue;

                    
                    // Check up to 5 blocks below if there is air
                    for (int i = 1; i < 5; i++)
                    {
                        int index3dBelow = index3d - chunksize * chunksize;
                        int curcy = cy;

                        if (index3dBelow < 0) {
                            index3dBelow += chunksize * chunksize * chunksize;
                            curcy--;
                        }

                        blockId = chunks[curcy].Blocks[index3dBelow];

                        if (blockId == 0)
                        {
                            deletePotentialFloatingBlocks(index3d, chunkX, cy, chunkZ);
                            break;
                        }
                    }
                }
            }
        }


        
        private void deletePotentialFloatingBlocks(int index3d, int chunkX, int chunkY, int chunkZ)
        {
            curVisitedNodes.Clear();

            //blockAccessor.SetBlock(61, new BlockPos(dx + chunkX * chunksize, dy + chunkY * chunksize, dz + chunkZ * chunksize));
            int baseX = chunkX * chunksize;
            int baseY = chunkY * chunksize;
            int baseZ = chunkZ * chunksize;

            int dx = index3d % chunksize;
            int dy = index3d / chunksize / chunksize;
            int dz = (index3d / chunksize) % chunksize;

            VisitNode basenode = new VisitNode(baseX + dx, baseY + dy, baseZ + dz);
            if (chunkVisitedNodes.Contains(basenode)) return;

            Queue<VisitNode> toVisit = new Queue<VisitNode>();
            toVisit.Enqueue(basenode);

            int foundBlocks = 0;


            while (toVisit.Count > 0)
            {
                VisitNode node = toVisit.Dequeue();
                foundBlocks++;

                curVisitedNodes.Add(node);
                
                for (int faceIndex = 0; faceIndex < BlockFacing.ALLFACES.Length; faceIndex++)
                {
                    Vec3i vec = BlockFacing.ALLFACES[faceIndex].Normali;
                    VisitNode nnode = new VisitNode(node.X + vec.X, node.Y + vec.Y, node.Z + vec.Z);

                    if (curVisitedNodes.Contains(nnode)) continue;
                    if (chunkVisitedNodes.Contains(nnode)) return;

                    if (blockAccessor.GetBlockId(nnode.X, nnode.Y, nnode.Z) != 0)
                    {
                        toVisit.Enqueue(nnode);
                    }
                }

                if (curVisitedNodes.Count > 20)
                {
                    foreach (var val in curVisitedNodes) chunkVisitedNodes.Add(val);
                    return;
                }
            }

            // We found a free floating section of blocks that's less that 10 blocks
            if (curVisitedNodes.Count < 20)
            {
                foreach (VisitNode fnode in curVisitedNodes)
                {
                    blockAccessor.SetBlock(0, new BlockPos(fnode.X, fnode.Y, fnode.Z));
                }

            }
        }


        struct VisitNode : IEquatable<VisitNode>
        {
            public int X, Y, Z;

            public VisitNode(int x, int y, int z)
            {
                this.X = x;
                this.Y = y;
                this.Z = z;
            }

            public bool Equals(VisitNode other)
            {
                return X == other.X && Y == other.Y && Z == other.Z;
            }
        }
    }
}
