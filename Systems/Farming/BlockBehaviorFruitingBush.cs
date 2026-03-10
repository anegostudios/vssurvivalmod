using System.Collections.Generic;
using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent;

public class NpkNutrients {
    public float N; public float P; public float K;
    public NpkNutrients(float n, float p, float k) { N = n; P = p; K = k; }
    public NpkNutrients() { }
    public NpkNutrients Clone() => new NpkNutrients(N,P,K);
    public static NpkNutrients operator *(NpkNutrients left, float right) => new NpkNutrients(left.N * right, left.P * right, left.K * right);
}

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
    public Dictionary<string, NpkNutrients> nutrientUseByHealthState = new()
    {
        ["bountiful"] = new NpkNutrients(10, 10, 10),
        ["healthy"] = new NpkNutrients(7, 7, 7),
        ["struggling"] = new NpkNutrients(4, 4, 4),
        ["barren"] = new NpkNutrients(1, 1, 1),
    };

    /// <summary>
    /// Sorted by growthstage index
    /// Young = 0, Mature = 1, Flowering = 2, Ripening = 3, Ripe = 4, Dormant = 5
    /// </summary>
    public NatFloat[] growthStageMonths;

    public BlockBehaviorFruitingBush(Block block) : base(block) { }

    public override void OnLoaded(ICoreAPI api)
    {
        base.OnLoaded(api);
        GrowthRateMul = (float)api.World.Config.GetDecimal("cropGrowthRateMul", GrowthRateMul);
        GrowthProperties = block.Attributes["growthProperties"];
        PauseGrowthBelowTemperature = GrowthProperties["pauseGrowthBelowTemperature"].AsFloat(-999);
        PauseGrowthAboveTemperature = GrowthProperties["pauseGrowthAboveTemperature"].AsFloat(999);
        ResetGrowthBelowTemperature = GrowthProperties["resetGrowthBelowTemperature"].AsFloat(-999);
        ResetGrowthAboveTemperature = GrowthProperties["resetGrowthAboveTemperature"].AsFloat(999);
        GoDormantBelowTemperature = GrowthProperties["goDormantBelowTemperature"].AsFloat(-999);
        LeaveDormantAboveTemperature = GrowthProperties["leaveDormantAboveTemperature"].AsFloat(999);
        nutrientUseByHealthState = GrowthProperties["nutrientUseByHealthState"].AsObject(nutrientUseByHealthState);
        growthStageMonths =
        [
            GrowthProperties["youngStageMonths"].AsObject<NatFloat>(new(3, 1, EnumDistribution.UNIFORM)),
            GrowthProperties["emptyStageMonths"].AsObject<NatFloat>(new(0.85f, 0.1f, EnumDistribution.UNIFORM)),
            GrowthProperties["floweringStageMonths"].AsObject<NatFloat>(new(0.35f, 0.1f, EnumDistribution.UNIFORM)),
            GrowthProperties["ripeningStageMonths"].AsObject<NatFloat>(new(0.85f, 0.1f, EnumDistribution.UNIFORM)),
            GrowthProperties["ripeStageMonths"].AsObject<NatFloat>(new(1, 0.1f, EnumDistribution.UNIFORM)),
            null
        ];

        CreatureDietFoodTags = block.Attributes["foodTags"].AsArray<string>()!;
        string? code = block.Attributes["harvestingSound"].AsString("game:sounds/block/leafy-picking");
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
