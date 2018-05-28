using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Vintagestory.ServerMods.NoObf
{
    [JsonObject(MemberSerialization.OptIn)]
    public class BlockLayerCodeByMin
    {
        [JsonProperty]
        public AssetLocation BlockCode;
        [JsonProperty]
        public float MinTemp = -30;
        [JsonProperty]
        public float MinRain = 0;
        [JsonProperty]
        public float MinFertility = 0;
        [JsonProperty]
        public float MaxFertility = 1;
        [JsonProperty]
        public float MaxY = 1;

        public ushort BlockId;

        public Dictionary<ushort, ushort> BlockIdMapping;
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class BlockLayer
    {
        [JsonProperty]
        public string Name;
        [JsonProperty]
        public string ID;
        [JsonProperty]
        public AssetLocation BlockCode;
        [JsonProperty]
        public BlockLayerCodeByMin[] BlockCodeByMin;
        [JsonProperty]
        public int MinTemp = -30;
        [JsonProperty]
        public int MaxTemp = 40;
        [JsonProperty]
        public float MinRain = 0;
        [JsonProperty]
        public float MaxRain = 1;
        [JsonProperty]
        public float MinFertility = 0;
        [JsonProperty]
        public float MaxFertility = 1;
        [JsonProperty]
        public float MinY = 0;
        [JsonProperty]
        public float MaxY = 1;

        [JsonProperty]
        public double[] NoiseAmplitudes;
        [JsonProperty]
        public double[] NoiseFrequencies;

        public ushort BlockId;
       

        ClampedPerlinNoise noise;

        public Dictionary<ushort, ushort> BlockIdMapping;
        
        public void Init(ICoreServerAPI api, RockstrataWorldProperty rockstrata, Random rnd)
        {
            if (NoiseAmplitudes != null && NoiseFrequencies != null)
            {
                noise = new ClampedPerlinNoise(NoiseAmplitudes, NoiseFrequencies, rnd.Next());
            }

            ResolveBlockIds(api, rockstrata);
        }

        void ResolveBlockIds(ICoreServerAPI api, RockstrataWorldProperty rockstrata)
        {
            if (BlockCode != null && BlockCode.Path.Length > 0)
            {
                if (BlockCode.Path.Contains("{rocktype}"))
                {
                    BlockIdMapping = new Dictionary<ushort, ushort>();
                    for (int i = 0; i < rockstrata.Variants.Length; i++)
                    {
                        string rocktype = rockstrata.Variants[i].BlockCode.Path.Split('-')[1];
                        BlockIdMapping.Add(api.WorldManager.GetBlockId(rockstrata.Variants[i].BlockCode), api.WorldManager.GetBlockId(BlockCode.CopyWithPath(BlockCode.Path.Replace("{rocktype}", rocktype))));
                    }
                } else
                {
                    BlockId = api.WorldManager.GetBlockId(BlockCode);
                }
            } else BlockCode = null;
           
            if (BlockCodeByMin != null)
            {
                for (int i = 0; i < BlockCodeByMin.Length; i++)
                {
                    AssetLocation blockCode = BlockCodeByMin[i].BlockCode;

                    if (blockCode.Path.Contains("{rocktype}"))
                    {
                        BlockCodeByMin[i].BlockIdMapping = new Dictionary<ushort, ushort>();
                        for (int j = 0; j < rockstrata.Variants.Length; j++)
                        {
                            string rocktype = rockstrata.Variants[j].BlockCode.Path.Split('-')[1];
                            BlockCodeByMin[i].BlockIdMapping.Add(api.WorldManager.GetBlockId(rockstrata.Variants[j].BlockCode), api.WorldManager.GetBlockId(blockCode.CopyWithPath(blockCode.Path.Replace("{rocktype}", rocktype))));
                        }
                    }
                    else
                    {
                        BlockCodeByMin[i].BlockId = api.WorldManager.GetBlockId(blockCode);
                    }

                    
                }
            }
        }

        public ushort GetBlockId(float temp, float rainRel, float fertilityRel, ushort firstBlockId)
        {
            if (BlockCode != null)
            {
                ushort mapppedBlockId = BlockId;
                if (BlockIdMapping != null)
                {
                    BlockIdMapping.TryGetValue(firstBlockId, out mapppedBlockId);
                }

                return mapppedBlockId;
            }


            for (int i = 0; i < BlockCodeByMin.Length; i++)
            {
                BlockLayerCodeByMin blcv = BlockCodeByMin[i];

                if (blcv.MinTemp <= temp && blcv.MinRain <= rainRel && blcv.MinFertility <= fertilityRel && blcv.MaxFertility >= fertilityRel)
                {
                    ushort mapppedBlockId = blcv.BlockId;
                    if (blcv.BlockIdMapping != null)
                    {
                        blcv.BlockIdMapping.TryGetValue(firstBlockId, out mapppedBlockId);
                    }

                    return mapppedBlockId;
                }
            }

            return 0;
        }
    }
}
