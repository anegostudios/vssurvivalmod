using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Vintagestory.ServerMods.NoObf
{
    public class WorldGenStructuresConfig
    {
        [JsonProperty]
        public float ChanceMultiplier;
        [JsonProperty]
        public WorldGenStructure[] Structures;

        BlockLayerConfig blockLayerConfig;

        internal void Init(ICoreServerAPI api)
        {
            IAsset asset = api.Assets.Get("worldgen/rockstrata.json");
            RockstrataWorldProperty rockstrata = asset.ToObject<RockstrataWorldProperty>();

            asset = api.Assets.Get("worldgen/blocklayerconfig.json");
            blockLayerConfig = asset.ToObject<BlockLayerConfig>();
            blockLayerConfig.ResolveBlockIds(api, rockstrata);

            
            for (int i = 0; i < Structures.Length; i++)
            {
                Structures[i].Init(api, blockLayerConfig);
                
            }

        }

    }
}
