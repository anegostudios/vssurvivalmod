using Newtonsoft.Json;
using System.Collections.Generic;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Common;

#nullable disable

namespace Vintagestory.ServerMods.NoObf
{
    [JsonObject(MemberSerialization.OptIn)]
    public class BlockPatchConfig
    {
        [JsonProperty]
        public NatFloat ChanceMultiplier;
        [JsonProperty]
        public BlockPatch[] Patches;

        /// <summary>
        /// Patches that are not handled by TreeGen
        /// </summary>
        public BlockPatch[] PatchesNonTree;

        internal void ResolveBlockIds(ICoreServerAPI api, RockStrataConfig rockstrata, LCGRandom rnd)
        {
            List<BlockPatch> patchesNonTree = new List<BlockPatch>();

            for (int i = 0; i < Patches.Length; i++)
            {
                BlockPatch patch = Patches[i];

                bool handledbyTreegen = patch.Placement == EnumBlockPatchPlacement.OnTrees || patch.Placement == EnumBlockPatchPlacement.UnderTrees;
                if (!handledbyTreegen)
                {
                    patchesNonTree.Add(patch);
                }

                patch.Init(api, rockstrata, rnd, i);
            }

            PatchesNonTree = patchesNonTree.ToArray();
        }





        public bool IsPatchSuitableAt(BlockPatch patch, Block onBlock, int mapSizeY, int climate, int y, float forestRel, float shrubRel)
        {
            if ((patch.Placement == EnumBlockPatchPlacement.NearWater || patch.Placement == EnumBlockPatchPlacement.UnderWater) && onBlock.LiquidCode != "water") return false;
            if ((patch.Placement == EnumBlockPatchPlacement.NearSeaWater || patch.Placement == EnumBlockPatchPlacement.UnderSeaWater) && onBlock.LiquidCode != "saltwater") return false;

            if (forestRel < patch.MinForest || forestRel > patch.MaxForest || shrubRel < patch.MinShrub || forestRel > patch.MaxShrub)
            {
                // faster path without needing to fetch rainfall and temperature etc
                return false;
            }

            int rain = Climate.GetRainFall((climate >> 8) & 0xff, y);
            float rainRel = rain / 255f;
            if (rainRel < patch.MinRain || rainRel > patch.MaxRain)
            {
                // again faster path without needing to fetch temperature etc
                return false;
            }

            int temp = Climate.GetScaledAdjustedTemperature((climate >> 16) & 0xff, y - TerraGenConfig.seaLevel);
            if (temp < patch.MinTemp || temp > patch.MaxTemp)
            {
                // again faster path without needing to fetch sealevel and fertility
                return false;
            }

            float sealevelDistRel = ((float)y - TerraGenConfig.seaLevel) / ((float)mapSizeY - TerraGenConfig.seaLevel);
            if (sealevelDistRel < patch.MinY || sealevelDistRel > patch.MaxY)
            {
                return false;
            }

            // finally test fertility (the least common blockpatch criterion)
            float fertilityRel = Climate.GetFertility(rain, temp, sealevelDistRel) / 255f;
            return fertilityRel >= patch.MinFertility && fertilityRel <= patch.MaxFertility;
        }


        public bool IsPatchSuitableUnderTree(BlockPatch patch, int mapSizeY, ClimateCondition climate, int y)
        {
            float rainRel = climate.Rainfall;
            if (rainRel < patch.MinRain || rainRel > patch.MaxRain)
            {
                // again faster path without needing to fetch temperature etc
                return false;
            }

            float temp = climate.Temperature;
            if (temp < patch.MinTemp || temp > patch.MaxTemp)
            {
                // again faster path without needing to fetch sealevel and fertility
                return false;
            }

            float sealevelDistRel = ((float)y - TerraGenConfig.seaLevel) / ((float)mapSizeY - TerraGenConfig.seaLevel);
            if (sealevelDistRel < patch.MinY || sealevelDistRel > patch.MaxY)
            {
                return false;
            }

            // finally test fertility (the least common blockpatch criterion)
            float fertilityRel = climate.Fertility;
            return fertilityRel >= patch.MinFertility && fertilityRel <= patch.MaxFertility;
        }
    }
}
