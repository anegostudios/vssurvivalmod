using Newtonsoft.Json;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Vintagestory.ServerMods
{
    public class WorldGenVillageConfig
    {
        [JsonProperty]
        public float ChanceMultiplier;
        [JsonProperty]
        public WorldGenVillage[] VillageTypes;

        BlockLayerConfig blockLayerConfig;

        internal void Init(ICoreServerAPI api, WorldGenStructuresConfig structureConfig)
        {
            Dictionary<string, Dictionary<int, Dictionary<int, int>>> resolvedRocktypeRemapGroups = structureConfig.resolvedRocktypeRemapGroups;
            Dictionary<string, int> schematicYOffsets = structureConfig.SchematicYOffsets;

            blockLayerConfig = BlockLayerConfig.GetInstance(api);

            for (int i = 0; i < VillageTypes.Length; i++)
            {
                LCGRandom rand = new LCGRandom(api.World.Seed + i + 512);
                VillageTypes[i].Init(api, blockLayerConfig, structureConfig, resolvedRocktypeRemapGroups, schematicYOffsets, null, blockLayerConfig.RockStrata, rand);
            }
        }
    }
}
