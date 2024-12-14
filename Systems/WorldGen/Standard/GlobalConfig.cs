using Newtonsoft.Json;
using Vintagestory.API.Common;

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
    }
}
