using Newtonsoft.Json;
using Vintagestory.API.Common;

namespace Vintagestory.ServerMods.NoObf
{
    public class BlockCropPropertiesType
    {
        [JsonProperty]
        public EnumSoilNutrient RequiredNutrient;

        [JsonProperty]
        public float NutrientConsumption;

        [JsonProperty]
        public int GrowthStages;

        [JsonProperty]
        public float TotalGrowthDays;

        [JsonProperty]
        public float GrowthRateDays;

        [JsonProperty]
        public bool MultipleHarvests;

        [JsonProperty]
        public int HarvestGrowthStageLoss;

        [JsonProperty]
        public CropBehaviorType[] Behaviors;
    }
}