using Newtonsoft.Json;
using System;
using System.Collections.Generic;

#nullable disable

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

        public void init(int mapsizey)
        {
            float mul = mapsizey / 256f;

            RockStrataIndexed = new GeologicProvinceRockStrata[Enum.GetValues(typeof(EnumRockGroup)).Length];

            foreach (var val in Enum.GetValues(typeof(EnumRockGroup)))
            {
                RockStrataIndexed[(int)val] = new GeologicProvinceRockStrata();

                if (Rockstrata.ContainsKey(""+val))
                {
                    var r = RockStrataIndexed[(int)val] = Rockstrata["" + val];
                    r.ScaledMaxThickness = mul * r.MaxThickness;
                }
            }
        }
    }
}
