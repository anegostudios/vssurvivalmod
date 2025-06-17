using System;
using System.Collections.Generic;
using System.Threading;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.ServerMods;

#nullable disable

namespace Vintagestory.GameContent
{
    public class ProPickWorkSpace
    {
        public Dictionary<string, string> pageCodes = new Dictionary<string, string>();
        public Dictionary<string, DepositVariant> depositsByCode = new Dictionary<string, DepositVariant>();

        GenRockStrataNew rockStrataGen;
        GenDeposits depositGen;
        ICoreServerAPI sapi;


        public virtual void OnLoaded(ICoreAPI api)
        {
            if (api.Side == EnumAppSide.Client) return;

            ICoreServerAPI sapi = api as ICoreServerAPI;
            this.sapi = sapi;

            rockStrataGen = new GenRockStrataNew();
            rockStrataGen.setApi(sapi);

            TyronThreadPool.QueueTask(() => {   // Initialise off-thread, instead of adding 1-2 seconds to game launch time
                rockStrataGen.initWorldGen();
                GenDeposits deposits = new GenDeposits();
                deposits.addHandbookAttributes = false;
                deposits.setApi(sapi);
                deposits.initAssets(sapi, false);
                deposits.initWorldGen();
                depositGen = deposits;
            }, "propickonloaded");

            sapi.Event.ServerRunPhase(EnumServerRunPhase.RunGame, () =>
            {
                DepositVariant[] deposits = api.ModLoader.GetModSystem<GenDeposits>()?.Deposits;
                if (deposits == null) return;

                for (int i = 0; i < deposits.Length; i++)
                {
                    DepositVariant variant = deposits[i];

                    if (variant.WithOreMap)
                    {
                        pageCodes[variant.Code] = variant.HandbookPageCode;
                        depositsByCode[variant.Code] = variant;
                        if (variant.HandbookPageCode == null)
                        {
                            api.World.Logger.Warning("Deposit " + variant.Code + " has no handbook page code. Links created by the prospecting pick will not work without it.");
                        }
                    }

                    for (int k = 0; variant.ChildDeposits != null && k < variant.ChildDeposits.Length; k++)
                    {
                        DepositVariant childVariant = variant.ChildDeposits[k];
                        if (!childVariant.WithOreMap) continue;

                        if (childVariant.HandbookPageCode == null)
                        {
                            api.World.Logger.Warning("Child Deposit " + childVariant.Code + " of deposit " + variant.Code + " has no handbook page code. Links created by the prospecting pick will not work without it.");
                        }

                        pageCodes[childVariant.Code] = childVariant.HandbookPageCode;
                        depositsByCode[childVariant.Code] = childVariant;
                    }
                }
            });
        }


        public void Dispose(ICoreAPI api)
        {
            if (sapi != null)
            {
                pageCodes = null;
                depositsByCode = null;
                rockStrataGen?.Dispose();
                rockStrataGen = null;
                depositGen?.Dispose();
                depositGen = null;

                sapi = null;
            }
        }


        // Tyrons Brute force way of getting the correct reading for a rock strata column
        public virtual int[] GetRockColumn(int posX, int posZ)
        {
            const int chunksize = GlobalConstants.ChunkSize;
            DummyChunk[] chunks = new DummyChunk[sapi.World.BlockAccessor.MapSizeY / chunksize];
            int chunkX = posX / chunksize;
            int chunkZ = posZ / chunksize;
            int lx = posX % chunksize;
            int lz = posZ % chunksize;

            IMapChunk mapchunk = sapi.World.BlockAccessor.GetMapChunk(new Vec2i(chunkX, chunkZ));

            for (int chunkY = 0; chunkY < chunks.Length; chunkY++)
            {
                chunks[chunkY] = new DummyChunk(chunksize);
                chunks[chunkY].MapChunk = mapchunk;
                chunks[chunkY].chunkY = chunkY;
            }

            int surfaceY = mapchunk.WorldGenTerrainHeightMap[lz * chunksize + lx];
            for (int y = 0; y < surfaceY; y++)
            {
                int chunkY = y / chunksize;
                int lY = y - chunkY * chunksize;
                int localIndex3D = (chunksize * lY + lz) * chunksize + lx;

                chunks[chunkY].Blocks[localIndex3D] = rockStrataGen.rockBlockId;
            }

            rockStrataGen.preLoad(chunks, chunkX, chunkZ);
            rockStrataGen.genBlockColumn(chunks, chunkX, chunkZ, lx, lz);

            if (depositGen == null)
            {
                // Wait for off-thread initialisation to finish (should be well finished by the time any player is able to use a ProPick, but let's make sure)
                int timeOutCount = 100;
                while (depositGen == null && timeOutCount-- > 0) Thread.Sleep(30);
                if (depositGen == null) throw new NullReferenceException("Prospecting Pick internal ore generator was not initialised, likely due to an exception during earlier off-thread worldgen");
            }
            depositGen.GeneratePartial(chunks, chunkX, chunkZ, 0, 0);


            int[] rockColumn = new int[surfaceY];

            for (int y = 0; y < surfaceY; y++)
            {
                int chunkY = y / chunksize;
                int lY = y - chunkY * chunksize;
                int localIndex3D = (chunksize * lY + lz) * chunksize + lx;

                rockColumn[y] = chunks[chunkY].Blocks[localIndex3D];
            }

            return rockColumn;
        }

        public class DummyChunk : IServerChunk
        {
            public int chunkY;
            public IMapChunk MapChunk { get; set; }
            IChunkBlocks IWorldChunk.Data => Blocks;
            IChunkLight IWorldChunk.Lighting => throw new NotImplementedException();
            public IChunkBlocks Blocks;

            public DummyChunk(int chunksize)
            {
                Blocks = new DummyChunkData(chunksize);
            }

            public class DummyChunkData : IChunkBlocks
            {
                public int[] blocks;


                public DummyChunkData(int chunksize)
                {
                    blocks = new int[chunksize * chunksize * chunksize];
                }

                public int this[int index3d] { get => blocks[index3d]; set => blocks[index3d] = value; }

                public int Length => blocks.Length;

                public void ClearBlocks()
                {
                    for (int i = 0; i < blocks.Length; i++) blocks[i] = 0;
                }

                public void ClearBlocksAndPrepare()
                {
                    ClearBlocks();
                }

                public int GetBlockId(int index3d, int layer)
                {
                    return blocks[index3d];
                }

                public int GetBlockIdUnsafe(int index3d)
                {
                    return this[index3d];
                }

                public int GetFluid(int index3d)
                {
                    throw new NotImplementedException();
                }

                public void SetBlockAir(int index3d)
                {
                    this[index3d] = 0;
                }

                public void SetBlockBulk(int chunkIndex, int v, int mantleBlockId, int mantleBlockId1)
                {
                    throw new NotImplementedException();
                }

                public void SetBlockUnsafe(int index3d, int value)
                {
                    this[index3d] = value;
                }

                public void SetFluid(int index3d, int value)
                {

                }

                public void TakeBulkReadLock()
                {
                }

                public void ReleaseBulkReadLock()
                {
                }

                public bool ContainsBlock(int id)
                {
                    throw new NotImplementedException();
                }

                public void FuzzyListBlockIds(List<int> reusableList)
                {
                    throw new NotImplementedException();
                }
            }

            #region unused by rockstrata gen
            public Entity[] Entities => throw new NotImplementedException();
            public int EntitiesCount => throw new NotImplementedException();
            public BlockEntity[] BlockEntities { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
            public HashSet<int> LightPositions { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

            public string GameVersionCreated => throw new NotImplementedException();

            public bool Disposed => throw new NotImplementedException();

            public bool Empty { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
			Dictionary<BlockPos, BlockEntity> IWorldChunk.BlockEntities { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

			public IChunkBlocks MaybeBlocks => throw new NotImplementedException();

            public bool NotAtEdge => throw new NotImplementedException();

            IChunkBlocks IWorldChunk.Blocks => throw new NotImplementedException();

            public int BlocksPlaced => throw new NotImplementedException();

            public int BlocksRemoved => throw new NotImplementedException();

            public Dictionary<string, object> LiveModData { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

            public void AddEntity(Entity entity)
            {
                throw new NotImplementedException();
            }
            public byte[] GetModdata(string key)
            {
                throw new NotImplementedException();
            }
            public byte[] GetServerModdata(string key)
            {
                throw new NotImplementedException();
            }
            public void MarkModified()
            {
                throw new NotImplementedException();
            }
            public bool RemoveEntity(long entityId)
            {
                throw new NotImplementedException();
            }
            public void RemoveModdata(string key)
            {
                throw new NotImplementedException();
            }
            public void SetModdata(string key, byte[] data)
            {
                throw new NotImplementedException();
            }
            public void SetServerModdata(string key, byte[] data)
            {
                throw new NotImplementedException();
            }
            public void Unpack()
            {
                throw new NotImplementedException();
            }
			public bool Unpack_ReadOnly()
			{
				throw new NotImplementedException();
			}
			public int UnpackAndReadBlock(int index, int layer)
			{
				throw new NotImplementedException();
			}
			public ushort Unpack_AndReadLight(int index)
			{
				throw new NotImplementedException();
			}
            public ushort Unpack_AndReadLight(int index, out int lightSat)
            {
                throw new NotImplementedException();
            }


            public Block GetLocalBlockAtBlockPos(IWorldAccessor world, BlockPos position)
            {
                throw new NotImplementedException();
            }

            public void MarkFresh()
            {
                throw new NotImplementedException();
            }

            public BlockEntity GetLocalBlockEntityAtBlockPos(BlockPos pos)
            {
                throw new NotImplementedException();
            }

			public bool AddDecor(IBlockAccessor blockAccessor, BlockPos pos, int faceIndex, Block block)
			{
				throw new NotImplementedException();
			}

			public void RemoveDecor(int index3d, IWorldAccessor world, BlockPos pos)
			{
				throw new NotImplementedException();
			}

            Block[] IWorldChunk.GetDecors(IBlockAccessor blockAccessor, BlockPos pos)
            {
                throw new NotImplementedException();
            }

            public Dictionary<int, Block> GetSubDecors(IBlockAccessor blockAccessor, BlockPos position)
            {
                throw new NotImplementedException();
            }

            public bool GetDecors(IBlockAccessor blockAccessor, BlockPos pos, Block[] result)
			{
				throw new NotImplementedException();
			}

			public Cuboidf[] AdjustSelectionBoxForDecor(IBlockAccessor blockAccessor, BlockPos pos, Cuboidf[] orig)
			{
				throw new NotImplementedException();
			}

			public void FinishLightDoubleBuffering()
			{
				throw new NotImplementedException();
			}

            public bool SetDecor(IBlockAccessor blockAccessor, Block block, BlockPos pos, BlockFacing onFace)
            {
                throw new NotImplementedException();
            }

			public bool SetDecor(IBlockAccessor blockAccessor, Block block, BlockPos pos, int subPosition)
			{
				throw new NotImplementedException();
			}

			public void BreakDecor(IWorldAccessor world, BlockPos pos, BlockFacing side = null)
            {
                throw new NotImplementedException();
            }

            public void BreakAllDecorFast(IWorldAccessor world, BlockPos pos, int index3d, bool callOnBrokenAsDecor = true)
            {
                throw new NotImplementedException();
            }

            public Dictionary<int, Block> GetDecors(IBlockAccessor blockAccessor, BlockPos pos)
            {
                throw new NotImplementedException();
            }

			public void SetDecors(Dictionary<int, Block> newDecors)
			{
				throw new NotImplementedException();
			}

			public void BreakDecorPart(IWorldAccessor world, BlockPos pos, BlockFacing side, int faceAndSubposition)
            {
                throw new NotImplementedException();
            }

            public Block GetDecor(IBlockAccessor blockAccessor, BlockPos pos, int faceAndSubposition)
            {
                throw new NotImplementedException();
            }

            public void SetModdata<T>(string key, T data)
            {
                throw new NotImplementedException();
            }

            public T GetModdata<T>(string key)
            {
                throw new NotImplementedException();
            }

            public bool BreakDecor(IWorldAccessor world, BlockPos pos, BlockFacing side = null, int? faceAndSubposition = null)
            {
                throw new NotImplementedException();
            }

            public T GetModdata<T>(string key, T defaultValue = default(T))
            {
                throw new NotImplementedException();
            }

            public int GetLightAbsorptionAt(int index3d, BlockPos blockPos, IList<Block> blockTypes)
            {
                throw new NotImplementedException();
            }

            public Block GetLocalBlockAtBlockPos(IWorldAccessor world, int posX, int posY, int posZ, int layer)
            {
                throw new NotImplementedException();
            }

            public Block GetLocalBlockAtBlockPos_LockFree(IWorldAccessor world, BlockPos pos, int layer = BlockLayersAccess.Default)
            {
                throw new NotImplementedException();
            }

            public bool SetDecor(Block block, int index3d, BlockFacing onFace)
            {
                throw new NotImplementedException();
            }

            public bool SetDecor(Block block, int index3d, int faceAndSubposition)
            {
                throw new NotImplementedException();
            }

            public bool RemoveBlockEntity(BlockPos pos)
            {
                throw new NotImplementedException();
            }

            public void AcquireBlockReadLock()
            {
                throw new NotImplementedException();
            }

            public void ReleaseBlockReadLock()
            {
                throw new NotImplementedException();
            }

            public void Dispose()
            {

            }

            #endregion
        }
    }
}
