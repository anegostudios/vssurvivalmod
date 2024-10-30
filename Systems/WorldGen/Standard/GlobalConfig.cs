using Newtonsoft.Json;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace Vintagestory.ServerMods.NoObf
{
    [JsonObject(MemberSerialization.OptIn)]
    public class GlobalConfig
    {
        [JsonProperty]
        public AssetLocation waterBlockCode;
        [JsonProperty]
        public AssetLocation saltWaterBlockCode;

        [JsonProperty]
        public AssetLocation lakeIceBlockCode;

        [JsonProperty]
        public AssetLocation lavaBlockCode;

        [JsonProperty]
        public AssetLocation basaltBlockCode;

        [JsonProperty]
        public AssetLocation mantleBlockCode;

        [JsonProperty]
        public AssetLocation defaultRockCode;

        [JsonProperty]
        public float neutralCreatureSpawnMultiplier = 1f;

        public int waterBlockId;
        public int saltWaterBlockId;
        public int lakeIceBlockId;
        public int lavaBlockId;
        public int basaltBlockId;
        public int mantleBlockId;
        public int defaultRockId;
        
        public static readonly string cacheKey = "GlobalConfig";

        public static GlobalConfig GetInstance(ICoreServerAPI api)
        {
            if(api.ObjectCache.TryGetValue(cacheKey, out var value))
            {
                return value as GlobalConfig;
            }

            var asset = api.Assets.Get("worldgen/global.json");
            var globalConfig = asset.ToObject<GlobalConfig>();

            globalConfig.defaultRockId = api.World.GetBlock(globalConfig.defaultRockCode)?.BlockId ?? 0;
            globalConfig.waterBlockId = api.World.GetBlock(globalConfig.waterBlockCode)?.BlockId ?? 0;
            globalConfig.saltWaterBlockId = api.World.GetBlock(globalConfig.saltWaterBlockCode)?.BlockId ?? 0;
            globalConfig.lakeIceBlockId = api.World.GetBlock(globalConfig.lakeIceBlockCode)?.BlockId ?? 0;
            globalConfig.lavaBlockId = api.World.GetBlock(globalConfig.lavaBlockCode)?.BlockId ?? 0;
            globalConfig.basaltBlockId = api.World.GetBlock(globalConfig.basaltBlockCode)?.BlockId ?? 0;
            globalConfig.mantleBlockId = api.World.GetBlock(globalConfig.mantleBlockCode)?.BlockId ?? 0;

            api.ObjectCache[cacheKey] = globalConfig;
            return globalConfig;
        }
    }
}
