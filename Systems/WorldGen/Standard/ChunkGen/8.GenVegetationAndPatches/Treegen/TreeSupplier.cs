using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.ServerMods.NoObf;

namespace Vintagestory.ServerMods
{
    public class TreeGenForClimate
    {
        public ITreeGenerator treeGen;
        public float size;
        public float vinesGrowthChance;

        public TreeGenForClimate(ITreeGenerator treeGen, float size, float vinesGrowthChance)
        {
            this.treeGen = treeGen;
            this.size = size;
            this.vinesGrowthChance = vinesGrowthChance;
        }
    }

    public class WgenTreeSupplier
    {
        private ICoreServerAPI api;

        internal TreeGenProperties treeGenProps;
        internal TreeGeneratorsUtil treeGenerators;

        Random random;

        float worldheight;

        Dictionary<TreeVariant, float> distances = new Dictionary<TreeVariant, float>();



        public WgenTreeSupplier(ICoreServerAPI api)
        {
            treeGenerators = new TreeGeneratorsUtil(api);

            this.api = api;
        }

        internal void LoadTrees()
        {
            treeGenProps = api.Assets.Get("worldgen/treengenproperties.json").ToObject<TreeGenProperties>();
            treeGenProps.descVineMinTempRel = TerraGenConfig.DescaleTemperature(treeGenProps.vinesMinTemp) / 255f;

            treeGenerators.LoadTreeGenerators();
            
            random = new Random(api.WorldManager.Seed);
            
            worldheight = api.WorldManager.MapSizeY;
        }


        public TreeGenForClimate GetRandomTreeGenForClimate(int climate, int forest, int y)
        {
            return GetRandomGenForClimate(treeGenProps.TreeGens, climate, forest, y);
        }

        public TreeGenForClimate GetRandomShrubGenForClimate(int climate, int forest, int y)
        {
            return GetRandomGenForClimate(treeGenProps.ShrubGens, climate, forest, y);
        }

        public TreeGenForClimate GetRandomGenForClimate(TreeVariant[] gens, int climate, int forest, int y)
        {
            int rain = TerraGenConfig.GetRainFall((climate >> 8) & 0xff, y);
            int temp = TerraGenConfig.GetScaledAdjustedTemperature((climate >> 16) & 0xff, y - TerraGenConfig.seaLevel);
            float heightRel = ((float)y - TerraGenConfig.seaLevel) / ((float)api.WorldManager.MapSizeY - TerraGenConfig.seaLevel);
            int fertility = TerraGenConfig.GetFertility(rain, temp, heightRel);

            float total = 0;
            float fertDist, rainDist, tempDist, forestDist, heightDist;

            distances.Clear();

            for (int i = 0; i < gens.Length; i++)
            {
                TreeVariant variant = gens[i];

                fertDist = Math.Abs(fertility - variant.FertMid) / variant.FertRange;
                rainDist = Math.Abs(rain - variant.RainMid) / variant.RainRange;
                tempDist = Math.Abs(temp - variant.TempMid) / variant.TempRange;
                forestDist = Math.Abs(forest - variant.ForestMid) / variant.ForestRange;
                heightDist = Math.Abs((y / worldheight) - variant.HeightMid) / variant.HeightRange;


                double distSq =
                    Math.Max(0, fertDist * fertDist - 1) +
                    Math.Max(0, rainDist * rainDist - 1) +
                    Math.Max(0, tempDist * tempDist - 1) +
                    Math.Max(0, forestDist * forestDist - 1) +
                    Math.Max(0, heightDist * heightDist - 1)
                ;

                if (random.NextDouble() < distSq) continue;

                float distance = (fertDist + rainDist + tempDist + forestDist + heightDist) * variant.Weight / 100f;

                distances.Add(variant, distance);

                total += distance;
            }

            distances = distances.Shuffle(random);

            double rnd = random.NextDouble();

            foreach (var val in distances)
            {
                rnd -= val.Value / total;
                if (rnd <= 0.001)
                {
                    float suitabilityBonus = GameMath.Clamp(0.7f - val.Value, 0f, 0.7f) * 1/0.7f * val.Key.SuitabilitySizeBonus;

                    float size = val.Key.MinSize + (float)random.NextDouble() * (val.Key.MaxSize - val.Key.MinSize) + suitabilityBonus;

                    float rainVal = Math.Max(0, (rain / 255f - treeGenProps.vinesMinRain) / (1 - treeGenProps.vinesMinRain));
                    float tempVal = Math.Max(0, (TerraGenConfig.DescaleTemperature(temp) / 255f - treeGenProps.descVineMinTempRel) / (1 - treeGenProps.descVineMinTempRel));

                    float vinesGrowthChance = 1.5f * rainVal * tempVal + 0.5f * rainVal * GameMath.Clamp((tempVal + 0.33f) / 1.33f, 0, 1);

                    ITreeGenerator treegen = treeGenerators.GetGenerator(val.Key.Generator);

                    if (treegen == null)
                    {
                        api.World.Logger.Error("treengenproperties.json references tree generator {0}, but no such generator exists!", val.Key.Generator);
                        return null;
                    }


                    return new TreeGenForClimate(treegen, size, vinesGrowthChance);
                }
            }

            return null;
        }
    }


    public static class DictionaryExtensions
    {
        public static Dictionary<TKey, TValue> Shuffle<TKey, TValue>(
           this Dictionary<TKey, TValue> source, Random rand)
        {
            return source.OrderBy(x => rand.Next())
               .ToDictionary(item => item.Key, item => item.Value);
        }
    }
}
