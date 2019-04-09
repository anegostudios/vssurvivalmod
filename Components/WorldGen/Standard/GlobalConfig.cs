using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;

namespace Vintagestory.ServerMods.NoObf
{
    [JsonObject(MemberSerialization.OptIn)]
    public class GlobalConfig
    {
        [JsonProperty]
        public AssetLocation waterBlockCode;

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


        public ushort waterBlockId;
        public ushort lakeIceBlockId;
        public ushort lavaBlockId;
        public ushort basaltBlockId;
        public ushort mantleBlockId;
        public ushort defaultRockId;
    }
}
