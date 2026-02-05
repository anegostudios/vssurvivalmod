using System;
using System.ComponentModel;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent;

#region Other classes
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

public interface ICrop
{
    bool Ripe { get; }
    EnumSoilNutrient RequiredNutrient { get; }
}

public enum EnumBushHealthState
{
    Barren,
    Struggling,
    Healthy,
    Bountiful
}

public enum EnumBushGrowthState
{
    Sapling = 0,
    Mature = 1,
    Flowering = 2,
    Ripening = 3,
    Ripe = 4,
    Dormant = 5
}

public class BerrybushState
{
    /// <summary>
    /// When the bush was planted
    /// </summary>
    public double PlantedTotalDays;
    /// <summary>
    /// When it matured or when it will mature
    /// </summary>
    public double MatureTotalDays;
    /// <summary>
    /// What growth state the bush is in
    /// </summary>
    public EnumBushGrowthState Growthstate;
    /// <summary>
    /// For wild bushes: What (always static) health state it is in
    /// </summary>
    public EnumBushHealthState? WildBushState;


    public void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
    {
        PlantedTotalDays = tree.GetDouble("plantedTotalDays");
        MatureTotalDays = tree.GetDouble("matureTotalDays");
        Growthstate = (EnumBushGrowthState)tree.GetInt("growthState");

        WildBushState = null;
        if (tree.HasAttribute("wildBushState")) WildBushState = (EnumBushHealthState)tree.GetInt("wildBushState");
    }

    public void ToTreeAttributes(ITreeAttribute tree)
    {
        tree.SetDouble("plantedTotalDays", PlantedTotalDays);
        tree.SetDouble("matureTotalDays", MatureTotalDays);
        tree.SetInt("growthState", (int)Growthstate);
        if (WildBushState != null) tree.SetInt("wildBushState", (int)WildBushState);
    }
}

#endregion

public class BlockBehaviorFruitingBush : BlockBehavior
{
    public float PauseGrowthBelowTemperature;
    public float PauseGrowthAboveTemperature;
    public float ResetGrowthBelowTemperature;
    public float ResetGrowthAboveTemperature;
    public float GoDormantBelowTemperature;
    public float LeaveDormantAboveTemperature;

    public string[]? CreatureDietFoodTags;
    public JsonObject? GrowthProperties;
    public float GrowthRateMul = 1f;
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
    }
}

public class BEBehaviorFruitingBush : BlockEntityBehavior, IAnimalFoodSource
{
    protected const float intervalHours = 2f;
    protected static readonly float[] NoNutrients = new float[3];

    protected NatFloat nextStageMonths = NatFloat.create(EnumDistribution.UNIFORM, 0.98f, 0.09f);
    protected float[] npkNutrients => Api.World.BlockAccessor.GetBlockEntity(soilPos)?.GetBehavior<BEBehaviorSoilNutrition>()?.NpkNutrients ?? NoNutrients;
    protected ICoreClientAPI capi;
    protected RoomRegistry roomreg;
    protected BlockPos soilPos;
    protected BlockBehaviorFruitingBush bhBush;
    protected double lastCheckAtTotalDays = 0;
    protected double transitionHoursLeft = -1;

    public int roomness;
    public BerrybushState BState;



    public BEBehaviorFruitingBush(BlockEntity blockentity) : base(blockentity)
    {
        BState = new BerrybushState();
    }
    public override void Initialize(ICoreAPI api, JsonObject properties)
    {
        base.Initialize(api, properties);

        soilPos = Blockentity.Pos.DownCopy();
        capi = api as ICoreClientAPI;
        roomreg = Api.ModLoader.GetModSystem<RoomRegistry>();
        nextStageMonths = bhBush.GrowthProperties?["nextStageMonths"].AsObject<NatFloat>(nextStageMonths) ?? nextStageMonths;

        if (api is ICoreServerAPI)
        {
            if (transitionHoursLeft <= 0)
            {
                transitionHoursLeft = GetHoursForNextStage();
                lastCheckAtTotalDays = api.World.Calendar.TotalDays;
            }

            if (Api.World.Config.GetBool("processCrops", true))
            {
                //Blockentity.RegisterGameTickListener(growthCheck, 8000, api.World.Rand.Next(3000));
            }

            api.ModLoader.GetModSystem<POIRegistry>().AddPOI(this);
        }
    }

    private void growthCheck(float dt)
    {
        double totalDays = Api.World.Calendar.TotalDays;
        if (totalDays < BState.MatureTotalDays) return;

        if (BState.Growthstate == EnumBushGrowthState.Sapling)
        {
            Blockentity.MarkDirty(true);
            BState.Growthstate = EnumBushGrowthState.Mature;
        }

        if (!(Api as ICoreServerAPI).World.IsFullyLoadedChunk(Pos)) return;

        if (Block.Attributes == null)
        {
#if DEBUG
            Api.World.Logger.Notification("Ghost berry bush block entity at {0}. Block.Attributes is null, will remove game tick listener", Pos);
#endif
            Blockentity.UnregisterAllTickListeners();
            return;
        }

        // In case this block was imported from another older world. In that case lastCheckAtTotalDays and LastPrunedTotalDays would be a future date.
        lastCheckAtTotalDays = Math.Min(lastCheckAtTotalDays, Api.World.Calendar.TotalDays);

        // We don't need to check more than one year because it just begins to loop then
        double daysToCheck = GameMath.Mod(Api.World.Calendar.TotalDays - lastCheckAtTotalDays, Api.World.Calendar.DaysPerYear);

        float intervalDays = intervalHours / Api.World.Calendar.HoursPerDay;
        if (daysToCheck <= intervalDays) return;

        roomness = getRoomness();

        ClimateCondition? conds = null;
        float baseTemperature = 0;
        while (daysToCheck > intervalDays)
        {
            daysToCheck -= intervalDays;
            lastCheckAtTotalDays += intervalDays;
            transitionHoursLeft -= intervalHours;

            if (conds == null)
            {
                conds = Api.World.BlockAccessor.GetClimateAt(Pos, EnumGetClimateMode.ForSuppliedDate_TemperatureOnly, lastCheckAtTotalDays);
                if (conds == null) return;
                baseTemperature = conds.WorldGenTemperature;
            }
            else
            {
                conds.Temperature = baseTemperature;  // Keep resetting the field we are interested in, because it can be modified by the OnGetClimate event
                Api.World.BlockAccessor.GetClimateAt(Pos, conds, EnumGetClimateMode.ForSuppliedDate_TemperatureOnly, lastCheckAtTotalDays);
            }

            float temperature = conds.Temperature;
            if (roomness > 0)
            {
                temperature += 5;
            }

            if (BState.Growthstate == EnumBushGrowthState.Dormant)
            {
                if (temperature > bhBush.LeaveDormantAboveTemperature)
                {
                    setGrowthState(EnumBushGrowthState.Mature);
                }
                continue;
            }


            bool pause = temperature < bhBush.PauseGrowthBelowTemperature || temperature > bhBush.PauseGrowthAboveTemperature;
            if (pause) continue;

            bool reset = temperature < bhBush.ResetGrowthBelowTemperature || temperature > bhBush.ResetGrowthAboveTemperature;
            if (reset)
            {
                if (BState.Growthstate == EnumBushGrowthState.Flowering || BState.Growthstate == EnumBushGrowthState.Ripening || BState.Growthstate == EnumBushGrowthState.Ripe)
                {
                    setGrowthState(EnumBushGrowthState.Mature);
                }
                continue;
            }

            bool goDormant = temperature < bhBush.GoDormantBelowTemperature;
            if (goDormant)
            {
                setGrowthState(EnumBushGrowthState.Dormant);
                continue;
            }

            if (transitionHoursLeft <= 0)
            {
                // Looping through 1,2,3,4, 1,2,3,4, ...
                setGrowthState((EnumBushGrowthState)(1 + GameMath.Mod((int)BState.Growthstate, 4)));
                transitionHoursLeft = GetHoursForNextStage();
            }
        }

        Blockentity.MarkDirty(false);
    }

    private int getRoomness()
    {
        if (Api.World.BlockAccessor.GetRainMapHeightAt(Pos) > Pos.Y) // Fast pre-check
        {
            Room? room = roomreg?.GetRoomForPosition(Pos);
            return (room != null && room.SkylightCount > room.NonSkylightCount && room.ExitCount == 0) ? 1 : 0;
        }

        return 0;
    }

    private void setGrowthState(EnumBushGrowthState state)
    {
        BState.Growthstate = state;
        Blockentity.MarkDirty(true);
    }

    public virtual double GetHoursForNextStage()
    {
        if (BState.Growthstate == EnumBushGrowthState.Ripe) return 4 * nextStageMonths.nextFloat() * Api.World.Calendar.DaysPerMonth * Api.World.Calendar.HoursPerDay;

        return nextStageMonths.nextFloat() * Api.World.Calendar.DaysPerMonth * Api.World.Calendar.HoursPerDay / bhBush.GrowthRateMul;
    }


    public void OnPlanted(ItemStack fromStack)
    {
        BState.PlantedTotalDays = Api.World.Calendar.TotalDays;
        BState.MatureTotalDays = Api.World.Calendar.DaysPerMonth * (6 + 6 * Api.World.Rand.NextDouble());
    }

    public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
    {
        var healthState = GetHealthState();

        dsc.AppendLine(Lang.Get("Health state: {0}", Lang.Get("healthstate-" + healthState.ToString().ToLowerInvariant())));
        if (getRoomness() > 0)
        {
            dsc.AppendLine(Lang.Get("greenhousetempbonus"));
        }
    }


    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
    {
        base.FromTreeAttributes(tree, worldAccessForResolve);
        BState.FromTreeAttributes(tree, worldAccessForResolve);
    }

    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        base.ToTreeAttributes(tree);
        BState.ToTreeAttributes(tree);
    }


    public EnumBushHealthState GetHealthState()
    {
        if (BState.WildBushState != null) return (EnumBushHealthState)BState.WildBushState;

        float avg = (npkNutrients[0] + npkNutrients[1] + npkNutrients[2]) / 3f;
        if (avg < 0.1) return EnumBushHealthState.Barren;
        if (avg < 0.3) return EnumBushHealthState.Struggling;
        if (avg < 0.8) return EnumBushHealthState.Healthy;
        return EnumBushHealthState.Bountiful;
    }


    #region IAnimalFoodSource impl
    public bool IsSuitableFor(Entity entity, CreatureDiet diet)
    {
        if (diet == null || BState.Growthstate != EnumBushGrowthState.Ripe) return false;
        return diet.Matches(EnumFoodCategory.NoNutrition, bhBush.CreatureDietFoodTags);
    }

    public float ConsumeOnePortion(Entity entity)
    {
        var bbh = Block.GetBehavior<BlockBehaviorHarvestable>();
        bbh?.harvestedStacks?.Foreach(harvestedStack => { Api.World.SpawnItemEntity(harvestedStack?.GetNextItemStack(), Pos); });
        Api.World.PlaySoundAt(bbh?.harvestingSound, Pos, 0);

        BState.Growthstate = EnumBushGrowthState.Mature;
        Blockentity.MarkDirty(true);
        return 0.1f;
    }

    public Vec3d Position => base.Pos.ToVec3d().Add(0.5, 0.5, 0.5);
    public string Type => "food";


    public override void OnBlockRemoved()
    {
        base.OnBlockRemoved();

        if (Api.Side == EnumAppSide.Server)
        {
            Api.ModLoader.GetModSystem<POIRegistry>().RemovePOI(this);
        }
    }

    public override void OnBlockUnloaded()
    {
        base.OnBlockUnloaded();

        if (Api?.Side == EnumAppSide.Server)
        {
            Api.ModLoader.GetModSystem<POIRegistry>().RemovePOI(this);
        }
    }
    #endregion

}
