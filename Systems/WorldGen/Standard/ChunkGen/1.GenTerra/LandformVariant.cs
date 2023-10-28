using Newtonsoft.Json;
using System;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Common;
using Vintagestory.API.Util;

namespace Vintagestory.ServerMods.NoObf
{
    public class LandformVariant : WorldPropertyVariant
    {
        [JsonIgnore]
        public int index;
        [JsonIgnore]
        public float[] TerrainYThresholds;
        [JsonIgnore]
        public int ColorInt;
        [JsonIgnore]
        public double WeightTmp; // Temporary helper value 
        
        
        [JsonProperty]
        public string HexColor;
        [JsonProperty]
        public double Weight;
        [JsonProperty]
        public bool UseClimateMap;
        [JsonProperty]
        public float MinTemp = -50;
        [JsonProperty]
        public float MaxTemp = 50;
        [JsonProperty]
        public int MinRain = 0;
        [JsonProperty]
        public int MaxRain = 255;

        [JsonProperty]
        public bool UseWindMap;
        [JsonProperty]
        public int MinWindStrength;
        [JsonProperty]
        public int MaxWindStrength;


        [JsonProperty]
        public double[] TerrainOctaves;
        [JsonProperty]
        public double[] TerrainOctaveThresholds = new double[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
        [JsonProperty]
        public float[] TerrainYKeyPositions;
        [JsonProperty]
        public float[] TerrainYKeyThresholds;
        [JsonProperty]
        public LandformVariant[] Mutations = new LandformVariant[0];
        // Mutation chance
        [JsonProperty]
        public float Chance = 0f;


        // What if we the horizontal plane could modify the terrain thresholds?
        // Example: A heavily perlin wobbled map that creates lots of stripes should 
        // give us ripples or dents in the terrain
        // Should probably only be taken into calculation if the base thresholds is a above a certain value (e.g. so only hills/mountains are affected)

        static Random rnd = new Random();

        public void Init(IWorldManagerAPI api, int index)
        {
            this.index = index;

            expandOctaves(api);

            LerpThresholds(api.MapSizeY);
            this.ColorInt = rnd.Next(int.MaxValue) | (255 << 24);
        }

        // Adds additional octaves to prevent super smooth worldgen on large world heights
        protected void expandOctaves(IWorldManagerAPI api)
        {
            int octaves = TerraGenConfig.GetTerrainOctaveCount(api.MapSizeY);
            int m = octaves - TerrainOctaves.Length;
            if (m > 0)
            {
                var ext = new double[m].Fill(TerrainOctaves[TerrainOctaves.Length - 1]);
                double addSum = 0;
                
                for (int i = 0; i < ext.Length; i++)
                {
                    var val = Math.Pow(0.8, i + 1);
                    ext[i] *= val;
                    addSum += val;
                }

                double prevSum = 0;
                for (int i = 0; i < TerrainOctaves.Length; i++)
                {
                    prevSum += TerrainOctaves[i];
                }
                for (int i = 0; i < TerrainOctaves.Length; i++)
                {
                    TerrainOctaves[i] *= (prevSum + addSum) / prevSum;
                }

                TerrainOctaves = TerrainOctaves.Append(ext);
            }
            int n = octaves - TerrainOctaveThresholds.Length;
            if (n > 0)
            {
                TerrainOctaveThresholds = TerrainOctaveThresholds.Append(new double[n].Fill(TerrainOctaveThresholds[TerrainOctaveThresholds.Length - 1]));
            }
        }


        void LerpThresholds(int mapSizeY)
        {
            TerrainYThresholds = new float[mapSizeY];

            float curThreshold = 1;
            float curThresholdY = 0;
            int curThresholdPos = -1;

            for (int y = 0; y < mapSizeY; y++)
            {
                if (curThresholdPos + 1 >= TerrainYKeyThresholds.Length)
                {
                    TerrainYThresholds[y] = 1; // We need inverted value
                    continue;
                } else
                {
                    if (y >= (TerrainYKeyPositions[curThresholdPos + 1] * mapSizeY))
                    {
                        curThreshold = TerrainYKeyThresholds[curThresholdPos + 1];
                        curThresholdY = (TerrainYKeyPositions[curThresholdPos + 1] * mapSizeY);
                        curThresholdPos++;
                    }
                }

                float nextThreshold = 0;
                float nextThresholdY = mapSizeY;
                if (curThresholdPos + 1 < TerrainYKeyThresholds.Length)
                {
                    nextThreshold = TerrainYKeyThresholds[curThresholdPos + 1];
                    nextThresholdY = (TerrainYKeyPositions[curThresholdPos + 1] * mapSizeY);
                }

                float range = nextThresholdY - curThresholdY;
                float distance = (y - curThresholdY) / range;

                if (range == 0)
                {
                    string pos = "";
                    for (int i = 0; i < TerrainYKeyPositions.Length; i++)
                    {
                        if (i > 0) pos += ", ";
                        pos += TerrainYKeyPositions[i] * mapSizeY;
                    }
                    throw new Exception("Illegal TerrainYKeyPositions in landforms.js, Landform " + Code + ", key positions must be more than 0 blocks apart. Translated key positions for this maps world height: " + pos);
                }
                
                TerrainYThresholds[y] = 1 - GameMath.Lerp(curThreshold, nextThreshold, distance); // We need inverted lerped value
            }
        }


        public float[] AddTerrainNoiseThresholds(float[] thresholds, float weight)
        {
            for (int y = 0; y < thresholds.Length; y++)
            {
                thresholds[y] += weight * TerrainYThresholds[y];
            }

            return thresholds;
        }

    }
}
