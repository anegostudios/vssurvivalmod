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
            }, 3500, rand.Next(3500));
        }
    }

    protected virtual void Update(float dt)
    {
        double nowTotalHours = Api.World.Calendar.TotalHours;
        double hourIntervall = 3 + rand.NextDouble();
        bool skyExposed = Api.World.BlockAccessor.GetRainMapHeightAt(Pos.X, Pos.Z) <= Pos.Y + RainHeightOffset;

        if ((nowTotalHours - totalHoursLastUpdate) < hourIntervall)
        {
            if (totalHoursLastUpdate > nowTotalHours)
            {
                // We need to rollback time when the blockEntity saved date is ahead of the calendar date: can happen if a schematic is imported
                double rollback = totalHoursLastUpdate - nowTotalHours;
                onRollback(rollback);
                totalHoursLastUpdate = nowTotalHours;
            }
            else
            {
                
                return;
            }
        }

        // Slow down growth on bad light levels
        int lightpenalty = 0;
        if (!allowundergroundfarming)
        {
            lightpenalty = Math.Max(0, Api.World.SeaLevel - Pos.Y);
        }

        int sunlight = Api.World.BlockAccessor.GetLightLevel(upPos, allowundergroundfarming ? EnumLightLevelType.MaxLight : EnumLightLevelType.OnlySunLight);
        double lightGrowthSpeedFactor = GameMath.Clamp(1 - (msFarming.Config.DelayGrowthBelowSunLight - (sunlight - lightpenalty)) * msFarming.Config.LossPerLevel, 0, 1);
        


        // Don't update more than a year
        totalHoursLastUpdate = Math.Max(totalHoursLastUpdate, nowTotalHours - Api.World.Calendar.DaysPerYear * Api.World.Calendar.HoursPerDay);

        if (!skyExposed) // Fast pre-check
        {
            Room room = msFarming.Roomreg?.GetRoomForPosition(upPos);
            roomness = (room != null && room.SkylightCount > room.NonSkylightCount && room.ExitCount == 0) ? 1 : 0;
        }
        else
        {
            roomness = 0;
        }
        
        ClimateCondition conds = null;

        beginIntervalledUpdate(out var intervalCallback, out var endCallback);

        // Fast forward in 3-4 hour intervalls
        while ((nowTotalHours - totalHoursLastUpdate) > hourIntervall)
        {
            totalHoursLastUpdate += hourIntervall;
            previousHourInterval = hourIntervall;

            hourIntervall = 3 + rand.NextDouble();

            if (conds == null)
            {
                conds = Api.World.BlockAccessor.GetClimateAt(Pos, EnumGetClimateMode.ForSuppliedDate_TemperatureRainfallOnly, totalHoursLastUpdate / Api.World.Calendar.HoursPerDay);
                if (conds == null) break;
            }
            else
            {
                Api.World.BlockAccessor.GetClimateAt(Pos, conds, EnumGetClimateMode.ForSuppliedDate_TemperatureRainfallOnly, totalHoursLastUpdate / Api.World.Calendar.HoursPerDay);
            }


            if (roomness > 0)
            {
                conds.Temperature += 5;
            }

            // Stop fertility recovery below zero degrees
            // 10% recovery speed at 1°C
            // 20% recovery speed at 2°C and so on
            double growthChance = GameMath.Clamp((conds.Temperature / 10f) * lightGrowthSpeedFactor, 0, 10);
            bool growthPaused = rand.NextDouble() > growthChance;

            intervalCallback?.Invoke(hourIntervall, conds, lightGrowthSpeedFactor, growthPaused);
        }

        endCallback?.Invoke();
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
    }
    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        base.ToTreeAttributes(tree);
        tree.SetDouble("totalHoursFertilityCheck", totalHoursLastUpdate);
    }

    public double TotalHoursLastUpdate => totalHoursLastUpdate;
}
