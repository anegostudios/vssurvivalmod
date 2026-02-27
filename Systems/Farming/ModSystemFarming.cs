using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace Vintagestory.GameContent;

public class ModSystemFarming : ModSystem
{
    public override bool ShouldLoad(ICoreAPI api) => true;
    protected ICoreAPI? api;

    public bool Allowundergroundfarming { get; protected set; }
    public bool AllowcropDeath { get; protected set; }
    public float FertilityRecoverySpeed { get; protected set; } = 0.25f;
    public float GrowthRateMul { get; protected set; } = 1f;


    public override void Start(ICoreAPI api)
    {
        this.api = api;
    }

    public override void StartClientSide(ICoreClientAPI api)
    {
        api.Event.LevelFinalize += loadWorldConfigValues;
    }


    public override void StartServerSide(ICoreServerAPI api)
    {
        api.Event.SaveGameLoaded += loadWorldConfigValues;
    }

    private void loadWorldConfigValues()
    {
        Allowundergroundfarming = api.World.Config.GetBool("allowUndergroundFarming", false);
        AllowcropDeath = api.World.Config.GetBool("allowCropDeath", true);
        FertilityRecoverySpeed = api.World.Config.GetFloat("fertilityRecoverySpeed", FertilityRecoverySpeed);
        GrowthRateMul = (float)api.World.Config.GetDecimal("cropGrowthRateMul", GrowthRateMul);
    }
}
