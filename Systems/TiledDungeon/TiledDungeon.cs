using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
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
        public string Code = null!;
        public List<DungeonTile> Tiles = new List<DungeonTile>();

        public Dictionary<string, DungeonTile> TilesByCode = new Dictionary<string, DungeonTile>();

        public float totalChance;

        public string? stairs;
        [JsonIgnore]
        public BlockSchematicPartial[]? Stairs;

        public string? start;
        [JsonIgnore]
        public BlockSchematicPartial[]? Start;

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

        [JsonProperty]
        public bool BuildProtected;

        [JsonProperty]
        public string? BuildProtectionName;

        [JsonProperty]
        public string? BuildProtectionDesc;

        [JsonProperty]
        public LandformConstraint[]? RequiredLandform;

        public void Init(ICoreServerAPI api)
        {
            totalChance = 0;

            var blockLayerConfig = BlockLayerConfig.GetInstance(api);
            blockLayerConfig.ResolveBlockIds(api);
            if (stairs != null)
            {
                var asset = api.Assets.Get("worldgen/dungeontiles/" + stairs + ".json");
                Stairs = WorldGenStructureBase.LoadSchematic<BlockSchematicPartial>(api, asset, blockLayerConfig, null, null,0);
                TilesByCode[stairs] = new DungeonTile() { ResolvedSchematics = new[] { Stairs }, Code = stairs };
            }

            if (start != null)
            {
                var assetStart = api.Assets.Get("worldgen/dungeontiles/" + start + ".json");
                Start = WorldGenStructureBase.LoadSchematic<BlockSchematicPartial>(api, assetStart, blockLayerConfig, null, null, 0,true);
                TilesByCode[start] = new DungeonTile() { ResolvedSchematics = new[] { Start }, Code = start };
            }

            if (ends != null)
            {
                var ends = new List<BlockSchematicPartial[]>();
                foreach (var e in this.ends)
                {
                    var assetStart = api.Assets.Get("worldgen/dungeontiles/" + e + ".json");
                    var blockSchematicPartials = WorldGenStructureBase.LoadSchematic<BlockSchematicPartial>(api, assetStart, blockLayerConfig, null, null, 0, true);
                    ends.Add(blockSchematicPartials);

                    if (!TilesByCode.TryAdd(e, new DungeonTile() { ResolvedSchematics = new[] { blockSchematicPartials }, Code = e }))
                    {
                        api.Logger.Error($"Dungeon {Code} has a end with duplicate tile code: {e}. Tile will be skipped.");
                    }
                }
                EndSchematics = ends.ToArray();
            }

            for (var i = 0; i < Tiles.Count; i++)
            {
                var tile = Tiles[i];
                if(Tiles[i].Code == null)
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
        }

        public TiledDungeon Copy()
        {
            var dungeon = new TiledDungeon()
            {
                totalChance = totalChance,
                Tiles = new List<DungeonTile>(Tiles),
                Code = Code,
                TilesByCode = new Dictionary<string, DungeonTile>(TilesByCode),
                Stairs = Stairs,
                stairs = stairs,
                Start = Start,
                start = start,
                EndSchematics = EndSchematics,
                ends = ends,
                RequiredLandform = RequiredLandform
            };
            return dungeon;
        }
    }
}
