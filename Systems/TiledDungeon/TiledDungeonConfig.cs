using System.Collections.Generic;
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

        public TiledDungeon[] Dungeons = null!;

        public Dictionary<string, TiledDungeon> DungeonsByCode = new Dictionary<string, TiledDungeon>();
        public Dictionary<int, Dictionary<int, int>>? resolvedRockTypeRemaps;

        public void Init(ICoreServerAPI api)
        {
            MinDistanceSq = minDistance * minDistance;
            StoryStructMinDistanceSq= storyStructMinDistance * storyStructMinDistance;

            var blockLayerConfig = BlockLayerConfig.GetInstance(api);
            ResolveRemaps(api, blockLayerConfig.RockStrata);
            for (var i = 0; i < Dungeons.Length; i++)
            {
                var tiledDungeon = Dungeons[i];
                if(tiledDungeon.Code == null)
                {
                    api.Logger.Error("Dungeon code at index: " + i + " is not specified. Will skip initialization");
                    continue;
                }
                tiledDungeon.Init(api);
                DungeonsByCode[tiledDungeon.Code] = tiledDungeon;

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

            foreach (var dungeon in Dungeons)
            {
                dungeon.resolvedRockTypeRemaps = resolvedRockTypeRemaps;
            }
        }
    }
}
