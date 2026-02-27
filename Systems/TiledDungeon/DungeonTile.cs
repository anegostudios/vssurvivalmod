using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

#nullable disable

namespace Vintagestory.ServerMods
{
    public class DungeonTile : WorldGenStructureBase
    {
        [JsonProperty]
        public float Chance = 1f;
        [JsonIgnore]
        public BlockSchematicPartial[][] ResolvedSchematics;
        [JsonIgnore]
        // Temp field for the dungeon generator
        public int[] ShuffledIndices;

        [JsonProperty]
        public TiledDungeon TileGenerator;

        [JsonProperty]
        public int Min = 0;
        [JsonProperty]
        public int Max = 9999;

        /// <summary>
        /// The names of all connectors inside all the tiles
        /// </summary>
        [JsonIgnore]
        public string[] CachedNames;

        public void Init(ICoreServerAPI api, BlockLayerConfig blockLayerConfig)
        {
            if (TileGenerator != null)
            {
                TileGenerator.Code = $"TileGenerator-{Code}";
                TileGenerator.Init(api);
                var schematics = new List<BlockSchematicPartial[]>();
                foreach (var t in TileGenerator.Tiles)
                {
                    schematics.AddRange(t.ResolvedSchematics);
                }
                ResolvedSchematics = schematics.ToArray();
            }
            else
            {
                ResolvedSchematics = LoadSchematicsWithRotations<BlockSchematicPartial>(api, this, blockLayerConfig, null, null, "dungeontiles/", true);
            }

            ShuffledIndices = new int[ResolvedSchematics.Length];
            for (int i = 0; i < ShuffledIndices.Length; i++) ShuffledIndices[i] = i;


            HashSet<string> nameshs = new HashSet<string>();
            foreach (var schematicPartials in ResolvedSchematics)
            {
                foreach (var schematic in schematicPartials[0].Connectors)
                {
                    nameshs.Add(schematic.Name);
                }
            }

            CachedNames = nameshs.ToArray();
        }

        public void FreshShuffleIndices(LCGRandom lcgRnd)
        {
            for (int i = 0; i < ShuffledIndices.Length; i++) ShuffledIndices[i] = i;
            ShuffledIndices.Shuffle(lcgRnd);
        }

        internal void PlaceConnectorsForDebug(IBlockAccessor ba, BlockPos pos, int tileIndex, int i)
        {
            foreach (var conn in ResolvedSchematics[tileIndex][i].Connectors)
            {
                var pathPosition = pos.AddCopy(conn.Position);
                ba.SetBlock(BlockSchematic.ConnectorBlockId, pathPosition);
                var be = ba.GetBlockEntity<BETileConnector>(pathPosition);
                be.Target = string.Join(",", conn.Targets);
                be.Name = conn.Name;
                be.Direction = conn.Facing;
                be.MarkDirty();
            }
        }
    }
}
