using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.ServerMods.NoObf;

namespace Vintagestory.ServerMods
{
    class NoiseLandforms : NoiseBase
    {
        // (Be aware, static vars never get unloaded even when singleplayer server has been shut down)
        public static LandformsWorldProperty landforms;

        public float scale;

        public NoiseLandforms(long seed, ICoreServerAPI api, float scale) : base(seed)
        {
            LoadLandforms(api);
            this.scale = scale;
        }

        public static void LoadLandforms(ICoreServerAPI api)
        {
            IAsset asset = api.Assets.Get("worldgen/landforms.json");
            landforms = asset.ToObject<LandformsWorldProperty>();

            int quantityMutations = 0;

            for (int i = 0; i < landforms.Variants.Length; i++)
            {
                LandformVariant variant = landforms.Variants[i];
                variant.index = i;
                variant.Init(api.WorldManager, i);

                if (variant.Mutations != null)
                {
                    quantityMutations += variant.Mutations.Length;
                }
            }

            landforms.LandFormsByIndex = new LandformVariant[quantityMutations + landforms.Variants.Length];

            // Mutations get indices after the parent ones
            for (int i = 0; i < landforms.Variants.Length; i++)
            {
                landforms.LandFormsByIndex[i] = landforms.Variants[i];
            }

            int nextIndex = landforms.Variants.Length;
            for (int i = 0; i < landforms.Variants.Length; i++)
            {
                LandformVariant variant = landforms.Variants[i];
                if (variant.Mutations != null)
                {
                    for (int j = 0; j < variant.Mutations.Length; j++)
                    {
                        LandformVariant variantMut = variant.Mutations[j];

                        if (variantMut.TerrainOctaves == null)
                        {
                            variantMut.TerrainOctaves = variant.TerrainOctaves;
                        }
                        if (variantMut.TerrainOctaveThresholds == null)
                        {
                            variantMut.TerrainOctaveThresholds = variant.TerrainOctaveThresholds;
                        }
                        if (variantMut.TerrainYKeyPositions == null)
                        {
                            variantMut.TerrainYKeyPositions = variant.TerrainYKeyPositions;
                        }
                        if (variantMut.TerrainYKeyThresholds == null)
                        {
                            variantMut.TerrainYKeyThresholds = variant.TerrainYKeyThresholds;
                        }


                        landforms.LandFormsByIndex[nextIndex] = variantMut;
                        variantMut.Init(api.WorldManager, nextIndex);
                        nextIndex++;
                    }
                }
            }
        }

        public int GetLandformIndexAt(int unscaledXpos, int unscaledZpos, int temp, int rain)
        {
            float xpos = (float)unscaledXpos / scale;
            float zpos = (float)unscaledZpos / scale;

            int xposInt = (int)xpos;
            int zposInt = (int)zpos;

            int parentIndex = GetParentLandformIndexAt(xposInt, zposInt, temp, rain);

            LandformVariant[] mutations = landforms.Variants[parentIndex].Mutations;
            if (mutations != null && mutations.Length > 0)
            {
                InitPositionSeed(unscaledXpos/2, unscaledZpos/2);
                float chance = NextInt(101) / 100f;

                for (int i = 0; i < mutations.Length; i++)
                {
                    LandformVariant variantMut = mutations[i];

                    if (variantMut.UseClimateMap)
                    {
                        int distRain = rain - GameMath.Clamp(rain, variantMut.MinRain, variantMut.MaxRain);
                        double distTemp = temp - GameMath.Clamp(temp, variantMut.MinTemp, variantMut.MaxTemp);
                        if (distRain != 0 || distTemp != 0) continue;
                    }


                    chance -= mutations[i].Chance;
                    if (chance <= 0)
                    {
                        return mutations[i].index;
                    }
                }
            }

            return parentIndex;
        }


        public int GetParentLandformIndexAt(int xpos, int zpos, int temp, int rain)
        {
            InitPositionSeed(xpos, zpos);

            double weightSum = 0;
            int i;
            for (i = 0; i < landforms.Variants.Length; i++)
            {
                double weight = landforms.Variants[i].Weight;

                if (landforms.Variants[i].UseClimateMap)
                {
                    int distRain = rain - GameMath.Clamp(rain, landforms.Variants[i].MinRain, landforms.Variants[i].MaxRain);
                    double distTemp = temp - GameMath.Clamp(temp, landforms.Variants[i].MinTemp, landforms.Variants[i].MaxTemp);
                    if (distRain != 0 || distTemp != 0) weight = 0;
                }

                landforms.Variants[i].WeightTmp = weight;
                weightSum += weight;
            }

            double rand = weightSum * NextInt(10000) / 10000.0;

            for (i = 0; i < landforms.Variants.Length; i++)
            {
                rand -= landforms.Variants[i].WeightTmp;
                if (rand <= 0) return landforms.Variants[i].index;
            }

            return landforms.Variants[i].index;
        }


    }
}
