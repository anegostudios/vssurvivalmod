using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace Vintagestory.ServerMods
{
    public class LandformConstraint
    {
        [JsonProperty]
        public string Code;
        [JsonProperty]
        public float Value;
        [JsonProperty]
        public string Type;
    }

    public class TiledDungeon
    {
        public bool Worldgen;
        public string Code = null!;
        public List<DungeonTile> Tiles = new List<DungeonTile>();

        public Dictionary<string, DungeonTile> TilesByCode = new Dictionary<string, DungeonTile>();

        [JsonIgnore]
        public float totalChance;
        public float chance;
        public int MinSpawnDistance;

        public string? start;
        [JsonIgnore]
        public BlockSchematicPartial[]? Start;

        public string? surface;
        [JsonIgnore]
        public BlockSchematicPartial[]? Surface;

        public string? SurfaceConnectorName;
        public int surfaceYOffset;

        public TiledDungeon? StairCase;

        public string[]? ends;
        [JsonIgnore]
        public BlockSchematicPartial[][]? EndSchematics;

        [JsonProperty]
        public int MaxTiles;

        [JsonProperty]
        public int MinTiles;

        /// <summary>
        /// If defined these connects must be closed or the tile will fail to generate
        /// </summary>
        [JsonProperty]
        public string[]? RequireClosed;

        /// <summary>
        /// If defined these connectors will remain opened for the current generator. This is relevant for subdungeons.
        /// </summary>
        [JsonProperty]
        public string[]? RequireOpened;

        /// <summary>
        /// If defined these connectors must not get blocked by other tiles.
        /// </summary>
        [JsonProperty]
        public HashSet<string>? RequireUnblocked;

        [JsonProperty]
        public bool BuildProtected;

        [JsonProperty]
        public string? BuildProtectionName;

        [JsonProperty]
        public string? BuildProtectionDesc;

        [JsonProperty]
        public LandformConstraint[]? RequiredLandform;

        // For rocktyped ruins
        internal Dictionary<int, Dictionary<int, int>>? resolvedRockTypeRemaps = null;
        public string? RockTypeRemapGroup = null;

        [JsonProperty]
        public AssetLocation[]? ReplaceWithBlocklayers;
        internal int[] replacewithblocklayersBlockids = Array.Empty<int>();

        [JsonProperty]
        public int MaxDepth { get; set; }

        [JsonProperty]
        public int MinDepth { get; set; }

        [JsonProperty]
        public Dictionary<string, int>? GroupMax;

        public void Init(ICoreServerAPI api)
        {
            totalChance = 0;

            var blockLayerConfig = BlockLayerConfig.GetInstance(api);
            blockLayerConfig.ResolveBlockIds(api);

            if (surface != null)
            {
                var asset = api.Assets.GetManyInCategory("worldgen", "dungeontiles/" + surface + ".json").FirstOrDefault();
                Surface = WorldGenStructureBase.LoadSchematic<BlockSchematicPartial>(api, asset, blockLayerConfig, null, null, 0, true);
                TilesByCode[surface] = new DungeonTile() { ResolvedSchematics = new[] { Surface }, Code = surface };
            }

            if (start != null)
            {
                var assetStart = api.Assets.GetManyInCategory("worldgen", "dungeontiles/" + start + ".json").FirstOrDefault();
                Start = WorldGenStructureBase.LoadSchematic<BlockSchematicPartial>(api, assetStart, blockLayerConfig, null, null, 0, true);
                TilesByCode[start] = new DungeonTile() { ResolvedSchematics = new[] { Start }, Code = start };
            }

            if (ends != null)
            {
                var ends = new List<BlockSchematicPartial[]>();
                foreach (var e in this.ends)
                {
                    var assetEnd = api.Assets.GetManyInCategory("worldgen", "dungeontiles/" + e + ".json").FirstOrDefault();
                    var blockSchematicPartials = WorldGenStructureBase.LoadSchematic<BlockSchematicPartial>(api, assetEnd, blockLayerConfig, null, null, 0, true);
                    ends.Add(blockSchematicPartials);

                    if (!TilesByCode.TryAdd(e, new DungeonTile() { ResolvedSchematics = new[] { blockSchematicPartials }, Code = e }))
                    {
                        api.Logger.Error($"Dungeon {Code} has a end with duplicate tile code: {e}. Tile will be skipped.");
                    }
                }

                EndSchematics = ends.ToArray();
            }

            StairCase?.Init(api);

            if (RequiredLandform != null && RequiredLandform.Any(l => l.Code == null || l.Type == null))
            {
                api.Logger.Error($"Dungeon {Code} has RequiredLandform with missing Code or Type.");
            }

            for (var i = 0; i < Tiles.Count; i++)
            {
                var tile = Tiles[i];
                if (Tiles[i].Code == null)
                {
                    api.Logger.Error($"Dungeon {Code} has a Tile at index: {i} without a code specified. Will skip initialization");
                    continue;
                }

                tile.Init(api, blockLayerConfig);

                if (tile.TileGenerator != null)
                {
                    foreach (var (code, subtile) in tile.TileGenerator.TilesByCode)
                    {
                        if (!TilesByCode.TryAdd(code, subtile))
                        {
                            api.Logger.Error($"Dungeon {Code} has a TileGenerator with duplicate tile code: {code}. Tile will be skipped.");
                        }
                    }
                }
                else
                {
                    if (!TilesByCode.TryAdd(tile.Code, tile))
                    {
                        api.Logger.Error($"Dungeon {Code} has a TileGenerator with duplicate tile code: {tile.Code}. Tile will be skipped.");
                    }
                }

                totalChance += tile.Chance;
            }

            if (RequireClosed == null && RequireOpened != null)
            {
                var toBeClosed = new HashSet<string>();
                foreach (var tile in Tiles)
                {
                    foreach (var name in tile.CachedNames)
                    {
                        if (!RequireOpened.Contains(name))
                        {
                            toBeClosed.Add(name);
                        }
                    }
                }

                RequireClosed = toBeClosed.ToArray();
                api.Logger.Debug($"Dungeon {Code} Setting RequireClosed to {string.Join(",", RequireClosed)}");
            }

            if (ReplaceWithBlocklayers != null)
            {
                replacewithblocklayersBlockids = new int[ReplaceWithBlocklayers.Length];
                for (var j = 0; j < replacewithblocklayersBlockids.Length; j++)
                {
                    var block = api.World.GetBlock(ReplaceWithBlocklayers[j]);
                    if (block == null)
                    {
                        throw new Exception(string.Format("Dungeon with code {0} has replace block layer {1} defined, but no such block found!",
                            Code, ReplaceWithBlocklayers[j]));
                    }

                    replacewithblocklayersBlockids[j] = block.Id;
                }
            }
        }
    }
}
