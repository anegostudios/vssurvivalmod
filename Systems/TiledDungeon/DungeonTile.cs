using Vintagestory.API.Server;

namespace Vintagestory.ServerMods
{
    public class DungeonTile : WorldGenStructureBase
    {
        public bool IgnoreMaxTiles;
        public float Chance = 1f;

        public BlockSchematicPartial[][] ResolvedSchematic;

        public void Init(ICoreServerAPI api, BlockLayerConfig blockLayerConfig)
        {
            ResolvedSchematic = LoadSchematicsWithRotations<BlockSchematicPartial>(api, Schematics, blockLayerConfig, null, null, 0, "dungeontiles/", 0, true);
        }
    }
}
