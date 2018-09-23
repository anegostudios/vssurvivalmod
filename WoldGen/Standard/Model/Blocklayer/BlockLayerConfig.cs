using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace Vintagestory.ServerMods.NoObf
{

    public class BlockLayerConfig
    {
        public int blockLayerTransitionSize;

        public BlockLayer[] Blocklayers;

        public TallGrassProperties Tallgrass;

        public SnowLayerProperties SnowLayer;

        public LakeBedLayerProperties LakeBedLayer;

        public static readonly string cacheKey = "BlockLayerConfig";

        /// <summary>
        /// Loads and caches the BlockLayerConfig if it's not already loaded. Otherwise
        /// returns the cached value
        /// </summary>
        /// <param name="api"></param>
        /// <returns></returns>
        public static BlockLayerConfig GetInstance(ICoreServerAPI api)
        {
            if(api.ObjectCache.ContainsKey(cacheKey))
            {
                return api.ObjectCache[cacheKey] as BlockLayerConfig;
            }
            else
            {
                IAsset asset = api.Assets.Get("worldgen/rockstrata.json");
                RockstrataWorldProperty rockstrata = asset.ToObject<RockstrataWorldProperty>();
                asset = api.Assets.Get("worldgen/blocklayerconfig.json");
                BlockLayerConfig blockLayerConfig = asset.ToObject<BlockLayerConfig>();
                blockLayerConfig.ResolveBlockIds(api, rockstrata);

                api.ObjectCache[cacheKey] = blockLayerConfig;
                return blockLayerConfig;
            }
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

        public void ResolveBlockIds(ICoreServerAPI api, RockstrataWorldProperty rockstrata)
        {
            for (int i = 0; i < Blocklayers.Length; i++)
            {
                Random rnd = new Random(api.WorldManager.Seed + i);

                Blocklayers[i].Init(api, rockstrata, rnd);
            }

            SnowLayer.BlockId = api.WorldManager.GetBlockId(SnowLayer.BlockCode);

            for (int i = 0; i < Tallgrass.BlockCodeByMin.Length; i++)
            {
                Tallgrass.BlockCodeByMin[i].BlockId = api.WorldManager.GetBlockId(Tallgrass.BlockCodeByMin[i].BlockCode);
            }

            for (int i = 0; i < LakeBedLayer.BlockCodeByMin.Length; i++)
            {
                Random rnd = new Random(api.WorldManager.Seed + i);
                LakeBedLayer.BlockCodeByMin[i].Init(api, rockstrata, rnd);
            }
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


        public bool Suitable(float temp, float rain, float yRel, Random rnd)
        {
            float transDistance = MinTemp - temp + transSize;
            return rain >= MinRain && rain <= MaxRain && MinY <= yRel && MaxY >= yRel && transDistance <= rnd.NextDouble() * transSize;
        }

        public ushort GetBlockForMotherRock(ushort rockBlockid)
        {
            ushort resultId = BlockId;
            BlockIdMapping?.TryGetValue(rockBlockid, out resultId);
            return resultId;
        }

        public Dictionary<ushort, ushort> BlockIdMapping;
        public ushort BlockId;

        public void Init(ICoreServerAPI api, RockstrataWorldProperty rockstrata, Random rnd)
        {
            ResolveBlockIds(api, rockstrata);
        }

        void ResolveBlockIds(ICoreServerAPI api, RockstrataWorldProperty rockstrata)
        {
            if (BlockCode != null && BlockCode.Path.Length > 0)
            {
                if (BlockCode.Path.Contains("{rocktype}"))
                {
                    BlockIdMapping = new Dictionary<ushort, ushort>();
                    for (int i = 0; i < rockstrata.Variants.Length; i++)
                    {
                        string rocktype = rockstrata.Variants[i].BlockCode.Path.Split('-')[1];
                        BlockIdMapping.Add(api.WorldManager.GetBlockId(rockstrata.Variants[i].BlockCode), api.WorldManager.GetBlockId(BlockCode.CopyWithPath(BlockCode.Path.Replace("{rocktype}", rocktype))));
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

        public ushort BlockId;
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

        public ushort BlockId;
    }
}
