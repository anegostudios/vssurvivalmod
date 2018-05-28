using Vintagestory.API;
using Newtonsoft.Json;
using System;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Common;

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
        public float WeightTmp; // Temporary helper value 
        
        
        [JsonProperty]
        public string HexColor;
        [JsonProperty]
        public float Weight; // 0..1
        [JsonProperty]
        public bool UseClimateMap;
        [JsonProperty]
        public int MinTemp;
        [JsonProperty]
        public int MaxTemp;
        [JsonProperty]
        public int MinRain;
        [JsonProperty]
        public int MaxRain;

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



        public void Init(IWorldManagerAPI api, int index)
        {
            this.index = index;
            LerpThresholds(api.MapSizeY);
            ColorInt = int.Parse(HexColor.TrimStart('#'), System.Globalization.NumberStyles.HexNumber);
            LoadAndNormalizeAmplitudes();
        }


        internal virtual void LoadAndNormalizeAmplitudes()
        {
            //double normalizationValue = 0;

            /*for (int i = 0; i < TerrainOctaves.Length; i++)
            {
                // Our noise gen doesn't exactly generate noise between 0..1 so NormalizedNoise.cs uses some heuristics to achieve that
                normalizationValue += TerrainOctaves[i]; // * Math.Pow(0.74 - 0.1, i + 1); 
            }

            double[] scaledAmplitudes = new double[TerraGenConfig.terrainGenOctaves];

            for (int i = 0; i < Math.Min(scaledAmplitudes.Length, TerrainOctaves.Length); i++)
            {
                scaledAmplitudes[i] = TerrainOctaves[i] / normalizationValue;
            }*/

            //TerrainOctaves = scaledAmplitudes;
        }



        void LerpThresholds(int mapSizeY)
        {
            TerrainYThresholds = new float[mapSizeY];

            float curThreshold = 1;
            int curThresholdY = 0;
            int curThresholdPos = -1;

            for (int y = 0; y < mapSizeY; y++)
            {
                if (curThresholdPos + 1 >= TerrainYKeyThresholds.Length)
                {
                    TerrainYThresholds[y] = 1; // We need inverted value
                    continue;
                } else
                {
                    if (y >= (int)(TerrainYKeyPositions[curThresholdPos + 1] * mapSizeY))
                    {
                        curThreshold = TerrainYKeyThresholds[curThresholdPos + 1];
                        curThresholdY = (int)(TerrainYKeyPositions[curThresholdPos + 1] * mapSizeY);
                        curThresholdPos++;
                    }
                }

                float nextThreshold = 0;
                int nextThresholdY = mapSizeY;
                if (curThresholdPos + 1 < TerrainYKeyThresholds.Length)
                {
                    nextThreshold = TerrainYKeyThresholds[curThresholdPos + 1];
                    nextThresholdY = (int)(TerrainYKeyPositions[curThresholdPos + 1] * mapSizeY);
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
                    throw new Exception("Illegal TerrainYKeyPositions in landforms.js, Landform " + Code + ", key positions must be at least 1 block apart. Translated key positions for this maps world height: " + pos);
                }

                TerrainYThresholds[y] = 1 - GameMath.Lerp(curThreshold, nextThreshold, distance); // We need inverted lerped value
            }


            /*string lerped = Code + ": [";

            for (int i = 0; i < TerrainYThresholds.Length; i++)
            {
                if (i > 0) lerped += ", ";
                lerped += TerrainYThresholds[i];
            }
            lerped += "]";

            Console.WriteLine(lerped);*/
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
