using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Vintagestory.ServerMods
{
    public class DungeonTile : WorldGenStructureBase
    {
        public string[][] Constraints = new string[6][];
        public BlockPos Start;
        public BlockPos End;
        public bool IgnoreMaxTiles;
        public float Chance = 1f;


        public BlockSchematicPartial[][] ResolvedSchematic;
        private BlockLayerConfig blockLayerConfig;

        public void Init(ICoreServerAPI api)
        {
            IAsset asset = api.Assets.Get("worldgen/rockstrata.json");
            RockStrataConfig rockstrata = asset.ToObject<RockStrataConfig>();
            asset = api.Assets.Get("worldgen/blocklayers.json");
            blockLayerConfig = asset.ToObject<BlockLayerConfig>();
            blockLayerConfig.ResolveBlockIds(api, rockstrata);
            ResolvedSchematic = LoadSchematicsWithRotations<BlockSchematicPartial>(api, Schematics, blockLayerConfig, null, null, 0, "dungeontiles/");
        }
    }
}
