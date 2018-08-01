using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    // Epiphany!
    // Stratus clouds can be made with using fog that is only added with a vertical up gradient. No fog near sealevel!!!
    // "Stratus clouds are low-level clouds characterized by horizontal layering with a uniform base, as opposed to convective or cumuliform clouds that are formed by rising thermals. More specifically, the term stratus is used to describe flat, hazy, featureless clouds of low altitude varying in color from dark gray to nearly white."

    // https://en.wikipedia.org/wiki/Cloud#/media/File:Cloud_types.jpg
    // Cumulus: tall chunky bits, low altitude
    // Cirrocumulus: like white noise, high altitude
    // Cirrus: white stripes, high altitude
    // Altostratus: flat chunky bits, medium altitude
    // Altocumulus: small chunky bits, medium altitude
    // Nimbostratus: Very tall chunk bits, low till medium altitude
    // Cumulonimbus: super tall chunky bits, low till high altitude
    // Stratus: Very Flat chunky bits, low altitude
    // Stratocumulus: Flat chunky bits, below mid altitude


    public class WeatherSimulation
    {
        Random rand;


        public WeatherPattern NewPattern;
        public WeatherPattern OldPattern;
        public float Weight;

        public bool Transitioning;
        public float TransitionDelay;

        public AmbientModifier BlendedAmbient;
        

        public WeatherPattern[] Patterns;
        WeatherSystem ws;

        float smoothedLightLevel;

        public WeatherSimulation(WeatherSystem ws)
        {
            this.ws = ws;
            rand = new Random(ws.api.World.Seed);
            BlendedAmbient = new AmbientModifier().EnsurePopulated();

            if (ws.api.Side == EnumAppSide.Client)
            {
                ws.capi.Ambient.CurrentModifiers.InsertBefore("serverambient", "weather", BlendedAmbient);
            }

            WeatherPattern ClearSky = new WeatherPatternClearSky(ws, "ClearSky", 1);
            ClearSky.Ambient.FogDensity = new WeightedFloat(2 / 2000f, 1);

            WeatherPattern CumulusClouds = new WeatherPattern(ws, "Cumulus Clouds", 1)
            {
                CloudDensityNoise = new SimplexNoise(new double[] { 4 }, new double[] { 1.5 }, rand.Next()),
                CloudDensityOffsetNoise = new SimplexNoise(new double[] { 4 }, new double[] { 1.5 }, rand.Next()),
                CloudOffsetYNoise = new SimplexNoise(new double[] { 2 }, new double[] { 1.5 }, rand.Next())
            };

            WeatherPattern StratusClouds = CumulusClouds.Clone();
            StratusClouds.Name = "Stratus Clouds";
            StratusClouds.Ambient.FlatFogYPos = new WeightedFloat(25, 1);
            StratusClouds.Ambient.FlatFogDensity = new WeightedFloat(6 / 250f, 1);
            StratusClouds.Ambient.FogDensity = new WeightedFloat(10 / 2000f, 1);
            StratusClouds.Chance = 0.35f;

            WeatherPattern CumulusCloudsWithFlatMist = CumulusClouds.Clone();
            CumulusCloudsWithFlatMist.Name = "Cumulus Clouds + Flat dense Mist";
            CumulusCloudsWithFlatMist.Ambient.FlatFogYPos = new WeightedFloat(5, 1);
            CumulusCloudsWithFlatMist.Ambient.FlatFogDensity = new WeightedFloat(-100/250f, 1);
            CumulusCloudsWithFlatMist.BeginUse += () =>
            {
                CumulusCloudsWithFlatMist.Ambient.FlatFogDensity.Value = (-50 - 50 * (float)rand.NextDouble()) / 250f;
                CumulusCloudsWithFlatMist.Ambient.FlatFogYPos.Value = -1 - 7 * (float)rand.NextDouble();
            };

            CumulusCloudsWithFlatMist.ChanceOfWeatherChange = 0.01f;
            CumulusCloudsWithFlatMist.Chance = 0.35f;

            WeatherPattern CumulusCloudsWithTallMist = CumulusClouds.Clone();
            CumulusCloudsWithTallMist.Name = "Cumulus Clouds + Tall dense Mist";
            CumulusCloudsWithTallMist.Ambient.FlatFogYPos = new WeightedFloat(40, 1);
            CumulusCloudsWithTallMist.Ambient.FlatFogDensity = new WeightedFloat(-30 / 250f, 1);
            CumulusCloudsWithTallMist.BeginUse += () => { CumulusCloudsWithTallMist.Ambient.FlatFogDensity.Value = (-15 - 15 * (float)rand.NextDouble()) / 250f; };
            CumulusCloudsWithTallMist.ChanceOfWeatherChange = 0.01f;
            CumulusCloudsWithTallMist.Chance = 0.35f;

            WeatherPattern CumulusCloudsWithFog = CumulusClouds.Clone();
            CumulusCloudsWithFog.Name = "Cumulus Clouds + Fog";
            CumulusCloudsWithFog.Ambient.FogDensity = new WeightedFloat(40 / 2000f, 1);
            CumulusCloudsWithFog.BeginUse += () => { CumulusCloudsWithFog.Ambient.FogDensity.Value = (10 + 30 * (float)rand.NextDouble()) / 2000f; };
            CumulusCloudsWithFog.Chance = 0.35f;

            WeatherPattern NimboStratusClouds = new WeatherPattern(ws, "Nimbostratus Clouds", 1)
            {
                CloudDensityNoise = new SimplexNoise(new double[] { 4 }, new double[] { 1.5 }, rand.Next()),
                CloudDensityOffsetNoise = new SimplexNoise(new double[] { 4 }, new double[] { 1.5 }, rand.Next()),
                CloudOffsetYNoise = new SimplexNoise(new double[] { 2 }, new double[] { 1.5 }, rand.Next())
            };

            WeatherPattern AltoCumulusClouds = new WeatherPattern(ws, "Altocumulus Clouds", 1)
            {
                CloudDensityNoise = new SimplexNoise(new double[] { 3 }, new double[] { 10 }, rand.Next()),
                CloudDensityOffsetNoise = new SimplexNoise(new double[] { 4 }, new double[] { 1.5 }, rand.Next()),
                CloudOffsetYNoise = new SimplexNoise(new double[] { 1 }, new double[] { 1.5 }, rand.Next())
            };
            

            WeatherPattern CirroCumulusClouds = new WeatherPattern(ws, "Cirrocumulus Clouds", 1)
            {
                CloudDensityNoise = new SimplexNoise(new double[] { 3 }, new double[] { 10 }, rand.Next()),
                CloudDensityOffsetNoise = new SimplexNoise(new double[] { 4 }, new double[] { 1.5 }, rand.Next()),
                CloudOffsetYNoise = new SimplexNoise(new double[] { 1 }, new double[] { 1.5 }, rand.Next())
            };
            CirroCumulusClouds.CloudYOffset = 100;

            Patterns = new WeatherPattern[]
            {
                ClearSky, CumulusClouds, CumulusCloudsWithFlatMist, CumulusCloudsWithTallMist, CumulusCloudsWithFog, StratusClouds, NimboStratusClouds, AltoCumulusClouds, CirroCumulusClouds
            };            
        }


        internal void LoadRandomPattern()
        {
            NewPattern = RandomPattern();
            OldPattern = RandomPattern();
            Weight = 1;
        }

        internal void Initialize()
        {
            for (int i = 0; i < Patterns.Length; i++)
            {
                Patterns[i].Initialize(i);
            }

            if (ws.api.Side == EnumAppSide.Client)
            {
                smoothedLightLevel = ws.capi.World.BlockAccessor.GetLightLevel(ws.capi.World.Player.Entity.Pos.AsBlockPos, EnumLightLevelType.OnlySunLight);
            }
        }

        public void Update(float dt)
        {
            if (ws.api.Side == EnumAppSide.Client)
            {
                int lightlevel = ws.capi.World.BlockAccessor.GetLightLevel(ws.capi.World.Player.Entity.Pos.AsBlockPos, EnumLightLevelType.OnlySunLight);
                smoothedLightLevel += (lightlevel - smoothedLightLevel) * dt * 4;


                // light level > 17 = 100% fog
                // light level <= 2 = 0% fog
                float fogMultiplier = GameMath.Clamp(smoothedLightLevel / 20f, 0f, 1);

                // sealevel = 100% fog
                // 70% sealevel = 0% fog
                float fac = (float)GameMath.Clamp(ws.capi.World.Player.Entity.Pos.Y / ws.capi.World.SeaLevel, 0, 1);
                fac *= fac;
                fogMultiplier *= fac;



                BlendedAmbient.FlatFogDensity.Value = NewPattern.Ambient.FlatFogDensity.Value * Weight + OldPattern.Ambient.FlatFogDensity.Value * (1 - Weight);
                BlendedAmbient.FlatFogDensity.Value *= fogMultiplier;

                BlendedAmbient.FlatFogDensity.Weight = NewPattern.Ambient.FlatFogDensity.Weight * Weight + OldPattern.Ambient.FlatFogDensity.Weight * (1 - Weight);

                BlendedAmbient.FlatFogYPos.Value = NewPattern.Ambient.FlatFogYPos.Value * Weight + OldPattern.Ambient.FlatFogYPos.Value * (1 - Weight);
                BlendedAmbient.FlatFogYPos.Weight = GameMath.Clamp(NewPattern.Ambient.FlatFogYPos.Weight + OldPattern.Ambient.FlatFogYPos.Weight, 0, 1);

                BlendedAmbient.FogDensity.Value = ws.capi.Ambient.Base.FogDensity.Value + NewPattern.Ambient.FogDensity.Value * Weight + OldPattern.Ambient.FogDensity.Value * (1 - Weight);
                BlendedAmbient.FogDensity.Value *= fogMultiplier;
                BlendedAmbient.FogDensity.Weight = GameMath.Clamp(NewPattern.Ambient.FogDensity.Weight + OldPattern.Ambient.FogDensity.Weight, 0, 1);
            }

            if (Transitioning)
            {
                Weight += dt / TransitionDelay;

                if (Weight > 1)
                {
                    Transitioning = false;
                    Weight = 1;
                }
            } else
            {
                if (ws.api.Side == EnumAppSide.Server && rand.NextDouble() < OldPattern.ChanceOfWeatherChange * ws.api.World.Calendar.SpeedOfTime / 60.0)
                {
                    TriggerTransition();
                }

            }

            if (ws.api.Side == EnumAppSide.Client)
            {
                NewPattern.Update(dt);
                OldPattern.Update(dt);
            }
        }

        public void TriggerTransition()
        {
            TriggerTransition(30 + (float)rand.NextDouble() * 60 * 60 / ws.api.World.Calendar.SpeedOfTime);
        }

        public void TriggerTransition(float delay)
        {
            Transitioning = true;
            TransitionDelay = delay;

            Weight = 0;
            OldPattern = NewPattern;
            NewPattern = RandomPattern();
            if (NewPattern != OldPattern) NewPattern.OnBeginUse();

            ws.serverChannel.BroadcastPacket(new WeatherState()
            {
                NewPatternIndex = NewPattern.Index,
                OldPatternIndex = OldPattern.Index,
                TransitionDelay = TransitionDelay,
                Transitioning = true
            });
        }
       
        public WeatherPattern RandomPattern()
        {
            float totalChance = 0;
            for (int i = 0; i < Patterns.Length; i++) totalChance += Patterns[i].Chance;

            float rndVal = (float)rand.NextDouble() * totalChance;
            for (int i = 0; i < Patterns.Length; i++)
            {
                rndVal -= Patterns[i].Chance;
                if (rndVal <= 0)
                {
                    return Patterns[i];
                }
            }

            return Patterns[Patterns.Length - 1];
        }
   

        public double GetBlendedCloudDensityAt(int dx, int dz)
        {
            return ws.capi.Ambient.BlendedCloudDensity + NewPattern.GetCloudDensityAt(dx, dz) * Weight + OldPattern.GetCloudDensityAt(dx, dz) * (1 - Weight);
        }

        public double GetBlendedCloudOffsetYAt(int dx, int dz)
        {
            return NewPattern.GetCloudOffsetYAt(dx, dz) * Weight + OldPattern.GetCloudOffsetYAt(dx, dz) * (1 - Weight);
        }

        internal void EnsureNoiseCacheIsFresh()
        {
            NewPattern.EnsureNoiseCacheIsFresh();
            OldPattern.EnsureNoiseCacheIsFresh();
        }
    }

    public class WeatherPatternClearSky : WeatherPattern
    {
        public WeatherPatternClearSky(WeatherSystem ws, string name, float chance) : base(ws, name, chance)
        {
        }

        public override double GetCloudDensityAt(int dx, int dz)
        {
            return -3;
        }

        public override double GetCloudOffsetYAt(int dx, int dz)
        {
            return 0;
        }

        public override void EnsureNoiseCacheIsFresh()
        {
            
        }

        public override void RegenNoiseCache()
        {
            
        }

        public override void Update(float dt)
        {
            
        }
    }

    


}
