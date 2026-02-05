using System.Collections.Generic;
using Newtonsoft.Json;
using Vintagestory.API.Server;

namespace Vintagestory.ServerMods
{
    public class TiledDungeonConfig
    {
        public int minDistance;
        [JsonIgnore]
        public int MinDistanceSq;

        public TiledDungeon[] Dungeons = null!;

        public Dictionary<string, TiledDungeon> DungeonsByCode = new Dictionary<string, TiledDungeon>();

        public void Init(ICoreServerAPI api)
        {
            MinDistanceSq = minDistance * minDistance;
            for (var i = 0; i < Dungeons.Length; i++)
            {
                if(Dungeons[i].Code == null)
                {
                    api.Logger.Error("Dungeon code at index: " + i + " is not specified. Will skip initialization");
                    continue;
                }
                Dungeons[i].Init(api);
                DungeonsByCode[Dungeons[i].Code] = Dungeons[i];
            }
        }
    }
}
