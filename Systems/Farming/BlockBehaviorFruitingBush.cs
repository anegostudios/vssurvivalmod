using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace Vintagestory.GameContent;

public class BlockBehaviorFruitingBush : BlockBehavior
{
    /// <summary>
    /// The amount of time, in seconds, it takes to harvest this block.
    /// </summary>
    [DocumentAsJson("Recommended", "0")]
    public float harvestTime;

    /// <summary>
    /// The amount of time, in seconds, to take a cutting
    /// </summary>
    [DocumentAsJson("Recommended", "0")]
    public float cuttingTime;

    /// <summary>
    /// An array of drops for when the block is harvested. If only using a single drop you can use <see cref="harvestedStack"/>, otherwise this property is required.
    /// </summary>
    [DocumentAsJson("Required")]
    public BlockDropItemStack[]? harvestedStacks;

    /// <summary>
    /// The block required to harvest the block.
    /// </summary>
    [DocumentAsJson("Optional", "None")]
    public EnumTool? Tool;


    public float PauseGrowthBelowTemperature;
    public float PauseGrowthAboveTemperature;
    public float ResetGrowthBelowTemperature;
    public float ResetGrowthAboveTemperature;
    public float GoDormantBelowTemperature;
    public float LeaveDormantAboveTemperature;

    public string[]? CreatureDietFoodTags;
    public JsonObject? GrowthProperties;
    public float GrowthRateMul = 1f;
    public AssetLocation? HarvestingSound;

    public BlockBehaviorFruitingBush(Block block) : base(block) { }

    public override void OnLoaded(ICoreAPI api)
    {
        base.OnLoaded(api);

        GrowthRateMul = (float)api.World.Config.GetDecimal("cropGrowthRateMul", GrowthRateMul);
        var attr = block.Attributes["growthProperties"][block.Variant["type"]];
        GrowthProperties = attr.Exists ? attr : block.Attributes["growthProperties"]["*"];
        PauseGrowthBelowTemperature = GrowthProperties["pauseGrowthBelowTemperature"].AsFloat(-999);
        PauseGrowthAboveTemperature = GrowthProperties["pauseGrowthAboveTemperature"].AsFloat(999);
        ResetGrowthBelowTemperature = GrowthProperties["resetGrowthBelowTemperature"].AsFloat(-999);
        ResetGrowthAboveTemperature = GrowthProperties["resetGrowthAboveTemperature"].AsFloat(999);
        GoDormantBelowTemperature = GrowthProperties["goDormantBelowTemperature"].AsFloat(-999);
        LeaveDormantAboveTemperature = GrowthProperties["leaveDormantAboveTemperature"].AsFloat(999);
        CreatureDietFoodTags = GrowthProperties["foodTags"].AsArray<string>();
        string? code = GrowthProperties["harvestingSound"].AsString("game:sounds/block/leafy-picking");
        if (code != null)
        {
            HarvestingSound = AssetLocation.Create(code, block.Code.Domain);
        }

        harvestTime = block.Attributes["harvestTime"].AsFloat(0.5f);
        cuttingTime = block.Attributes["cuttingTime"].AsFloat(2f);
        harvestedStacks = block.Attributes["harvestedStacks"].AsObject<BlockDropItemStack[]>(null);
        foreach (var hstack in harvestedStacks) hstack.Resolve(api.World, "harvested stack of fruiting bush", code);
        Tool = block.Attributes["harvestTool"].AsObject<EnumTool?>(null);
    }



}
