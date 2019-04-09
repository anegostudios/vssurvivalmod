using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace Vintagestory.ServerMods.NoObf
{
    [JsonObject(MemberSerialization.OptIn)]
    public class GeologicProvinceVariant
    {
        public int Index;
        public int ColorInt;

        [JsonProperty]
        public string Code;
        [JsonProperty]
        public string Hexcolor;
        [JsonProperty]
        public int Weight;
        [JsonProperty]
        public Dictionary<string, GeologicProvinceRockStrata> Rockstrata;

        public GeologicProvinceRockStrata[] RockStrataIndexed;

        public void init()
        {
            RockStrataIndexed = new GeologicProvinceRockStrata[Enum.GetValues(typeof(EnumRockGroup)).Length];

            foreach (var val in Enum.GetValues(typeof(EnumRockGroup)))
            {
                RockStrataIndexed[(int)val] = new GeologicProvinceRockStrata();

                if (Rockstrata.ContainsKey(""+val))
                {
                    RockStrataIndexed[(int)val] = Rockstrata["" + val];
                }
            }
        }
    }
}
