using Newtonsoft.Json;

#nullable disable

namespace Vintagestory.GameContent;

public class PathAndOffset
{
    [JsonProperty]
    public string path;
    [JsonProperty]
    public int dx;
    [JsonProperty]
    public int dy;
    [JsonProperty]
    public int dz;
    [JsonProperty]
    public int maxCount;
}
