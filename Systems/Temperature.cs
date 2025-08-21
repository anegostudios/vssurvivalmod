using System;
using System.Collections.Generic;
using System.IO;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

#nullable disable

namespace Vintagestory.GameContent
{

    /// <summary>
    /// Season, Location and Day/Night cycle aware temperature retrieval system
    /// </summary>
    public class ModTemperature : ModSystem
    {
        ICoreAPI api;

        
        public SimplexNoise YearlyTemperatureNoise;
        public SimplexNoise DailyTemperatureNoise;

        public override bool ShouldLoad(EnumAppSide side)
        {
            return true;
        }

        public override void Start(ICoreAPI api)
        {
            this.api = api;
            api.Event.OnGetClimate += Event_OnGetClimate;

            YearlyTemperatureNoise = SimplexNoise.FromDefaultOctaves(3, 0.001, 0.95, api.World.Seed + 12109);
            DailyTemperatureNoise = SimplexNoise.FromDefaultOctaves(3, 1, 0.95, api.World.Seed + 128109);
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            api.ChatCommands.GetOrCreate("debug")
            .BeginSubCommand("exptempplot")
                .WithDescription("Export a 1 year long temperatures at a 6 hour interval at this location")
                .RequiresPrivilege(Privilege.controlserver)
                .RequiresPlayer()
                .HandleWith(args =>
                {
                    exportPlotHere(args.Caller.Entity.Pos.AsBlockPos);
                    return TextCommandResult.Success("ok exported");
                })
            .EndSubCommand();
        }

        private void Event_OnGetClimate(ref ClimateCondition climate, BlockPos pos, EnumGetClimateMode mode, double totalDays)
        {
            if (mode == EnumGetClimateMode.WorldGenValues) return;

            double yearRel = api.World.Calendar.YearRel;
            double hourOfDay = (totalDays % 1) * api.World.Calendar.HoursPerDay;

            updateTemperature(ref climate, pos, yearRel, hourOfDay, totalDays);
        }

        private void updateTemperature(ref ClimateCondition climate, BlockPos pos, double yearRel, double hourOfDay, double totalDays)
        {
            // 1. Global average temperature at this location
            double heretemp = climate.WorldGenTemperature;

            // 2. season based temperature
            // - Near the equator the variation seems to be only 5-10 degrees
            // - In european cities the per month average temperature seem to vary by about 20 degrees (~ 0 - 20)
            // - Above the arctic circle it seems to vary by up to 60 degrees (~ -39 - 20)

            // -1 for south pole, 0 for equater, 1 for north pole
            double latitude = api.World.Calendar.OnGetLatitude(pos.Z);
            double seasonalVariationAmplitude = Math.Abs(latitude) * 65;

            heretemp -= seasonalVariationAmplitude / 2;

            float? seasonOverride = api.World.Calendar.SeasonOverride;

            if (seasonOverride != null)
            {
                double distanceToJanuary = GameMath.Smootherstep(Math.Abs(GameMath.CyclicValueDistance(0.5f, (float)seasonOverride * 12, 12) / 6f));
                heretemp += seasonalVariationAmplitude * distanceToJanuary;
            }
            else
            {

                // 1 to 0 => january is coldest month
                // 0 to -1 => july is coldest month
                if (latitude > 0)
                {
                    double distanceToJanuary = GameMath.Smootherstep(Math.Abs(GameMath.CyclicValueDistance(0.5f, yearRel * 12, 12) / 6f));
                    heretemp += seasonalVariationAmplitude * distanceToJanuary;
                }
                else
                {
                    double distanceToJuly = GameMath.Smootherstep(Math.Abs(GameMath.CyclicValueDistance(6.5f, yearRel * 12, 12) / 6f));
                    heretemp += seasonalVariationAmplitude * distanceToJuly;
                }
            }

            // 3. diurnal temperature variation:
            // https://en.wikipedia.org/wiki/Diurnal_temperature_variation

            // Variation is higher in dry areas
            double diurnalVariationAmplitude = 18 - climate.Rainfall * 13;

            // just before sunrise is the coldest time. We have no time zones in VS
            // lets just hardcode 4 am for this for now, making 4pm the warmest time
            double distanceTo6Am = GameMath.SmoothStep(Math.Abs(GameMath.CyclicValueDistance(4, hourOfDay, 24) / 12f));
            heretemp += (distanceTo6Am - 0.5) * diurnalVariationAmplitude;

            // 4. Yearly random noise
            heretemp += YearlyTemperatureNoise.Noise(totalDays, 0) * 3;

            // 5. Daily random noise
            heretemp += DailyTemperatureNoise.Noise(totalDays, 0);

            climate.Temperature = (float)heretemp;
        }


        void exportPlotHere(BlockPos pos)
        {
            ClimateCondition cond = api.World.BlockAccessor.GetClimateAt(pos);
            double totalhours = 0;// api.World.Calendar.TotalHours;
            double starttemp = cond.Temperature;

            double hoursPerday = api.World.Calendar.HoursPerDay;
            double daysPerYear = api.World.Calendar.DaysPerYear;
            double daysPerMonth = api.World.Calendar.DaysPerMonth;

            double monthsPerYear = daysPerYear / daysPerMonth;

            List<string> entries = new List<string>();

            for (double plothours = 0; plothours < 24*144; plothours += 1)
            {
                cond.Temperature = (float)starttemp;

                double totalDays = totalhours / hoursPerday;
                double yearRel = api.World.Calendar.YearRel;
                double hourOfDay = totalhours % hoursPerday;
                double month = yearRel * monthsPerYear;

                updateTemperature(ref cond, pos, yearRel, hourOfDay, totalDays);

                entries.Add(
                    string.Format("{0}.{1}.{2} {3}:00", (int)(totalDays % daysPerMonth) + 1, (int)month + 1, (int)(totalDays / daysPerYear + IGameCalendar.StartYear), (int)hourOfDay)
                    +
                    ";" + cond.Temperature
                );

                totalhours += 1;
            }

            File.WriteAllText("temperatureplot.csv", string.Join("\r\n", entries));
        }
    }

}
