using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace Vintagestory.ServerMods.NoObf
{
    [JsonObject(MemberSerialization.OptIn)]
    public class RockStrataVariant : WorldPropertyVariant
    {
		[JsonProperty]
        public AssetLocation BlockCode;
		[JsonProperty]
        public string RockType;
        [JsonProperty]
        public float Weight;
        [JsonProperty]
        public float SoilpH;
        [JsonProperty]
        public float WeatheringFactor;
        [JsonProperty]
        public float HeightErosion;
        [JsonProperty]
        public EnumRockGroup RockGroup;
    }

   
}
