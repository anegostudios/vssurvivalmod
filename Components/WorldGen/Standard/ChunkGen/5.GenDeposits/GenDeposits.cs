using System;
using System.Collections.Generic;
using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.ServerMods.NoObf;

namespace Vintagestory.ServerMods
{
    public class GenDeposits : GenPartial
    {
        public DepositVariant[] Deposits;

        internal override int chunkRange { get { return 3; } }
        public override double ExecuteOrder() { return 0.2; }


        int regionChunkSize;
        int noiseSizeClimate;
        int noiseSizeOre;
        int regionSize;

        float chanceMultiplier;

        IBlockAccessor blockAccessor;

        public LCGRandom depositRand;
        Random rand = new Random();

        Block[] blockTypes;
        BlockPos tmpPos = new BlockPos();

        NormalizedSimplexNoise depositShapeDistortNoise;
        Dictionary<BlockPos, DepositVariant> subDepositsToPlace = new Dictionary<BlockPos, DepositVariant>();
        MapLayerBase verticalDistortTop;
        MapLayerBase verticalDistortBottom;

        public override bool ShouldLoad(EnumAppSide side)
        {
            return side == EnumAppSide.Server;
        }


        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);

            if (DoDecorationPass)
            {
                api.Event.ChunkColumnGeneration(GenChunkColumn, EnumWorldGenPass.TerrainFeatures, "standard");
                api.Event.GetWorldgenBlockAccessor(OnWorldGenBlockAccessor);
                api.Event.MapRegionGeneration(OnMapRegionGen, "standard");
            }
        }




        private void OnWorldGenBlockAccessor(IChunkProviderThread chunkProvider)
        {
            blockAccessor = chunkProvider.GetBlockAccessor(true);
        }

        public override void initWorldGen()
        {
            base.initWorldGen();

            chanceMultiplier = api.Assets.Get("worldgen/deposits.json").ToObject<Deposits>().ChanceMultiplier;

            Dictionary<AssetLocation, DepositVariant[]> depositFiles = api.Assets.GetMany<DepositVariant[]>(api.World.Logger, "worldgen/deposits/");
            List<DepositVariant> variants = new List<DepositVariant>();
            foreach (var val in depositFiles)
            {
                foreach (var depo in val.Value)
                {
                    depo.fromFile = val.Key.ToString();
                    variants.Add(depo);

                    if (depo.ChildDeposits != null)
                    {
                        foreach (var childdepo in depo.ChildDeposits)
                        {
                            childdepo.fromFile = val.Key.ToString();
                        }
                    }
                }
            }
            Deposits = variants.ToArray();

            
            depositShapeDistortNoise = NormalizedSimplexNoise.FromDefaultOctaves(3, 1 / 10f, 0.9f, 1);

            regionSize = api.WorldManager.RegionSize;
            regionChunkSize = api.WorldManager.RegionSize / chunksize;
            noiseSizeClimate = regionSize / TerraGenConfig.climateMapScale;
            noiseSizeOre = regionSize / TerraGenConfig.oreMapScale;
            


            int seed = api.WorldManager.Seed;
            depositRand = new LCGRandom(api.WorldManager.Seed + 34613);

            for (int i = 0; i < Deposits.Length; i++)
            {
                DepositVariant variant = Deposits[i];
                variant.Init(api, depositRand, depositShapeDistortNoise);

                if (variant.WithOreMap)
                {
                    NoiseOre noiseOre = new NoiseOre(seed++);
                    variant.OreMapLayer = GenMaps.GetOreMap(seed++, noiseOre);
                }

                if (variant.ChildDeposits != null)
                {
                    for (int k = 0; k < variant.ChildDeposits.Length; k++)
                    {
                        DepositVariant childVariant = variant.ChildDeposits[k];
                        if (childVariant.WithOreMap)
                        {
                            NoiseOre noiseOre = new NoiseOre(seed++);
                            childVariant.OreMapLayer = GenMaps.GetOreMap(seed++, noiseOre);
                        }
                    }
                }
            }

            
            blockTypes = api.World.Blocks;

            verticalDistortBottom = GenMaps.GetDepositVerticalDistort(seed++);
            verticalDistortTop = GenMaps.GetDepositVerticalDistort(seed++);
        }

        

        private void OnMapRegionGen(IMapRegion mapRegion, int regionX, int regionZ)
        {
            int pad = 2;
            TerraGenConfig.depositVerticalDistortScale = 2;
            int noiseSize = api.WorldManager.RegionSize / TerraGenConfig.depositVerticalDistortScale;

            IntMap map = mapRegion.OreMapVerticalDistortBottom;
            map.Size = noiseSize + 2*pad;
            map.BottomRightPadding = map.TopLeftPadding = pad;
            map.Data = verticalDistortBottom.GenLayer(regionX * noiseSize - pad, regionZ * noiseSize - pad, noiseSize + 2*pad, noiseSize + 2 * pad);

            map = mapRegion.OreMapVerticalDistortTop;
            map.Size = noiseSize + 2 * pad;
            map.BottomRightPadding = map.TopLeftPadding = pad;
            map.Data = verticalDistortTop.GenLayer(regionX * noiseSize - pad, regionZ * noiseSize - pad, noiseSize + 2 * pad, noiseSize + 2 * pad);


            for (int i = 0; i < Deposits.Length; i++)
            {
                DepositVariant variant = Deposits[i];
                variant.OnMapRegionGen(mapRegion, regionX, regionZ);
            }
        }


        public override void GeneratePartial(IServerChunk[] chunks, int originChunkX, int originChunkZ, int chunkdX, int chunkdZ)
        {
            int chunkx = originChunkX + chunkdX;
            int chunkz = originChunkZ + chunkdZ;

            int baseX = chunkx * chunksize;
            int baseZ = chunkz * chunksize;

            subDepositsToPlace.Clear();

            for (int i = 0; i < Deposits.Length; i++)
            {
                DepositVariant variant = Deposits[i];
                float quantityFactor = variant.WithOreMap ? variant.GetOreMapFactor(chunkx, chunkz) : 1;

                float qModified = variant.TriesPerChunk * quantityFactor * chanceMultiplier;
                int quantity = (int)qModified;
                quantity += chunkRand.NextInt(100) < 100 * (qModified - quantity) ? 1 : 0;
                
                while (quantity-- > 0)
                {
                    tmpPos.Set(baseX + chunkRand.NextInt(chunksize), -99, baseZ + chunkRand.NextInt(chunksize));

                    depositRand.SetWorldSeed(chunkRand.NextInt(10000000));
                    depositRand.InitPositionSeed(chunkx, chunkz);

                    GenDeposit(chunks, originChunkX, originChunkZ, tmpPos, variant);
                }
            }

            foreach (var val in subDepositsToPlace)
            {
                val.Value.GeneratorInst.GenDeposit(blockAccessor, chunks, originChunkX, originChunkZ, val.Key, ref subDepositsToPlace);
            }
        }

        
        

        /// <summary>
        /// forceInitialPosY is for subdeposits
        /// </summary>
        /// <param name="chunks"></param>
        /// <param name="originChunkX"></param>
        /// <param name="originChunkZ"></param>
        /// <param name="offsetX"></param>
        /// <param name="offsetZ"></param>
        /// <param name="variant"></param>
        /// <param name="forceInitialPosY"></param>
        /// <returns></returns>
        void GenDeposit(IServerChunk[] chunks, int originChunkX, int originChunkZ, BlockPos pos, DepositVariant variant)
        {
            int lx = GameMath.Mod(pos.X, chunksize);
            int lz = GameMath.Mod(pos.Z, chunksize);

            IMapChunk heremapchunk = chunks[0].MapChunk;

            // Check if suited for this area, climate wise
            if (variant.Climate != null)
            {
                IMapChunk originMapchunk = null;

                originMapchunk = api.WorldManager.GetMapChunk(pos.X / chunksize, pos.Z / chunksize); 
                if (originMapchunk == null) return; // Definition: Climate dependent deposits are limited to size 32x32x32 

                pos.Y = originMapchunk.RainHeightMap[lz * chunksize + lx];

                IntMap climateMap = api.World.BlockAccessor.GetMapRegion(pos.X / regionSize, pos.Z / regionSize).ClimateMap;

                float posXInRegionClimate = ((float)lx / regionSize - (float)lx / regionSize) * noiseSizeClimate;
                float posZInRegionClimate = ((float)lz / regionSize - (float)lz / regionSize) * noiseSizeClimate;

                int climate = climateMap.GetUnpaddedColorLerped(posXInRegionClimate, posZInRegionClimate);
                float temp = TerraGenConfig.GetScaledAdjustedTemperatureFloat((climate >> 16) & 0xff, pos.Y - TerraGenConfig.seaLevel);
                float rainRel = TerraGenConfig.GetRainFall((climate >> 8) & 0xff, pos.Y) / 255f;

                if (rainRel < variant.Climate.MinRain || rainRel > variant.Climate.MaxRain || temp < variant.Climate.MinTemp || temp > variant.Climate.MaxTemp) return;
            }

            variant.GeneratorInst.GenDeposit(blockAccessor, chunks, originChunkX, originChunkZ, pos, ref subDepositsToPlace);
        }

    }
}
