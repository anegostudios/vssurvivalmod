using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace Vintagestory.GameContent;

public class FarmingConfig
{
    public CodeAndChance[]? WeedBlockCodes;
    public int DelayGrowthBelowSunLight = 19;
    public float TotalWeedChance;
    public float LossPerLevel = 0.1f;
}

public class ModSystemFarming : ModSystem
{
    public override bool ShouldLoad(ICoreAPI api) => true;
    protected ICoreAPI? api;

    public bool Allowundergroundfarming { get; protected set; }
    public bool AllowcropDeath { get; protected set; }
    public float FertilityRecoverySpeed { get; protected set; } = 0.25f;
    public float GrowthRateMul { get; protected set; } = 1f;


    public WeatherSystemBase? Wsys;
    public RoomRegistry? Roomreg;
    public FarmingConfig? Config;

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
        Wsys = api.ModLoader.GetModSystem<WeatherSystemBase>();
        Roomreg = api.ModLoader.GetModSystem<RoomRegistry>();


        Config = api.Assets.Get<FarmingConfig>("config/farming.json");
        if (Config.WeedBlockCodes != null)
        {
            foreach (var val in Config.WeedBlockCodes)
            {
                Config.TotalWeedChance += val.Chance;
            }
        }

        Allowundergroundfarming = api.World.Config.GetBool("allowUndergroundFarming", false);
        AllowcropDeath = api.World.Config.GetBool("allowCropDeath", true);
        FertilityRecoverySpeed = api.World.Config.GetFloat("fertilityRecoverySpeed", FertilityRecoverySpeed);
        GrowthRateMul = (float)api.World.Config.GetDecimal("cropGrowthRateMul", GrowthRateMul);
    }
}
