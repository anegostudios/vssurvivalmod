using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Newtonsoft.Json;
using Vintagestory.API.Server;

namespace Vintagestory.ServerMods
{
    public class TiledDungeonConfig : WorldGenStructuresConfigBase
    {
        public int minDistance;
        public int storyStructMinDistance;
        [JsonIgnore]
        public int MinDistanceSq;
        [JsonIgnore]
        public int StoryStructMinDistanceSq;

        /// <summary>
        /// Dungeon definitions, initialized on first retrieval through TryGetDungeon().
        /// </summary>
        [JsonProperty("dungeons")]
        public TiledDungeon[] MaybeDungeons = null!;

        private readonly Dictionary<string, Lazy<TiledDungeon>> dungeonsByCode = new();
        public Dictionary<int, Dictionary<int, int>>? resolvedRockTypeRemaps;

        public bool TryGetDungeon(string code, [NotNullWhen(true)] out TiledDungeon? dungeon)
        {
            dungeon = dungeonsByCode.GetValueOrDefault(code)?.Value;
            return dungeon != null;
        }

        public void Init(ICoreServerAPI api)
        {
            dungeonsByCode.Clear();
            MinDistanceSq = minDistance * minDistance;
            StoryStructMinDistanceSq= storyStructMinDistance * storyStructMinDistance;

            var blockLayerConfig = BlockLayerConfig.GetInstance(api);
            ResolveRemaps(api, blockLayerConfig.RockStrata);
            for (var i = 0; i < MaybeDungeons.Length; i++)
            {
                var tiledDungeon = MaybeDungeons[i];
                if(tiledDungeon.Code == null)
                {
                    api.Logger.Error("Dungeon code at index: " + i + " is not specified. Will skip initialization");
                    continue;
                }
                tiledDungeon.InitSkipGenerationFlags(api);
                dungeonsByCode[tiledDungeon.Code] = new Lazy<TiledDungeon>(() =>
                {
                    tiledDungeon.Init(api, new WorldGenStructuresConfig
                    {
                        LoadedSchematicsCache = new Dictionary<string, BlockSchematicStructure[]>()
                    });
                    return tiledDungeon;
                });

                // For rocktyped dungeons
                foreach (var tile in tiledDungeon.Tiles)
                {
                    if (tile.RockTypeRemapGroup != null)
                    {
                        resolvedRockTypeRemaps = resolvedRocktypeRemapGroups[tile.RockTypeRemapGroup];
                    }
                    else if(tiledDungeon.RockTypeRemapGroup != null)
                    {
                        resolvedRockTypeRemaps =  resolvedRocktypeRemapGroups[tiledDungeon.RockTypeRemapGroup!];
                    }
                    if (tile.RockTypeRemaps != null)
                    {
                        if (resolvedRockTypeRemaps != null)
                        {
                            var ownRemaps = ResolveRockTypeRemaps(tile.RockTypeRemaps, blockLayerConfig.RockStrata, api);
                            foreach (var val in resolvedRockTypeRemaps)
                            {
                                ownRemaps[val.Key] = val.Value;
                            }

                            resolvedRockTypeRemaps = ownRemaps;
                        }
                        else
                        {
                            resolvedRockTypeRemaps = ResolveRockTypeRemaps(tile.RockTypeRemaps, blockLayerConfig.RockStrata, api);
                        }
                    }
                }
            }

            foreach (var dungeon in MaybeDungeons)
            {
                dungeon.resolvedRockTypeRemaps = resolvedRockTypeRemaps;
            }
        }
    }
}
