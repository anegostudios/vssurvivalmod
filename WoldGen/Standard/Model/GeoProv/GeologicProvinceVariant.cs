using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace Vintagestory.ServerMods.NoObf
{
    [JsonObject(MemberSerialization.OptIn)]
    public class GeologicProvinceVariant
    {
        public int Index;
        public int ColorInt;

        [JsonProperty]
        public string Code;
        [JsonProperty]
        public string Hexcolor;
        [JsonProperty]
        public int Weight;
        [JsonProperty]
        public Dictionary<string, GeologicProvinceRockStrata> Rockstrata;
        [JsonProperty]
        public float AverageSoilThickness;

        RockStrataGen[] ValueGenerators;


        
        public void InitRockStrataGen(ICoreServerAPI api, Random rand, Dictionary<EnumRockGroup, List<RockStrataVariant>> VariantsByRockGroup)
        {
            List<RockStrataGen> Gens = new List<RockStrataGen>();

            var values = Enum.GetValues(typeof(EnumRockGroup));

       //     Console.WriteLine("init rock strata " + code);

            foreach(EnumRockGroup rockgroup in values)
            {
                GeologicProvinceRockStrata gprs = Rockstrata[rockgroup + ""];

                float current = gprs.minQuant + (float)rand.NextDouble() * (gprs.maxQuant - gprs.minQuant);
                gprs.currentq = (int)current + ((rand.NextDouble() * (current - (int)current)) > 0 ? 1 : 0);

              //  Console.WriteLine("add " + current + " " + rockgroup +"s");

                for (int i = 0; i < gprs.currentq; i++)
                {
                    int rnd = rand.Next(VariantsByRockGroup[rockgroup].Count);
                    RockStrataVariant rockType = VariantsByRockGroup[rockgroup][rnd];

                    float multiplier = rockType.Weight / 100f;
                    // Multiplier range from 0..1 to 100 - 5
                    //multiplier = (1 - multiplier) * 193 + 7;
                    multiplier = (1 - multiplier) * 95 + 5;

                    int blockId = api.WorldManager.GetBlockId(rockType.BlockCode);
                    if (blockId == -1)
                    {
                        throw new Exception("Block with code " + rockType.BlockCode + " not found! Can't generate rock strata this way :(");
                    }

                    RockStrataGen gen = new RockStrataGen()
                    {
                        amplitude = 1,
                        multiplier = multiplier,
                        offset = (float)rand.NextDouble(),
                        thickness = gprs.maxThickness,
                        blockId = (ushort)blockId,
                        heightErosion = 1 - rockType.HeightErosion
                    };

                    Gens.Add(gen);

                   // Console.WriteLine("addded rock type generator {0} (id={4}), m={1}, o={2}, t={3}", rockType.BlockCode, gen.blockId, gen.multiplier, gen.offset, gen.thickness);
                }
            }

            ValueGenerators = Gens.ToArray();

            
        }




        public void LoadRockStratas(double noiseValue, List<ushort> blockIds, int surfaceY, float weight)
        {
            int y = surfaceY;

            for (int i = 0; i < ValueGenerators.Length; i++)
            {
                RockStrataGen gen = ValueGenerators[i];
                double value = gen.GetValue(noiseValue);

                if (value * gen.thickness >= 1)
                {
                    double he = 0; // gen.heightErosion * Math.Max(0, (y - TerraGenConfig.seaLevel) / 16f);
                    
                    int thickness = (int)(value * gen.thickness * weight * (1 - he));

                    while (thickness-- > 0 && blockIds.Count < surfaceY + 1)
                    {
                        blockIds.Add(gen.blockId);
                        y--;
                    }

                }
            }
        }
    }
}
