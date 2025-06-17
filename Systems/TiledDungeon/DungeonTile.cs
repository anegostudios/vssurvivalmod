using Vintagestory.API.Server;

#nullable disable

namespace Vintagestory.ServerMods
{
    public class DungeonTile : WorldGenStructureBase
    {
        public bool IgnoreMaxTiles;
        public float Chance = 1f;

        public BlockSchematicPartial[][] ResolvedSchematic;

        public void Init(ICoreServerAPI api, BlockLayerConfig blockLayerConfig)
        {
            ResolvedSchematic = LoadSchematicsWithRotations<BlockSchematicPartial>(api, this, blockLayerConfig, null, null, "dungeontiles/", true);
        }
    }
}
