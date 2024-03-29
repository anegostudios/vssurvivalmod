﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Vintagestory.ServerMods
{
    public class WorldGenStoryStructuresConfig : WorldGenStructuresConfigBase
    {
        [JsonProperty]
        public WorldGenStoryStructure[] Structures;


        public RockStrataConfig Init(ICoreServerAPI api, LCGRandom rand)
        {
            IAsset asset = api.Assets.Get("worldgen/rockstrata.json");
            RockStrataConfig rockstrata = asset.ToObject<RockStrataConfig>();
            ResolveRemaps(api, rockstrata);

            foreach (var struc in Structures)
            {
                struc.Init(api, rand);
            }

            return rockstrata;
        }

    }

    public class WorldGenStructuresConfig : WorldGenStructuresConfigBase
    {
        [JsonProperty]
        public float ChanceMultiplier;
        [JsonProperty]
        public WorldGenStructure[] Structures;

        BlockLayerConfig blockLayerConfig;
        /// <summary>
        /// Cache for every loaded schematic asset. The key is the AssetLocation + '~' + the yOffset (some can have a custom yOffset).  The value is an array with the schematic's four rotations
        /// <br/>LoadSchematic<>() attempts first to find the desired schematic in the cache
        /// </summary>
        public Dictionary<string, BlockSchematicStructure[]> LoadedSchematicsCache;

        internal void Init(ICoreServerAPI api)
        {
            IAsset asset = api.Assets.Get("worldgen/rockstrata.json");
            RockStrataConfig rockstrata = asset.ToObject<RockStrataConfig>();
            asset = api.Assets.Get("worldgen/blocklayers.json");
            blockLayerConfig = asset.ToObject<BlockLayerConfig>();
            blockLayerConfig.ResolveBlockIds(api, rockstrata);

            ResolveRemaps(api, rockstrata);

            LoadedSchematicsCache = new Dictionary<string, BlockSchematicStructure[]>();

            for (int i = 0; i < Structures.Length; i++)
            {
                LCGRandom rand = new LCGRandom(api.World.Seed + i + 512);
                try
                {
                    Structures[i].Init(api, blockLayerConfig, rockstrata, this, rand);
                }
                catch (Exception e)
                {
                    api.Logger.Error("The following exception occurred while initialising structure for worldgen: " + Structures[i].Code);
                    api.Logger.Error(e);
                }
            }
        }
    }

    public class WorldGenStructuresConfigBase
    {
        [JsonProperty]
        public Dictionary<string, Dictionary<AssetLocation, AssetLocation>> RocktypeRemapGroups = null;
        [JsonProperty]
        public Dictionary<string, int> SchematicYOffsets = null;

        public Dictionary<string, Dictionary<int, Dictionary<int, int>>> resolvedRocktypeRemapGroups = null;

        public void ResolveRemaps(ICoreServerAPI api, RockStrataConfig rockstrata)
        {

            if (RocktypeRemapGroups != null)
            {
                resolvedRocktypeRemapGroups = new Dictionary<string, Dictionary<int, Dictionary<int, int>>>();

                foreach (var val in RocktypeRemapGroups)
                {
                    resolvedRocktypeRemapGroups[val.Key] = ResolveRockTypeRemaps(val.Value, rockstrata, api);
                }
            }
        }

        public static Dictionary<int, Dictionary<int, int>> ResolveRockTypeRemaps(Dictionary<AssetLocation, AssetLocation> rockTypeRemaps, RockStrataConfig rockstrata, ICoreAPI api)
        {
            var resolvedReplaceWithRocktype = new Dictionary<int, Dictionary<int, int>>();

            foreach (var val in rockTypeRemaps)
            {
                Dictionary<int, int> blockIdByRockId = new Dictionary<int, int>();
                foreach (var strat in rockstrata.Variants)
                {
                    Block rockBlock = api.World.GetBlock(strat.BlockCode);
                    AssetLocation resolvedLoc = val.Value.Clone();
                    resolvedLoc.Path = resolvedLoc.Path.Replace("{rock}", rockBlock.LastCodePart());

                    Block resolvedBlock = api.World.GetBlock(resolvedLoc);
                    if (resolvedBlock != null)
                    {
                        blockIdByRockId[rockBlock.Id] = resolvedBlock.Id;

                        Block quartzBlock = api.World.GetBlock(new AssetLocation("ore-quartz-" + rockBlock.LastCodePart()));
                        if (quartzBlock != null)
                        {
                            blockIdByRockId[quartzBlock.Id] = resolvedBlock.Id;
                        }
                    }
                }

                Block[] sourceBlocks = api.World.SearchBlocks(val.Key);
                foreach (var sourceBlock in sourceBlocks)
                {
                    resolvedReplaceWithRocktype[sourceBlock.Id] = blockIdByRockId;
                }
            }

            return resolvedReplaceWithRocktype;
        }
    }
}
