using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Vintagestory.ServerMods.NoObf
{
    public enum EnumBlockPatchPlacement
    {
        ReplaceSurface,
        OnSurface,
        NearWater,
        Anywhere,
        Underground,
        UnderWater
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class BlockPatch
    {
        [JsonProperty]
        public AssetLocation[] blockCodes;
        [JsonProperty]
        public float Chance = 0.05f;
        [JsonProperty]
        public int MinTemp = -30;
        [JsonProperty]
        public int MaxTemp = 40;
        [JsonProperty]
        public float MinRain = 0;
        [JsonProperty]
        public float MaxRain = 1;
        [JsonProperty]
        public float MinForest = 0;
        [JsonProperty]
        public float MaxForest = 1;
        [JsonProperty]
        public float MinFertility = 0;
        [JsonProperty]
        public float MaxFertility = 1;
        [JsonProperty]
        public float MinY = -0.3f;
        [JsonProperty]
        public float MaxY = 1;
        [JsonProperty]
        public EnumBlockPatchPlacement Placement = EnumBlockPatchPlacement.OnSurface;
        [JsonProperty]
        public NatFloat OffsetX = NatFloat.createGauss(0, 5);
        [JsonProperty]
        public NatFloat OffsetZ = NatFloat.createGauss(0, 5);
        [JsonProperty]
        public NatFloat BlockCodeIndex = null;
        [JsonProperty]
        public NatFloat Quantity = NatFloat.createGauss(7, 7);

        /// <summary>
        /// This property is only used if the placement is set to UnderWater. It determines the minimum water depth for this patch
        /// to be placed. 
        /// </summary>
        [JsonProperty]
        public int MinWaterDepth = 3;

        /// <summary>
        /// When true, will be generated after shrubs and trees were generated
        /// </summary>
        [JsonProperty]
        public bool PostPass = false;

        /// <summary>
        /// When true, will be generated before block layers are generated
        /// </summary>
        [JsonProperty]
        public bool PrePass = false;


        public Block[] Blocks;

        public Dictionary<int, Block[]> BlocksByRockType;
        BlockPos pos = new BlockPos();
        BlockPos tempPos = new BlockPos();

        public BlockPatch()
        {

        }


        public void Generate(IBlockAccessor blockAccessor, Random rnd, int posX, int posY, int posZ, int firstBlockId)
        {
            float quantity = Quantity.nextFloat() + 1;
            int chunkSize = blockAccessor.ChunkSize;

            Block[] blocks = getBlocks(firstBlockId);
            
            while (quantity-- > 0)
            {
                if (quantity < 1 && rnd.NextDouble() > quantity) break;

                pos.X = posX + (int)OffsetX.nextFloat();
                pos.Z = posZ + (int)OffsetZ.nextFloat();

                int index = GameMath.Mod((int)BlockCodeIndex.nextFloat(), blocks.Length);

                IServerChunk chunk = (IServerChunk)blockAccessor.GetChunk(pos.X / chunkSize, 0, pos.Z / chunkSize);
                if (chunk == null) break;

                int lx = GameMath.Mod(pos.X, chunkSize);
                int lz = GameMath.Mod(pos.Z, chunkSize);

                if (Placement == EnumBlockPatchPlacement.Underground)
                {
                    pos.Y = rnd.Next(Math.Max(1, chunk.MapChunk.WorldGenTerrainHeightMap[lz * blockAccessor.ChunkSize + lx] - 1)) ;
                }
                else
                {

                    pos.Y = chunk.MapChunk.RainHeightMap[lz * blockAccessor.ChunkSize + lx] + 1;

                    if (Math.Abs(pos.Y - posY) > 8 || pos.Y >= blockAccessor.MapSizeY - 1) continue;
                    if (Placement == EnumBlockPatchPlacement.UnderWater)
                    {
                        tempPos.Set(pos.X, pos.Y - GameMath.Max(1, MinWaterDepth), pos.Z);
                        Block downBlock = blockAccessor.GetBlock(tempPos);
                        if (downBlock == null || downBlock.LiquidCode != "water") continue;
                    }
                }

                blocks[index].TryPlaceBlockForWorldGen(blockAccessor, pos, BlockFacing.UP, rnd);
            }
        }


        private Block[] getBlocks(int firstBlockId)
        {
            Block[] blocks = this.Blocks;
            if (BlocksByRockType == null || !BlocksByRockType.TryGetValue(firstBlockId, out blocks))
            {
                blocks = this.Blocks;
            }

            return blocks;
        }
    }
}
