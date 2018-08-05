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
    public class WeatherPattern
    {
        public float Chance = 1f;
        public float OverallDensityOffset = -0.35f;
        public float FlatFogDensityOffset = 0f;
        public float CloudYOffset = 0f;

        public float ChanceOfWeatherChange = 0.001f;

        public string Name;

        public AmbientModifier Ambient = new AmbientModifier().EnsurePopulated();

        public SimplexNoise CloudDensityNoise;
        public SimplexNoise CloudDensityOffsetNoise;
        public SimplexNoise CloudOffsetYNoise;
        public SimplexNoise FlatFog;

        WeatherSystem ws;
        ICoreAPI api;
        double densityOffset;

        int lastTileX, lastTileZ;
        public double[,] CloudDensityNoiseCache;
        public double[,] CloudOffsetYNoiseCache;

        public event API.Common.Action BeginUse;


        public WeatherPattern(WeatherSystem ws, string name, float chance)
        {
            this.ws = ws;
            api = ws.api;
            this.Name = name;
            this.Chance = chance;
        }

        public virtual string GetWeatherName()
        {
            return Name + string.Format(" (doff={0})", (densityOffset + OverallDensityOffset).ToString("0.##"));
        }

        public virtual void EnsureNoiseCacheIsFresh()
        {
            bool unfresh =
                CloudDensityNoiseCache == null ||
                lastTileX != ws.CloudTileX || lastTileZ != ws.CloudTileZ ||
                ws.CloudTileLength != CloudDensityNoiseCache.GetLength(1)
            ;

            if (unfresh)
            {
                RegenNoiseCache();

                return;
            }
        }

        public virtual void RegenNoiseCache()
        {
            int len = ws.CloudTileLength;

            CloudDensityNoiseCache = new double[len, len];
            CloudOffsetYNoiseCache = new double[len, len];

            lastTileX = ws.CloudTileX;
            lastTileZ = ws.CloudTileZ;

            double timeAxis = api.World.Calendar.TotalDays / 10.0;

            for (int dx = 0; dx < len; dx++)
            {
                for (int dz = 0; dz < len; dz++)
                {
                    double x = (lastTileX + dx - len / 2) / 20.0;
                    double z = (lastTileZ + dz - len / 2) / 20.0;

                    CloudDensityNoiseCache[dx, dz] = CloudDensityNoise.Noise(x, z, timeAxis) / 2;
                    CloudOffsetYNoiseCache[dx, dz] = CloudOffsetYNoise.Noise(x, z, timeAxis) / 2;
                }
            }
        }

        public virtual void OnBeginUse()
        {
            BeginUse?.Invoke();
        }

        public virtual void Update(float dt)
        {
            densityOffset = CloudDensityOffsetNoise.Noise(api.World.Calendar.TotalHours / 10.0, 0) / 2;

            EnsureNoiseCacheIsFresh();
        }

        public virtual double GetCloudDensityAt(int dx, int dz)
        {
            return GameMath.Clamp(CloudDensityNoiseCache[dx, dz] + densityOffset + OverallDensityOffset, 0, 1);
        }

        public virtual double GetCloudOffsetYAt(int dx, int dz)
        {
            return 100 * CloudOffsetYNoiseCache[dx, dz] + CloudYOffset;
        }

        public WeatherPattern Clone()
        {
            return new WeatherPattern(ws, Name, Chance)
            {
                Ambient = Ambient.Clone().EnsurePopulated(),
                CloudDensityNoise = CloudDensityNoise.Clone(),
                CloudDensityOffsetNoise = CloudDensityOffsetNoise.Clone(),
                CloudOffsetYNoise = CloudOffsetYNoise.Clone(),
                CloudYOffset = CloudYOffset,
                FlatFogDensityOffset = FlatFogDensityOffset
            };
        }

        public int Index = 0;

        public virtual void Initialize(int index)
        {
            this.Index = index;
            EnsureNoiseCacheIsFresh();
        }
    }
}
