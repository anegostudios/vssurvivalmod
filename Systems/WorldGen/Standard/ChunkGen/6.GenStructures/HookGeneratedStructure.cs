using System.Collections.Generic;
using Newtonsoft.Json;
using Vintagestory.API.Common;

namespace Vintagestory.GameContent;

public class HookGeneratedStructure
{
    [JsonProperty]
    public PathAndOffset[] mainElements;
    [JsonProperty]
    public Dictionary<string, PathAndOffset> endElements;
    [JsonProperty]
    public int offsetX;
    [JsonProperty]
    public int offsetY;
    [JsonProperty]
    public int offsetZ;
    [JsonProperty]
    public int endOffsetY;
    [JsonProperty]
    public AssetLocation[] ReplaceWithBlocklayers;
    [JsonProperty]
    public int mainsizeX;
    [JsonProperty]
    public int mainsizeZ;
    [JsonProperty]
    public bool buildProtected;
    [JsonProperty]
    public string buildProtectionDesc;
    [JsonProperty]
    public string buildProtectionName;
    [JsonProperty]
    public string group;
}