using System.Collections.Generic;
using Vintagestory.API.Server;

namespace Vintagestory.ServerMods
{
    public class TiledDungeon
    {
        public string Code;
        public List<DungeonTile> Tiles = new List<DungeonTile>();

        public Dictionary<string, DungeonTile> TilesByCode = new Dictionary<string, DungeonTile>();

        public float totalChance;

        internal void Init(ICoreServerAPI api)
        {
            totalChance = 0;
            for (int i = 0; i < Tiles.Count; i++)
            {
                Tiles[i].Init(api);
                TilesByCode[Tiles[i].Code] = Tiles[i];

                totalChance += Tiles[i].Chance;
            }
        }
    }
}
