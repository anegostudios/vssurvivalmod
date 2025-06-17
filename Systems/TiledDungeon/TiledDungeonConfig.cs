using System.Collections.Generic;
using Vintagestory.API.Server;

#nullable disable

namespace Vintagestory.ServerMods
{
    public class TiledDungeonConfig
    {
        public TiledDungeon[] Dungeons;

        public Dictionary<string, TiledDungeon> DungeonsByCode;

        public void Init(ICoreServerAPI api)
        {
            DungeonsByCode = new Dictionary<string, TiledDungeon>();

            for (var i = 0; i < Dungeons.Length; i++)
            {
                Dungeons[i].Init(api);
                DungeonsByCode[Dungeons[i].Code] = Dungeons[i];
            }
        }
    }
}
