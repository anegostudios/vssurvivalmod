using Newtonsoft.Json;
using Vintagestory.API.Common;

namespace Vintagestory.ServerMods.NoObf
{
    [JsonObject(MemberSerialization.OptIn)]
    public class LandformsWorldProperty : WorldProperty<LandformVariant>
    {
        [JsonIgnore]
        public LandformVariant[] LandFormsByIndex;
    }
}
