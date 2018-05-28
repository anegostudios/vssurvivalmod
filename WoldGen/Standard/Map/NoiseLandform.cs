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

        public NoiseLandforms(long seed, ICoreServerAPI api) : base(seed)
        {
            LoadLandforms(api);
        }

        public static void ReloadLandforms(ICoreServerAPI api)
        {
            api.Assets.Reload(new AssetLocation("worldgen/terrain/standard"));
            LoadLandforms(api);
        }

        public static void LoadLandforms(ICoreServerAPI api)
        { 
            IAsset asset = api.Assets.Get("worldgen/terrain/standard/landforms.json");
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

        public int GetLandformIndexAt(int unscaledXpos, int unscaledZpos, int climate)
        {
            float xpos = (float)unscaledXpos / TerraGenConfig.landformMapScale;
            float zpos = (float)unscaledZpos / TerraGenConfig.landformMapScale;

            int xposInt = (int)xpos;
            int zposInt = (int)zpos;

            int parentIndex = GetParentLandformIndexAt(xposInt, zposInt, climate, xpos - xposInt, zpos - zposInt);

            LandformVariant[] mutations = landforms.Variants[parentIndex].Mutations;
            if (mutations != null && mutations.Length > 0)
            {
                InitPositionSeed(unscaledXpos, unscaledZpos);
                float chance = NextInt(101) / 100f;
                for (int i = 0; i < mutations.Length; i++)
                {
                    LandformVariant variantMut = mutations[i];

                    if (variantMut.UseClimateMap)
                    {
                        float rainSuitability = GameMath.TriangleStep((climate >> 8) & 0xff, landforms.Variants[i].MinRain, landforms.Variants[i].MaxRain);
                        float tempSuitability = GameMath.TriangleStep((climate >> 16) & 0xff, landforms.Variants[i].MinTemp, landforms.Variants[i].MaxTemp);

                        if (rainSuitability == 0 || tempSuitability == 0) continue;
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


        public int GetParentLandformIndexAt(int xpos, int zpos, int climate, float lerpx, float lerpz)
        {
            InitPositionSeed(xpos, zpos);

            float weightSum = 0;
            int i = 0;
            for (i = 0; i < landforms.Variants.Length; i++)
            {
                float weight = landforms.Variants[i].Weight;

                if (landforms.Variants[i].UseClimateMap)
                {
                    weight *= GameMath.TriangleStep((climate >> 8) & 0xff, landforms.Variants[i].MinRain, landforms.Variants[i].MaxRain);
                    weight *= GameMath.TriangleStep((climate >> 16) & 0xff, landforms.Variants[i].MinTemp, landforms.Variants[i].MaxTemp);
                }

                landforms.Variants[i].WeightTmp = weight;
                weightSum += weight;
            }

            float rand = weightSum * NextInt(10000) / 10000f;

            for (i = 0; i < landforms.Variants.Length; i++)
            {
                rand -= landforms.Variants[i].WeightTmp;
                if (rand <= 0) return landforms.Variants[i].index;
            }

            return landforms.Variants[i].index;
        }


    }
}
