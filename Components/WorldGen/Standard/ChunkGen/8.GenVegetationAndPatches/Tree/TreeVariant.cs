using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace Vintagestory.ServerMods.NoObf
{
    [JsonObject(MemberSerialization.OptIn)]
    public class TreeVariant
    {
        [JsonProperty]
        public AssetLocation Generator;

        [JsonProperty]
        public float Weight;

        [JsonProperty]
        public float MinSize = 0.2f;

        [JsonProperty]
        public float MaxSize = 1f;

        [JsonProperty]
        public float SuitabilitySizeBonus = 0.5f;

        [JsonProperty]
        public float SaplingDropRate;

        [JsonProperty]
        public float GrowthSpeed;

        [JsonProperty]
        public float JankaHardness;

        [JsonProperty]
        public int MinTemp = -40;

        [JsonProperty]
        public int MaxTemp = 40;

        [JsonProperty]
        public int MinRain = 0;

        [JsonProperty]
        public int MaxRain = 255;

        [JsonProperty]
        public int MinFert = 0;

        [JsonProperty]
        public int MaxFert = 255;

        [JsonProperty]
        public int MinForest = 0;

        [JsonProperty]
        public int MaxForest = 255;

        [JsonProperty]
        public float MinHeight = 0;

        [JsonProperty]
        public float MaxHeight = 1;


        public int TempMid;
        public float TempRange;
        public int RainMid;
        public float RainRange;
        public int FertMid;
        public float FertRange;
        public int ForestMid;
        public float ForestRange;
        public float HeightMid;
        public float HeightRange;

        [OnDeserialized]
        public void AfterDeserialization(StreamingContext context)
        {
            TempMid = (MinTemp + MaxTemp) / 2;
            TempRange = (MaxTemp - MinTemp) / 2;

            if (TempRange == 0)
            {
                TempRange = 1;
            }

            RainMid = (MinRain + MaxRain) / 2;
            RainRange = (MaxRain - MinRain) / 2;

            if (RainRange == 0)
            {
                RainRange = 1;
            }

            FertMid = (MinFert + MaxFert) / 2;
            FertRange = (MaxFert - MinFert) / 2;

            if (FertRange == 0)
            {
                FertRange = 1;
            }

            ForestMid = (MinForest + MaxForest) / 2;
            ForestRange = (MaxForest - MinForest) / 2;

            if (ForestRange == 0)
            {
                ForestRange = 1;
            }

            HeightMid = (MinHeight + MaxHeight) / 2;
            HeightRange = (MaxHeight - MinHeight) / 2;

            if (HeightRange == 0)
            {
                HeightRange = 1;
            }

        }

    }
}
