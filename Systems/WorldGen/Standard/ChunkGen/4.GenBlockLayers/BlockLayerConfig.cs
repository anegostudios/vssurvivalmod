﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Reflection;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

#nullable disable

namespace Vintagestory.ServerMods
{

    public class BlockLayerConfig
    {
        public float blockLayerTransitionSize;

        public BlockLayer[] Blocklayers;

        public TallGrassProperties Tallgrass;

        public SnowLayerProperties SnowLayer;

        public BeachLayerProperties BeachLayer;

        public LakeBedLayerProperties LakeBedLayer;

        public LakeBedLayerProperties OceanBedLayer;


        public RockStrataConfig RockStrata;

        public static readonly string cacheKey = "BlockLayerConfig";

        /// <summary>
        /// Loads and caches the BlockLayerConfig if it's not already loaded. Otherwise
        /// returns the cached value
        /// </summary>
        /// <param name="api"></param>
        /// <returns></returns>
        public static BlockLayerConfig GetInstance(ICoreServerAPI api)
        {
            if(api.ObjectCache.TryGetValue(cacheKey, out var value))
            {
                return value as BlockLayerConfig;
            }

            var asset = api.Assets.Get("worldgen/blocklayers.json");
            var blockLayerConfig = asset.ToObject<BlockLayerConfig>();
            asset = api.Assets.Get("worldgen/rockstrata.json");
            blockLayerConfig.RockStrata = asset.ToObject<RockStrataConfig>();
            blockLayerConfig.ResolveBlockIds(api);

            api.ObjectCache[cacheKey] = blockLayerConfig;
            return blockLayerConfig;
        }

        public BlockLayer GetBlockLayerById(IWorldAccessor world, string blockLayerId)
        {
            foreach (BlockLayer layer in Blocklayers)
            {
                if (blockLayerId.Equals(layer.ID))
                {
                    return layer;
                }
            }
            return null;
        }

        public void ResolveBlockIds(ICoreServerAPI api)
        {
            for (int i = 0; i < Blocklayers.Length; i++)
            {
                Random rnd = new Random(api.WorldManager.Seed + i);

                Blocklayers[i].Init(api, RockStrata, rnd);
            }

            SnowLayer.BlockId = api.WorldManager.GetBlockId(SnowLayer.BlockCode);

            for (int i = 0; i < Tallgrass.BlockCodeByMin.Length; i++)
            {
                Tallgrass.BlockCodeByMin[i].BlockId = api.WorldManager.GetBlockId(Tallgrass.BlockCodeByMin[i].BlockCode);
            }

            for (int i = 0; i < LakeBedLayer.BlockCodeByMin.Length; i++)
            {
                Random rnd = new Random(api.WorldManager.Seed + i);
                LakeBedLayer.BlockCodeByMin[i].Init(api, RockStrata, rnd);
            }
            for (int i = 0; i < OceanBedLayer.BlockCodeByMin.Length; i++)
            {
                Random rnd = new Random(api.WorldManager.Seed + i);
                OceanBedLayer.BlockCodeByMin[i].Init(api, RockStrata, rnd);
            }
            
            BeachLayer.ResolveBlockIds(api, RockStrata);
        }
    }

    public class TallGrassProperties
    {
        public float RndWeight;
        public float PerlinWeight;
        public TallGrassBlockCodeByMin[] BlockCodeByMin;
    }


    public class LakeBedLayerProperties
    {
        public LakeBedBlockCodeByMin[] BlockCodeByMin;

        public int GetSuitable(float temp, float rainRel, float yRel, LCGRandom rand, int rockBlockId)
        {
            for (int i = 0; i < BlockCodeByMin.Length; i++)
            {
                if (BlockCodeByMin[i].Suitable(temp, rainRel, yRel, rand))
                {
                    return BlockCodeByMin[i].GetBlockForMotherRock(rockBlockId);
                }
            }

            return 0;
        }
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class LakeBedBlockCodeByMin
    {
        [JsonProperty]
        public float MinTemp = -30;
        [JsonProperty]
        public float MinRain = 0;
        [JsonProperty]
        public float MaxRain = 1;
        [JsonProperty]
        public float MinY = 0;
        [JsonProperty]
        public float MaxY = 1;
        [JsonProperty]
        public AssetLocation BlockCode;

        int transSize = 3;


        public bool Suitable(float temp, float rain, float yRel, LCGRandom rnd)
        {
            return Suitable(temp, rain, yRel, rnd.NextFloat());
        }

        public bool Suitable(float temp, float rain, float yRel, float rnd)
        {
            float transDistance = MinTemp - temp + transSize;
            return rain >= MinRain && rain <= MaxRain && MinY <= yRel && MaxY >= yRel && transDistance <= rnd * transSize;
        }

        public int GetBlockForMotherRock(int rockBlockid)
        {
            int resultId = BlockId;
            BlockIdMapping?.TryGetValue(rockBlockid, out resultId);
            return resultId;
        }

        public Dictionary<int, int> BlockIdMapping;
        public int BlockId;

        public void Init(ICoreServerAPI api, RockStrataConfig rockstrata, Random rnd)
        {
            ResolveBlockIds(api, rockstrata);
        }

        void ResolveBlockIds(ICoreServerAPI api, RockStrataConfig rockstrata)
        {
            if (BlockCode != null && BlockCode.Path.Length > 0)
            {
                if (BlockCode.Path.Contains("{rocktype}"))
                {
                    BlockIdMapping = new Dictionary<int, int>();
                    for (int i = 0; i < rockstrata.Variants.Length; i++)
                    {
                        if (rockstrata.Variants[i].IsDeposit) continue;

                        string rocktype = rockstrata.Variants[i].BlockCode.Path.Split('-')[1];

                        Block rockBlock = api.World.GetBlock(rockstrata.Variants[i].BlockCode);
                        Block typedBlock = api.World.GetBlock(BlockCode.CopyWithPath(BlockCode.Path.Replace("{rocktype}", rocktype)));

                        if (rockBlock != null && typedBlock != null)
                        {
                            BlockIdMapping[rockBlock.BlockId] = typedBlock.BlockId;
                        }
                    }
                }
                else
                {
                    BlockId = api.WorldManager.GetBlockId(BlockCode);
                }
            }
            else BlockCode = null;
        }

    }


    [JsonObject(MemberSerialization.OptIn)]
    public class TallGrassBlockCodeByMin
    {
        [JsonProperty]
        public int MinTemp;
        [JsonProperty]
        public float MinRain;
        [JsonProperty]
        public float MaxForest;
        [JsonProperty]
        public AssetLocation BlockCode;

        public int BlockId;
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class SnowLayerProperties
    {
        [JsonProperty]
        public int MaxTemp;
        [JsonProperty]
        public int TransitionSize;
        [JsonProperty]
        public AssetLocation BlockCode;

        public int BlockId;
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class BeachLayerProperties
    {
        [JsonProperty]
        public float Strength;
        [JsonProperty]
        public AssetLocation BlockCode;

        public Dictionary<int, int> BlockIdMapping;
        public int BlockId;

        public void ResolveBlockIds(ICoreServerAPI api, RockStrataConfig rockstrata)
        {
            if (BlockCode != null && BlockCode.Path.Length > 0)
            {
                if (BlockCode.Path.Contains("{rocktype}"))
                {
                    BlockIdMapping = new Dictionary<int, int>();
                    for (int i = 0; i < rockstrata.Variants.Length; i++)
                    {
                        if (rockstrata.Variants[i].IsDeposit) continue;

                        string rocktype = rockstrata.Variants[i].BlockCode.Path.Split('-')[1];

                        Block rockBlock = api.World.GetBlock(rockstrata.Variants[i].BlockCode);
                        Block rocktypedBlock = api.World.GetBlock(BlockCode.CopyWithPath(BlockCode.Path.Replace("{rocktype}", rocktype)));
                        if (rockBlock != null && rocktypedBlock != null)
                        {
                            BlockIdMapping[rockBlock.BlockId] = rocktypedBlock.BlockId;
                        }
                    }
                }
                else
                {
                    BlockId = api.WorldManager.GetBlockId(BlockCode);
                }
            }
            else BlockCode = null;

        }
        }
    }
