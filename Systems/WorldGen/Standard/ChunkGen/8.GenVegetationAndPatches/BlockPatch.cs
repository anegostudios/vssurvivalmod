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
        UnderWater,
        NearSeaWater,
        UnderSeaWater,
        UnderTrees,
        OnTrees,
        OnSurfacePlusUnderTrees
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
        public float MinShrub = 0;
        [JsonProperty]
        public float MaxShrub = 1;
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
        public EnumTreeType TreeType = EnumTreeType.Any;
        [JsonProperty]
        public NatFloat OffsetX = NatFloat.createGauss(0, 5);
        [JsonProperty]
        public NatFloat OffsetZ = NatFloat.createGauss(0, 5);
        [JsonProperty]
        public NatFloat BlockCodeIndex = null;
        [JsonProperty]
        public NatFloat Quantity = NatFloat.createGauss(7, 7);
        [JsonProperty]
        public string MapCode = null;
        [JsonProperty]
        public string[] RandomMapCodePool = null;

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

        public void Init(ICoreServerAPI api, RockStrataConfig rockstrata, LCGRandom rnd, int i)
        {
            List<Block> blocks = new List<Block>();

            for (int j = 0; j < blockCodes.Length; j++)
            {
                AssetLocation code = blockCodes[j];

                if (code.Path.Contains("{rocktype}"))
                {
                    if (BlocksByRockType == null) BlocksByRockType = new Dictionary<int, Block[]>();

                    for (int k = 0; k < rockstrata.Variants.Length; k++)
                    {
                        string rocktype = rockstrata.Variants[k].BlockCode.Path.Split('-')[1];
                        AssetLocation rocktypedCode = code.CopyWithPath(code.Path.Replace("{rocktype}", rocktype));

                        Block rockBlock = api.World.GetBlock(rockstrata.Variants[k].BlockCode);

                        if (rockBlock != null)
                        {
                            BlocksByRockType[rockBlock.BlockId] = new Block[] { api.World.GetBlock(rocktypedCode) };
                        }
                    }
                }
                else
                {
                    Block block = api.World.GetBlock(code);
                    if (block != null)
                    {
                        blocks.Add(block);
                    }
                    else
                    {
                        api.World.Logger.Warning("Block patch Nr. {0}: Unable to resolve block with code {1}. Will ignore.", i, code);
                    }
                }
            }

            Blocks = blocks.ToArray();

            if (BlockCodeIndex == null)
            {
                BlockCodeIndex = NatFloat.createUniform(0, Blocks.Length);
            }

            if (RandomMapCodePool != null)
            {
                int index = rnd.NextInt(RandomMapCodePool.Length);
                MapCode = RandomMapCodePool[index];
            }
        }

        public void Generate(IBlockAccessor blockAccessor, LCGRandom rnd, int posX, int posY, int posZ, int firstBlockId)
        {
            float quantity = Quantity.nextFloat() + 1;
            int chunkSize = blockAccessor.ChunkSize;

            Block[] blocks = getBlocks(firstBlockId);
            if (blocks.Length == 0) return;

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
                    pos.Y = rnd.NextInt(Math.Max(1, chunk.MapChunk.WorldGenTerrainHeightMap[lz * blockAccessor.ChunkSize + lx] - 1));
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
            Block[] blocks;
            if (BlocksByRockType == null || !BlocksByRockType.TryGetValue(firstBlockId, out blocks))
            {
                blocks = Blocks;
            }

            return blocks;
        }
    }
}
