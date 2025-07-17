using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

#nullable disable

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
        public int MinWaterDepth = 0;

        /// <summary>
        /// This property is only used if the placement is set to UnderWater.
        /// When MinWaterDepth is 0 this property is used to calculate the MinWaterDepth = sealevel * MinWaterDepthP.
        /// Value range: 0.0 - 1.0
        /// </summary>
        [JsonProperty]
        public float MinWaterDepthP = 0;

        /// <summary>
        /// This property is only used if the placement is set to UnderWater. It determines the maximum water depth for this patch
        /// to be placed. (default 0 / disabled)
        /// </summary>
        [JsonProperty]
        public int MaxWaterDepth = 0;

        /// <summary>
        /// This property is only used if the placement is set to UnderWater.
        /// When MaxWaterDepth is 0 this property is used to calculate the MaxWaterDepth = sealevel * MaxWaterDepthP
        /// Value range: 0.0 - 1.0
        /// </summary>
        [JsonProperty]
        public float MaxWaterDepthP = 0;

        /// <summary>
        /// This property can be specified to limit (or increase) the distance from the starting position - e.g. Tule near water should not be more than 1 block higher or lower
        /// Conceivably something which spawns rarely in mountains e.g. Edelweiss would want this higher
        /// </summary>
        [JsonProperty]
        public int MaxHeightDifferential = 8;

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

        [JsonProperty]
        public BlockPatchAttributes Attributes;

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
                            var block = api.World.GetBlock(rocktypedCode);

                            BlocksByRockType[rockBlock.BlockId] = new Block[] { block };
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
                        if (code.Path.Contains('*'))
                        {
                            var searchBlocks = api.World.SearchBlocks(code);
                            if (searchBlocks != null)
                            {
                                blocks.AddRange(searchBlocks);
                            }
                            else
                            {
                                api.World.Logger.Warning("Block patch Nr. {0}: Unable to resolve block with code {1}. Will ignore.", i, code);
                            }
                        }
                        else
                        {
                            api.World.Logger.Warning("Block patch Nr. {0}: Unable to resolve block with code {1}. Will ignore.", i, code);
                        }
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

            if (Attributes != null)
            {
                Attributes.Init(api, i);
            }

            if (MinWaterDepth == 0 && MinWaterDepthP != 0)
            {
                MinWaterDepth = (int)(api.World.SeaLevel * Math.Clamp(MinWaterDepthP, 0, 1));
            }
            if (MaxWaterDepth == 0 && MaxWaterDepthP != 0)
            {
                MaxWaterDepth = (int)(api.World.SeaLevel * Math.Clamp(MaxWaterDepthP, 0, 1));
            }
        }

        public void Generate(IBlockAccessor blockAccessor, IRandom rnd, int posX, int posY, int posZ, int firstBlockId, bool isStoryPatch)
        {
            float quantity = Quantity.nextFloat(1f, rnd) + 1;
            const int chunkSize = GlobalConstants.ChunkSize;

            Block[] blocks = getBlocks(firstBlockId);
            if (blocks.Length == 0) return;

            ModStdWorldGen modSys = null;
            if (blockAccessor is IWorldGenBlockAccessor wgba) modSys = wgba.WorldgenWorldAccessor.Api.ModLoader.GetModSystem<GenVegetationAndPatches>();

            while (quantity-- > 0)
            {
                if (quantity < 1 && rnd.NextFloat() > quantity) break;

                pos.X = posX + (int)OffsetX.nextFloat(1f, rnd);
                pos.Z = posZ + (int)OffsetZ.nextFloat(1f, rnd);
                if(!blockAccessor.IsValidPos(pos)) continue;

                if (modSys != null && !isStoryPatch && modSys.GetIntersectingStructure(pos, ModStdWorldGen.SkipPatchesgHashCode) != null) continue;

                int index = GameMath.Mod((int)BlockCodeIndex.nextFloat(1f, rnd), blocks.Length);

                IServerChunk chunk = (IServerChunk)blockAccessor.GetChunk(pos.X / chunkSize, 0, pos.Z / chunkSize);
                if (chunk == null) break;

                int lx = GameMath.Mod(pos.X, chunkSize);
                int lz = GameMath.Mod(pos.Z, chunkSize);

                if (Placement == EnumBlockPatchPlacement.Underground)
                {
                    pos.Y = rnd.NextInt(Math.Max(1, chunk.MapChunk.WorldGenTerrainHeightMap[lz * GlobalConstants.ChunkSize + lx] - 1));
                }
                else
                {
                    pos.Y = chunk.MapChunk.RainHeightMap[lz * GlobalConstants.ChunkSize + lx] + 1;

                    if (Math.Abs(pos.Y - posY) > MaxHeightDifferential || pos.Y >= blockAccessor.MapSizeY - 1) continue;

                    if (Placement == EnumBlockPatchPlacement.UnderWater || Placement == EnumBlockPatchPlacement.UnderSeaWater)
                    {
                        // ensure top most block is not solid (can be if a larger structure generates over ponds)
                        tempPos.Set(pos.X, pos.Y - 2, pos.Z);
                        var topBlock = blockAccessor.GetBlock(tempPos, BlockLayersAccess.Fluid);
                        if (!topBlock.IsLiquid()) continue;

                        tempPos.Y = pos.Y - GameMath.Max(1, MinWaterDepth);
                        Block downBlock = blockAccessor.GetBlock(tempPos, BlockLayersAccess.Fluid);

                        if (Placement == EnumBlockPatchPlacement.UnderWater && downBlock.LiquidCode != "water") continue;
                        if (Placement == EnumBlockPatchPlacement.UnderSeaWater && downBlock.LiquidCode != "saltwater") continue;

                        if (MaxWaterDepth > 0)
                        {
                            tempPos.Set(pos.X, pos.Y - (MaxWaterDepth + 1), pos.Z);
                            downBlock = blockAccessor.GetBlock(tempPos, BlockLayersAccess.Fluid);

                            if (Placement == EnumBlockPatchPlacement.UnderWater && downBlock.LiquidCode == "water") continue;
                            if (Placement == EnumBlockPatchPlacement.UnderSeaWater && downBlock.LiquidCode == "saltwater") continue;
                        }
                    }
                }

                if (Placement == EnumBlockPatchPlacement.UnderWater || Placement == EnumBlockPatchPlacement.UnderSeaWater)
                {
                    blocks[index].TryPlaceBlockForWorldGenUnderwater(blockAccessor, pos, BlockFacing.UP, rnd, MinWaterDepth, MaxWaterDepth, Attributes);
                }
                else
                {
                    blocks[index].TryPlaceBlockForWorldGen(blockAccessor, pos, BlockFacing.UP, rnd, Attributes);
                }
            }
        }


        private Block[] getBlocks(int firstBlockId)
        {
            if (BlocksByRockType == null || !BlocksByRockType.TryGetValue(firstBlockId, out Block[] blocks))
            {
                blocks = Blocks;
            }

            return blocks;
        }
    }
}
