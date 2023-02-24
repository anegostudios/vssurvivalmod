using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.ServerMods.NoObf;

namespace Vintagestory.ServerMods
{
    public enum EnumTreeType
    {
        Any,
        Decidious,
        Conifer,
        Tropical,
        Acacia
    }

    public class TreeGenInstance : TreeGenParams
    {
        public ITreeGenerator treeGen;
        public void GrowTree(IBlockAccessor blockAccessor, BlockPos pos)
        {
            treeGen.GrowTree(blockAccessor, pos, this);
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


        public TreeGenInstance GetRandomTreeGenForClimate(int climate, int forest, int y, bool isUnderwater)
        {
            return GetRandomGenForClimate(treeGenProps.TreeGens, climate, forest, y, isUnderwater);
        }

        public TreeGenInstance GetRandomShrubGenForClimate(int climate, int forest, int y)
        {
            return GetRandomGenForClimate(treeGenProps.ShrubGens, climate, forest, y, false);
        }

        public TreeGenInstance GetRandomGenForClimate(TreeVariant[] gens, int climate, int forest, int y, bool isUnderwater)
        {
            int rain = TerraGenConfig.GetRainFall((climate >> 8) & 0xff, y);
            int unscaledTemp = (climate >> 16) & 0xff;
            int temp = TerraGenConfig.GetScaledAdjustedTemperature(unscaledTemp, y - TerraGenConfig.seaLevel);
            float heightRel = ((float)y - TerraGenConfig.seaLevel) / ((float)api.WorldManager.MapSizeY - TerraGenConfig.seaLevel);
            int fertility = TerraGenConfig.GetFertility(rain, temp, heightRel);

            float total = 0;
            float fertDist, rainDist, tempDist, forestDist, heightDist;

            distances.Clear();

            for (int i = 0; i < gens.Length; i++)
            {
                TreeVariant variant = gens[i];
                if (isUnderwater && (int)variant.Habitat == 0) continue;
                if (!isUnderwater && variant.Habitat == EnumTreeHabitat.Water) continue;


                fertDist = Math.Abs(fertility - variant.FertMid) / variant.FertRange;
                rainDist = Math.Abs(rain - variant.RainMid) / variant.RainRange;
                tempDist = Math.Abs(temp - variant.TempMid) / variant.TempRange;
                forestDist = Math.Abs(forest - variant.ForestMid) / variant.ForestRange;
                heightDist = Math.Abs((y / worldheight) - variant.HeightMid) / variant.HeightRange;


                double distSq =
                    Math.Max(0, 1.2f * fertDist * fertDist - 1) +
                    Math.Max(0, 1.2f * rainDist * rainDist - 1) +
                    Math.Max(0, 1.2f * tempDist * tempDist - 1) +
                    Math.Max(0, 1.2f * forestDist * forestDist - 1) +
                    Math.Max(0, 1.2f * heightDist * heightDist - 1)
                ;

                if (random.NextDouble() < distSq) continue;

                float distance = GameMath.Clamp(1 - (fertDist + rainDist + tempDist + forestDist + heightDist) / 5f, 0, 1) * variant.Weight / 100f;

                distances.Add(variant, distance);

                total += distance;
            }

            distances = distances.Shuffle(random);

            double rnd = random.NextDouble() * total;

            foreach (var val in distances)
            {
                rnd -= val.Value;

                if (rnd <= 0.001)
                {
                    float suitabilityBonus = GameMath.Clamp(0.7f - val.Value, 0f, 0.7f) * 1 / 0.7f * val.Key.SuitabilitySizeBonus;

                    float size = val.Key.MinSize + (float)random.NextDouble() * (val.Key.MaxSize - val.Key.MinSize) + suitabilityBonus;
                    float descaledTemp = TerraGenConfig.DescaleTemperature(temp);

                    float rainVal = Math.Max(0, (rain / 255f - treeGenProps.vinesMinRain) / (1 - treeGenProps.vinesMinRain));
                    float tempVal = Math.Max(0, (descaledTemp / 255f - treeGenProps.descVineMinTempRel) / (1 - treeGenProps.descVineMinTempRel));

                    float rainValMoss = rain / 255f;
                    float tempValMoss = descaledTemp / 255f;

                    float vinesGrowthChance = 1.5f * rainVal * tempVal + 0.5f * rainVal * GameMath.Clamp((tempVal + 0.33f) / 1.33f, 0, 1);

                    // https://www.math3d.org/IyFcWuzED
                    // min(1,max(0, 2.25 * x - 128/255 + y^0.5 * 1.5 max(-0.5, 128/255-y)))
                    var mossGrowChance = 2.25 * rainValMoss - 0.5 + Math.Sqrt(tempValMoss) * 3 * Math.Max(-0.5, 0.5 - tempValMoss);
                    float mossGrowthChance = GameMath.Clamp((float)mossGrowChance, 0, 1);

                    //float mossGrowthChance = 1.5f * rainValMoss * tempValMoss + 1f * rainValMoss * GameMath.Clamp((tempValMoss + 0.33f) / 1.33f, 0, 1);

                    ITreeGenerator treegen = treeGenerators.GetGenerator(val.Key.Generator);

                    if (treegen == null)
                    {
                        api.World.Logger.Error("treengenproperties.json references tree generator {0}, but no such generator exists!", val.Key.Generator);
                        return null;
                    }


                    return new TreeGenInstance()
                    {
                        treeGen = treegen,
                        size = size,
                        vinesGrowthChance = vinesGrowthChance,
                        mossGrowthChance = mossGrowthChance
                    };
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


        /// <summary>
        /// Creates a shallow copy of the dictionary, i.e. the keys and values are not cloned
        /// </summary>
        /// <typeparam name="TKey"></typeparam>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="source"></param>
        /// <returns></returns>
        public static Dictionary<TKey, TValue> ShallowClone<TKey, TValue>(
           this Dictionary<TKey, TValue> source)
        {
            var cloned = new Dictionary<TKey, TValue>();
            foreach (var val in source)
            {
                cloned[val.Key] = val.Value;
            }

            return cloned;
        }
    }
}
