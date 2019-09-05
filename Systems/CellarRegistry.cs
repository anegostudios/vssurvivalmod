using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Vintagestory.GameContent
{
    public class Cellar
    {
        public int CoolingWallCount;
        public int NonCoolingWallCount;
    }

    public class ChunkCellars {
        public Dictionary<BlockPos, Cellar> CellarByPosition = new Dictionary<BlockPos, Cellar>();
    }

    public class CellarRegistry : ModSystem
    {
        protected Dictionary<long, ChunkCellars> CellarsByChunkIndex = new Dictionary<long, ChunkCellars>();

        int chunksize;
        int chunkMapSizeX;
        int chunkMapSizeZ;

        ICoreAPI api;

        public override bool ShouldLoad(EnumAppSide forSide)
        {
            return true;
        }

        public override void Start(ICoreAPI api)
        {
            base.Start(api);
            this.api = api;

            api.Event.ChunkDirty += Event_ChunkDirty;
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            api.Event.BlockTexturesLoaded += init;
        }
        public override void StartServerSide(ICoreServerAPI api)
        {
            api.Event.SaveGameLoaded += init;
        }
        private void init()
        {
            chunksize = this.api.World.BlockAccessor.ChunkSize;
            chunkMapSizeX = api.World.BlockAccessor.MapSizeX / chunksize;
            chunkMapSizeZ = api.World.BlockAccessor.MapSizeZ / chunksize;
        }


        private void Event_ChunkDirty(Vec3i chunkCoord, IWorldChunk chunk, bool isNewChunk)
        {
            long index3d = MapUtil.Index3dL(chunkCoord.X, chunkCoord.Y, chunkCoord.Z, chunkMapSizeX, chunkMapSizeZ);
            CellarsByChunkIndex.Remove(index3d);
        }

        public Cellar GetCellarForPosition(BlockPos pos)
        {
            long index3d = MapUtil.Index3dL(pos.X / chunksize, pos.Y / chunksize, pos.Z / chunksize, chunkMapSizeX, chunkMapSizeZ);

            ChunkCellars cellars = null;
            Cellar cellar = null;
            if (CellarsByChunkIndex.TryGetValue(index3d, out cellars))
            {
                if (cellars.CellarByPosition.TryGetValue(pos, out cellar))
                {
                    return cellar;
                }

                cellar = FindCellarForPosition(pos);
                cellars.CellarByPosition[pos] = cellar;
                return cellar;
            }

            cellar = FindCellarForPosition(pos);
            CellarsByChunkIndex[index3d] = new ChunkCellars();
            CellarsByChunkIndex[index3d].CellarByPosition[pos] = cellar;
            return cellar;
        }


        private Cellar FindCellarForPosition(BlockPos pos)
        {
            HashSet<BlockPos> visitedPositions = new HashSet<BlockPos>();
            Queue<BlockPos> bfsQueue = new Queue<BlockPos>();
            bfsQueue.Enqueue(pos);

            int maxHalfSize = 6;

            int coolingWallCount = 0;
            int nonCoolingWallCount = 0;

            while (bfsQueue.Count > 0)
            {
                BlockPos bpos = bfsQueue.Dequeue();
                
                foreach (BlockFacing facing in BlockFacing.ALLFACES)
                {
                    BlockPos npos = bpos.AddCopy(facing);
                    Block nBlock = api.World.BlockAccessor.GetBlock(npos);

                    // We hit a wall, no need to scan further
                    if (nBlock.SideSolid[facing.GetOpposite().Index])
                    {
                        if (nBlock.BlockMaterial == EnumBlockMaterial.Stone || nBlock.BlockMaterial == EnumBlockMaterial.Soil || nBlock.BlockMaterial == EnumBlockMaterial.Ceramic) coolingWallCount++;
                        else nonCoolingWallCount++;
                        
                        continue;
                    }

                    // We hit a door or trapdoor - stop, but penalty!
                    if (nBlock.Code.Path.Contains("door"))
                    {
                        nonCoolingWallCount+=3;
                        continue;
                    }
                    
                    // Only traverse within an 8x8x8 block cube
                    bool inCube = Math.Abs(npos.X - pos.X) <= maxHalfSize && Math.Abs(npos.Y - pos.Y) <= maxHalfSize && Math.Abs(npos.Z - pos.Z) <= maxHalfSize;

                    // Not a cellar. 
                    if (!inCube)
                    {
                        return null;
                    }

                    if (!visitedPositions.Contains(npos))
                    {
                        bfsQueue.Enqueue(npos);
                        visitedPositions.Add(npos);
                    }
                }
            }

            return new Cellar()
            {
                CoolingWallCount = coolingWallCount,
                NonCoolingWallCount = nonCoolingWallCount
            };
        }
    }
}
