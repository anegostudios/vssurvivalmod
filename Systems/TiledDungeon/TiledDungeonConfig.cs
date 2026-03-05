using System.Collections.Generic;
using Newtonsoft.Json;
using Vintagestory.API.Server;

namespace Vintagestory.ServerMods
{
    public class TiledDungeonConfig : WorldGenStructuresConfigBase
    {
        public int minDistance;
        [JsonIgnore]
        public int MinDistanceSq;

        public TiledDungeon[] Dungeons = null!;

        public Dictionary<string, TiledDungeon> DungeonsByCode = new Dictionary<string, TiledDungeon>();
        public Dictionary<int, Dictionary<int, int>>? resolvedRockTypeRemaps;

        public void Init(ICoreServerAPI api)
        {
            MinDistanceSq = minDistance * minDistance;

            var blockLayerConfig = BlockLayerConfig.GetInstance(api);
            ResolveRemaps(api, blockLayerConfig.RockStrata);
            for (var i = 0; i < Dungeons.Length; i++)
            {
                if(Dungeons[i].Code == null)
                {
                    api.Logger.Error("Dungeon code at index: " + i + " is not specified. Will skip initialization");
                    continue;
                }
                Dungeons[i].Init(api);
                DungeonsByCode[Dungeons[i].Code] = Dungeons[i];

                // For rocktyped dungeons
                foreach (var tile in Dungeons[i].Tiles)
                {
                    if (tile.RockTypeRemapGroup != null)
                    {
                        resolvedRockTypeRemaps = resolvedRocktypeRemapGroups[tile.RockTypeRemapGroup];
                    }
                    else if(Dungeons[i].RockTypeRemapGroup != null)
                    {
                        resolvedRockTypeRemaps =  resolvedRocktypeRemapGroups[Dungeons[i].RockTypeRemapGroup!];
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
