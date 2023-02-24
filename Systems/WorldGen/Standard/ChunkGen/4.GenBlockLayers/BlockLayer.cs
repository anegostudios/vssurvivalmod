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

namespace Vintagestory.ServerMods
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
        //[JsonProperty]
        //public float MinY = 1;
        //[JsonProperty]
        //public float MaxY = 1;

        public int BlockId;

        public Dictionary<int, int> BlockIdMapping;
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
        public int Thickness = 1;

        [JsonProperty]
        public double[] NoiseAmplitudes;
        [JsonProperty]
        public double[] NoiseFrequencies;

        public int BlockId;
       
        public Dictionary<int, int> BlockIdMapping;

        public void Init(ICoreServerAPI api, RockStrataConfig rockstrata, Random rnd)
        {
            ResolveBlockIds(api, rockstrata);
        }

        void ResolveBlockIds(ICoreServerAPI api, RockStrataConfig rockstrata)
        {
            if (BlockCode != null && BlockCode.Path.Length > 0)
            {
                if (BlockCode.Path.Contains("{rocktype}"))
                {
                    BlockIdMapping = new Dictionary<int, int>();
                    for (int i = 0; i < rockstrata.Variants.Length; i++)
                    {
                        if (rockstrata.Variants[i].IsDeposit) continue;

                        string rocktype = rockstrata.Variants[i].BlockCode.Path.Split('-')[1];

                        Block rockBlock = api.World.GetBlock(rockstrata.Variants[i].BlockCode);
                        Block rocktypedBlock  = api.World.GetBlock(BlockCode.CopyWithPath(BlockCode.Path.Replace("{rocktype}", rocktype)));
                        if (rockBlock != null && rocktypedBlock != null)
                        {
                            BlockIdMapping[rockBlock.BlockId] = rocktypedBlock.BlockId;
                        }
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
                        BlockCodeByMin[i].BlockIdMapping = new Dictionary<int, int>();
                        for (int j = 0; j < rockstrata.Variants.Length; j++)
                        {
                            string rocktype = rockstrata.Variants[j].BlockCode.Path.Split('-')[1];

                            Block rockBlock = api.World.GetBlock(rockstrata.Variants[j].BlockCode);
                            Block rocktypedBlock = api.World.GetBlock(blockCode.CopyWithPath(blockCode.Path.Replace("{rocktype}", rocktype)));

                            if (rockBlock != null && rocktypedBlock != null)
                            {
                                BlockCodeByMin[i].BlockIdMapping[rockBlock.BlockId] = rocktypedBlock.BlockId;
                            }
                        }
                    }
                    else
                    {
                        BlockCodeByMin[i].BlockId = api.WorldManager.GetBlockId(blockCode);
                    }

                    
                }
            }
        }

        public int GetBlockId(double posRand, float temp, float rainRel, float fertilityRel, int firstBlockId)
        {
            if (BlockCode != null)
            {
                int mapppedBlockId = BlockId;
                if (BlockIdMapping != null)
                {
                    BlockIdMapping.TryGetValue(firstBlockId, out mapppedBlockId);
                }

                return mapppedBlockId;
            }


            for (int i = 0; i < BlockCodeByMin.Length; i++)
            {
                BlockLayerCodeByMin blcv = BlockCodeByMin[i];

                float tempDist = Math.Abs(temp - GameMath.Max(temp, blcv.MinTemp));
                float rainDist = Math.Abs(rainRel - GameMath.Max(rainRel, blcv.MinRain));
                float fertDist = Math.Abs(fertilityRel - GameMath.Clamp(fertilityRel, blcv.MinFertility, blcv.MaxFertility));

                if (tempDist + rainDist + fertDist <= posRand)
                {
                    int mapppedBlockId = blcv.BlockId;
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
