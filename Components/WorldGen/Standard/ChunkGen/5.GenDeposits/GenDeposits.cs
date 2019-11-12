using System;
using System.Collections.Generic;
using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
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

        IWorldGenBlockAccessor blockAccessor;

        public LCGRandom depositRand;
        Random rand = new Random();

        List<Block> blockTypes;
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
                            childdepo.parentDeposit = depo;
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

            Dictionary<string, MapLayerBase> maplayersByCode = new Dictionary<string, MapLayerBase>();

            for (int i = 0; i < Deposits.Length; i++)
            {
                DepositVariant variant = Deposits[i];
                variant.Init(api, depositRand, depositShapeDistortNoise);

                if (variant.WithOreMap)
                {
                    variant.OreMapLayer = getOrCreateMapLayer(seed, variant.Code, maplayersByCode, variant.OreMapScale, variant.OreMapContrast, variant.OreMapSub);
                }

                if (variant.ChildDeposits != null)
                {
                    for (int k = 0; k < variant.ChildDeposits.Length; k++)
                    {
                        DepositVariant childVariant = variant.ChildDeposits[k];
                        if (childVariant.WithOreMap)
                        {
                            childVariant.OreMapLayer = getOrCreateMapLayer(seed, childVariant.Code, maplayersByCode, variant.OreMapScale, variant.OreMapContrast, variant.OreMapSub);
                        }
                    }
                }
            }
            
            blockTypes = api.World.Blocks;
            verticalDistortBottom = GenMaps.GetDepositVerticalDistort(seed + 12);
            verticalDistortTop = GenMaps.GetDepositVerticalDistort(seed + 28);
        }

        
        MapLayerBase getOrCreateMapLayer(int seed, string oremapCode, Dictionary<string, MapLayerBase> maplayersByCode, float scaleMul, float contrastMul, float sub)
        {
            MapLayerBase ml = null;
            if (!maplayersByCode.TryGetValue(oremapCode, out ml))
            {
                NoiseOre noiseOre = new NoiseOre(seed + oremapCode.GetHashCode());
                maplayersByCode[oremapCode] = ml = GenMaps.GetOreMap(seed + oremapCode.GetHashCode() + 1, noiseOre, scaleMul, contrastMul, sub);
            }

            return ml;
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


        protected override void GenChunkColumn(IServerChunk[] chunks, int chunkX, int chunkZ, ITreeAttribute chunkGenParams = null)
        {
            base.GenChunkColumn(chunks, chunkX, chunkZ);
        }

        public override void GeneratePartial(IServerChunk[] chunks, int chunkX, int chunkZ, int chunkdX, int chunkdZ)
        {
            int fromChunkx = chunkX + chunkdX;
            int fromChunkz = chunkZ + chunkdZ;

            int fromBaseX = fromChunkx * chunksize;
            int fromBaseZ = fromChunkz * chunksize;

            subDepositsToPlace.Clear();

            for (int i = 0; i < Deposits.Length; i++)
            {
                DepositVariant variant = Deposits[i];

                /*if (variant.Code != "sphalerite" || chunkdX != 0 || chunkdZ != 0)
                {
                    continue;
                }*/

                float quantityFactor = variant.WithOreMap ? variant.GetOreMapFactor(fromChunkx, fromChunkz) : 1;

                float qModified = variant.TriesPerChunk * quantityFactor * chanceMultiplier;
                int quantity = (int)qModified;
                quantity += chunkRand.NextInt(100) < 100 * (qModified - quantity) ? 1 : 0;
                
                while (quantity-- > 0)
                {
                    tmpPos.Set(fromBaseX + chunkRand.NextInt(chunksize), -99, fromBaseZ + chunkRand.NextInt(chunksize));
                    long crseed = chunkRand.NextInt(10000000);
                    depositRand.SetWorldSeed(crseed);
                    depositRand.InitPositionSeed(fromChunkx, fromChunkz);

                    GenDeposit(chunks, chunkX, chunkZ, tmpPos, variant);
                }
            }

            foreach (var val in subDepositsToPlace)
            {
                depositRand.SetWorldSeed(chunkRand.NextInt(10000000));
                depositRand.InitPositionSeed(fromChunkx, fromChunkz);

                val.Value.GeneratorInst.GenDeposit(blockAccessor, chunks, chunkX, chunkZ, val.Key, ref subDepositsToPlace);
            }
        }

        
        

        /// <summary>
        /// forceInitialPosY is for subdeposits
        /// </summary>
        /// <param name="chunks"></param>
        /// <param name="chunkX"></param>
        /// <param name="chunkZ"></param>
        /// <param name="offsetX"></param>
        /// <param name="offsetZ"></param>
        /// <param name="variant"></param>
        /// <param name="forceInitialPosY"></param>
        /// <returns></returns>
        void GenDeposit(IServerChunk[] chunks, int chunkX, int chunkZ, BlockPos depoCenterPos, DepositVariant variant)
        {
            int lx = GameMath.Mod(depoCenterPos.X, chunksize);
            int lz = GameMath.Mod(depoCenterPos.Z, chunksize);

            // Check if suited for this area, climate wise
            if (variant.Climate != null)
            {
                IMapChunk originMapchunk = api.WorldManager.GetMapChunk(depoCenterPos.X / chunksize, depoCenterPos.Z / chunksize);

                if (originMapchunk == null) return; // Definition: Climate dependent deposits are limited to size 32x32x32 

                depoCenterPos.Y = originMapchunk.RainHeightMap[lz * chunksize + lx];

                IntMap climateMap = blockAccessor.GetMapRegion(depoCenterPos.X / regionSize, depoCenterPos.Z / regionSize).ClimateMap;

                float posXInRegionClimate = ((float)lx / regionSize - (float)lx / regionSize) * noiseSizeClimate;
                float posZInRegionClimate = ((float)lz / regionSize - (float)lz / regionSize) * noiseSizeClimate;

                int climate = climateMap.GetUnpaddedColorLerped(posXInRegionClimate, posZInRegionClimate);
                float temp = TerraGenConfig.GetScaledAdjustedTemperatureFloat((climate >> 16) & 0xff, depoCenterPos.Y - TerraGenConfig.seaLevel);
                float rainRel = TerraGenConfig.GetRainFall((climate >> 8) & 0xff, depoCenterPos.Y) / 255f;

                if (rainRel < variant.Climate.MinRain || rainRel > variant.Climate.MaxRain || temp < variant.Climate.MinTemp || temp > variant.Climate.MaxTemp) return;
            }

            variant.GeneratorInst.GenDeposit(blockAccessor, chunks, chunkX, chunkZ, depoCenterPos, ref subDepositsToPlace);
        }

    }
}
