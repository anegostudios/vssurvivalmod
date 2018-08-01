using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class WeatherSnapshot
    {
        public double hoursTotal;
        public Dictionary<long, float[]> WeatherNoiseByChunk = new Dictionary<long, float[]>();
    }
    
    public class WeatherPatternSimulation
    {
        int worldSeed;
        IWorldAccessor world;
        NormalizedSimplexNoise[] NoiseGens;

        WeatherSnapshot snap1 = new WeatherSnapshot();
        WeatherSnapshot snap2 = new WeatherSnapshot();

        int chunksize;

        public WeatherPatternSimulation(int worldSeed, IWorldAccessor world)
        {
            this.worldSeed = worldSeed;
            this.world = world;

            chunksize = world.BlockAccessor.ChunkSize;

            //NoiseGens = new NormalizedPerlinNoise[Enum.GetValues(typeof(EnumWeatherPattern)).Length];
            for (int i = 0; i < NoiseGens.Length; i++)
            {
                NoiseGens[i] = NormalizedSimplexNoise.FromDefaultOctaves(3, 0.1, 0.8, worldSeed + i + 1231212);
            }
        }


        public void OnUpdate(float dt)
        {
            snap1 = snap2;
            snap2 = new WeatherSnapshot() {
                hoursTotal = world.Calendar.TotalHours
            };

            IPlayer[] players;

            if (world is IClientWorldAccessor)
            {
                players = new IPlayer[] { ((IClientWorldAccessor)world).Player };
            } else
            {
                players = world.AllOnlinePlayers;
            }

            for (int i = 0; i < players.Length; i++)
            {
                int viewdistance = players[i].WorldData.LastApprovedViewDistance;
                int chunksize = world.BlockAccessor.ChunkSize;
                int chunkRadius = (int)Math.Ceiling((float)viewdistance / chunksize) + 1;
                Vec2i[] points = ShapeUtil.GetOctagonPoints(
                    (int)players[i].Entity.Pos.X / chunksize,
                    (int)players[i].Entity.Pos.Z / chunksize,
                    chunkRadius
                );

                GenWeatherForPoints(points, snap2);
            }
        }


        private void GenWeatherForPoints(Vec2i[] points, WeatherSnapshot intoSnap)
        {
            for (int i = 0; i < points.Length; i++)
            {
                long chunkIndex2d = ((points[i].X + chunksize / 2) << 32) | (points[i].Y + chunksize / 2);

                if (!intoSnap.WeatherNoiseByChunk.ContainsKey(chunkIndex2d))
                {
                    intoSnap.WeatherNoiseByChunk[chunkIndex2d] = CalcWeatherAt(points[i].X, points[i].Y, intoSnap.hoursTotal);
                }
            }
        }

        public void GetWeather(double x, double z, ref float[] weather)
        {
            if (!TryGetCachedWeather(x, z, ref weather))
            {
                CacheWeatherAt(x, z);
            }

            TryGetCachedWeather(x, z, ref weather);
        }



        private void CacheWeatherAt(double x, double z)
        {
            int cX = (int)Math.Round(x / chunksize);
            int cZ = (int)Math.Round(z / chunksize);

            Vec2i[] points = new Vec2i[]
            {
                new Vec2i(cX, cZ),
                new Vec2i(cX - 1, cZ),
                new Vec2i(cX, cZ - 1),
                new Vec2i(cX - 1, cZ - 1),
            };

            GenWeatherForPoints(points, snap1);
            GenWeatherForPoints(points, snap2);
        }

        public bool TryGetCachedWeather(double x, double z, ref float[] weather)
        {
            int cX = (int)Math.Round(x / chunksize);
            int cZ = (int)Math.Round(z / chunksize);

            int startX = (cX - 1) * chunksize + chunksize / 2;
            int startZ = (cZ - 1) * chunksize + chunksize / 2;
            float dx = (float)((x - startX) / chunksize);
            float dz = (float)((z - startZ) / chunksize);

            double hoursTotal = world.Calendar.TotalHours;
            double timespan = snap2.hoursTotal - snap1.hoursTotal;

            long topleft = ((cX - 1) << 32) | (cZ - 1);
            long topright = (cX << 32) | (cZ - 1);
            long bottomright = (cX << 32) | cZ;
            long bottomleft = ((cX - 1) << 32) | cZ;

            bool success = true;

            float[] wtl1=null, wtr1 = null, wbr1 = null, wbl1 = null, wtl2 = null, wtr2 = null, wbr2 = null, wbl2 = null;

            success = success && snap1.WeatherNoiseByChunk.TryGetValue(topleft, out wtl1);
            success = success && snap1.WeatherNoiseByChunk.TryGetValue(topright, out wtr1);
            success = success && snap1.WeatherNoiseByChunk.TryGetValue(bottomright, out wbr1);
            success = success && snap1.WeatherNoiseByChunk.TryGetValue(bottomleft, out wbl1);

            success = success && snap2.WeatherNoiseByChunk.TryGetValue(topleft, out wtl2);
            success = success && snap2.WeatherNoiseByChunk.TryGetValue(topright, out wtr2);
            success = success && snap2.WeatherNoiseByChunk.TryGetValue(bottomright, out wbr2);
            success = success && snap2.WeatherNoiseByChunk.TryGetValue(bottomleft, out wbl2);

            if (!success) return false;

            for (int i = 0; i < weather.Length; i++)
            {
                float weatherSnap1 = GameMath.BiLerp(wtl1[i], wtr1[i], wbr1[i], wbl1[i], dx, dz);
                float weatherSnap2 = GameMath.BiLerp(wtl2[i], wtr2[i], wbr2[i], wbl2[i], dx, dz);

                weather[i] = GameMath.Lerp(weatherSnap1, weatherSnap2, (float)((hoursTotal - snap1.hoursTotal) / timespan));
            }

            return true;
        }


        private float[] CalcWeatherAt(int x, int z, double hoursTotal)
        {
            float[] weather = new float[NoiseGens.Length];
            for (int i = 0; i < weather.Length; i++)
            {
                weather[i] = (float)NoiseGens[i].Noise(x, hoursTotal, z);
            }

            return weather;
        }
    }
}
