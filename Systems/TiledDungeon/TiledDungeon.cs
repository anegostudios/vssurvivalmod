﻿using System.Collections.Generic;
using Newtonsoft.Json;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace Vintagestory.ServerMods
{
    public class TiledDungeon
    {
        public string Code;
        public List<DungeonTile> Tiles = new List<DungeonTile>();

        public Dictionary<string, DungeonTile> TilesByCode = new Dictionary<string, DungeonTile>();

        public float totalChance;
        public string stairs;
        [JsonIgnore]
        public BlockSchematicPartial[] Stairs;
        public string start;
        [JsonIgnore]
        public BlockSchematicPartial[] Start;

        internal void Init(ICoreServerAPI api)
        {
            totalChance = 0;

            var blockLayerConfig = BlockLayerConfig.GetInstance(api);
            blockLayerConfig.ResolveBlockIds(api);
            if (stairs != null)
            {
                var asset = api.Assets.Get("worldgen/dungeontiles/" + stairs + ".json");
                Stairs = WorldGenStructureBase.LoadSchematic<BlockSchematicPartial>(api, asset, blockLayerConfig, null, 0);
                foreach (var blockSchematicPartial in Stairs)
                {
                    blockSchematicPartial.InitMetaBlocks(api.World.BlockAccessor);
                }
            }

            if (start != null)
            {
                var assetStart = api.Assets.Get("worldgen/dungeontiles/" + start + ".json");
                Start = WorldGenStructureBase.LoadSchematic<BlockSchematicPartial>(api, assetStart, blockLayerConfig, null, 0, true);
                foreach (var blockSchematicPartial in Start)
                {
                    blockSchematicPartial.InitMetaBlocks(api.World.BlockAccessor);
                }
                TilesByCode[start] = new DungeonTile() { ResolvedSchematic = new[] { Start } };
            }

            for (var i = 0; i < Tiles.Count; i++)
            {
                Tiles[i].Init(api, blockLayerConfig);
                TilesByCode[Tiles[i].Code] = Tiles[i];

                totalChance += Tiles[i].Chance;
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
                start = start
            };
            return dungeon;
        }
    }
}
