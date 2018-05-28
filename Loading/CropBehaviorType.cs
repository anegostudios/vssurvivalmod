using Newtonsoft.Json;
using Vintagestory.API;

namespace Vintagestory.ServerMods.NoObf
{
    public class CropBehaviorType
    {
        [JsonProperty]
        public string name;

        [JsonConverter(typeof(JsonObjectConverter))]
        public JsonObject properties;
    }
}