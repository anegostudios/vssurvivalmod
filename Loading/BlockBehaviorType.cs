using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API;

namespace Vintagestory.ServerMods.NoObf
{
    public class BlockBehaviorType
    {
        [JsonProperty]
        public string name;

        [JsonConverter(typeof(JsonObjectConverter))]
        public JsonObject properties;
    }
}