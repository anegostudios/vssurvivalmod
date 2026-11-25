using System;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Vintagestory.GameContent;

#nullable disable

// Base class for farmland crops that fast forward growth.
// Handles the time tracking and fast forwarding logic, while the actual growth logic is implemented in the derived class
public class BlockEntityFastForwardGrowth : BlockEntity
{
    protected static Random rand = new Random();

    protected double totalHoursLastUpdate;
    protected bool allowundergroundfarming;
    protected double previousHourInterval;
    protected BlockPos upPos;
    protected ModSystemFarming msFarming;
    protected int roomness;
    protected virtual int RainHeightOffset => 0;


    public override void Initialize(ICoreAPI api)
    {
        base.Initialize(api);
        msFarming = api.ModLoader.GetModSystem<ModSystemFarming>();
        allowundergroundfarming = Api.World.Config.GetBool("allowUndergroundFarming", false);
        upPos = Pos.UpCopy();
        registerTickListener(api);
    }

    protected virtual void registerTickListener(ICoreAPI api)
    {
        if (api.Side != EnumAppSide.Server) return;

        if (Api.World.Config.GetBool("processCrops", true))
        {
            RegisterGameTickListener((dt) =>
            {
                if (!(Api as ICoreServerAPI).World.IsFullyLoadedChunk(Pos)) return;
                Update(dt);
            }, 4500, rand.Next(4500));
        }
    }

    protected virtual void Update(float dt)
    {
        double hoursSinceLastUpdate = Api.World.Calendar.TotalHours - totalHoursLastUpdate;
        double hourIntervall = 3 + rand.NextDouble();

        if (hoursSinceLastUpdate < hourIntervall)
        {
            if (hoursSinceLastUpdate < 0)
            {
                // We need to rollback time when the blockEntity saved date is ahead of the calendar date: can happen if a schematic is imported
                onRollback(-hoursSinceLastUpdate);
                totalHoursLastUpdate = Api.World.Calendar.TotalHours;
            }

            ShortUpdate(dt);
            return;     // Early exit for performance
        }

        IWorldAccessor world = Api.World;
        IBlockAccessor blockAccessor = world.BlockAccessor;

        // Don't fast-forward for more than the past year
        float hoursPerDay = world.Calendar.HoursPerDay;
        if (hoursSinceLastUpdate > world.Calendar.DaysPerYear * hoursPerDay)
        {
            float oneYear = world.Calendar.DaysPerYear * hoursPerDay;
            totalHoursLastUpdate += hoursSinceLastUpdate - oneYear;    // Skip the excess time, doing nothing
            hoursSinceLastUpdate = oneYear;
        }

        ClimateCondition conds = blockAccessor.GetClimateAt(Pos, EnumGetClimateMode.ForSuppliedDate_TemperatureRainfallOnly, totalHoursLastUpdate / hoursPerDay);
        if (conds == null) return;

        // Slow down growth on bad light levels
        int lightpenalty = 0;
        if (!allowundergroundfarming)
        {
            lightpenalty = Math.Max(0, world.SeaLevel - Pos.Y);
        }
        int sunlight = blockAccessor.GetLightLevel(upPos, allowundergroundfarming ? EnumLightLevelType.MaxLight : EnumLightLevelType.OnlySunLight);
        double lightGrowthSpeedFactor = GameMath.Clamp(1 - (msFarming.Config.DelayGrowthBelowSunLight - (sunlight - lightpenalty)) * msFarming.Config.LossPerLevel, 0, 1);

        bool skyExposed = blockAccessor.GetRainMapHeightAt(Pos.X, Pos.Z) <= Pos.Y + RainHeightOffset;
        if (!skyExposed) // Fast pre-check - if it is skyExposed (which is the normal case), then it cannot be in a room
        {
            // If we do have to check for a room, update roomness no more than twice per 24 hours
            if (hoursSinceLastUpdate > 12 || (((int)(totalHoursLastUpdate + hoursSinceLastUpdate) / 12 ^ (int)totalHoursLastUpdate / 12) & 1) == 1)
            {
                roomness = GetRoomness();
            }
        }
        else
        {
            roomness = 0;
        }

        beginIntervalledUpdate(out var intervalCallback, out var endCallback);
        float delayGrowthBelowTemperature = msFarming.Config.DelayGrowthBelowTemperature;
        float lossPerDegree = msFarming.Config.LossPerDegree;

        // Fast forward in 3-4 hour intervalls
        while (hoursSinceLastUpdate > hourIntervall)
        {
            hoursSinceLastUpdate -= hourIntervall;
            totalHoursLastUpdate += hourIntervall;
            previousHourInterval = hourIntervall;

            hourIntervall = 3 + rand.NextDouble();

            /// This updates the temperature and rainfall in the conds supplied
            blockAccessor.GetClimateAt(Pos, conds, EnumGetClimateMode.ForSuppliedDate_TemperatureRainfallOnly, totalHoursLastUpdate / hoursPerDay);

            if (roomness > 0)
            {
                conds.Temperature += 5;
            }

            // Stop fertility recovery below zero degrees
            // 10% recovery speed at 1°C
            // 20% recovery speed at 2°C and so on
            double growthChance = 1 + (conds.Temperature - delayGrowthBelowTemperature) * lossPerDegree;    // Clamping this to 0,1 is not necessary, because rand.NextDouble() can only be between 0 and 1.  
            bool growthPaused = rand.NextDouble() > growthChance;

            intervalCallback?.Invoke(hourIntervall, conds, lightGrowthSpeedFactor, growthPaused);
        }

        endCallback?.Invoke();
    }


    protected virtual int GetRoomness()
    {
        Room room = msFarming.Roomreg?.GetRoomForPosition(upPos);
        return (room != null && room.SkylightCount > room.NonSkylightCount && room.ExitCount == 0) ? 1 : 0;
    }


    // Called if there was no 3-4 hour interval yet, sub-classes may want to do something here e.g. BESoilNutrition still checks moisturelevel
    protected virtual void ShortUpdate(float dt)
    {
    }

    protected virtual void onRollback(double hoursrolledback)
    {

    }

    /// <summary>
    /// Called when a update on a farmland block happens. Return a callback handler that is called for the fast forward simulation of this crop
    /// </summary>
    /// <returns></returns>
    protected virtual void beginIntervalledUpdate(out FarmlandFastForwardUpdate onInterval, out FarmlandUpdateEnd onEnd)
    {
        onInterval = null;
        onEnd = null;
    }


    public override void OnBlockPlaced(ItemStack byItemStack = null)
    {
        base.OnBlockPlaced(byItemStack);
        totalHoursLastUpdate = Api.World.Calendar.TotalHours;
    }


    public virtual void OnCreatedFromSoil(Block block, TreeAttribute existingFertilityData = null)
    {
        totalHoursLastUpdate = Api.World.Calendar.TotalHours;
    }


    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
    {
        base.FromTreeAttributes(tree, worldAccessForResolve);

        if (tree.HasAttribute("totalHoursFertilityCheck"))
        {
            totalHoursLastUpdate = tree.GetDouble("totalHoursFertilityCheck");
        }
        else
        {
            // Pre v1.5.1
            totalHoursLastUpdate = tree.GetDouble("totalDaysFertilityCheck") * 24;
        }

        roomness = tree.GetInt("roomness");
    }

    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        base.ToTreeAttributes(tree);
        tree.SetDouble("totalHoursFertilityCheck", totalHoursLastUpdate);
        tree.SetInt("roomness", roomness);
    }

    public double TotalHoursLastUpdate => totalHoursLastUpdate;

    public virtual void SendFullUpdateToClient(ICoreServerAPI sapi, IServerPlayer player)
    {
        // Forces an immediate roomness update so that the clientside BlockInfo is correct - as for performance roomness is normally only updated every 12 hours

        int oldRoomness = roomness;
        bool skyExposed = sapi.World.BlockAccessor.GetRainMapHeightAt(Pos.X, Pos.Z) <= Pos.Y + RainHeightOffset;
        roomness = skyExposed ? 0 : GetRoomness();

        if (roomness != oldRoomness) MarkDirty();
    }
}
