using Newtonsoft.Json;
using Vintagestory.API.Common;

#nullable disable

namespace Vintagestory.ServerMods.NoObf
{
    [JsonObject(MemberSerialization.OptIn)]
    public class LandformsWorldProperty : WorldProperty<LandformVariant>
    {
        [JsonIgnore]
        public LandformVariant[] LandFormsByIndex;
    }
}
