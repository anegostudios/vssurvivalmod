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
    public class GenBlockLayers : ModStdWorldGen
    {
        private ICoreServerAPI api;
        
        List<int> BlockLayersIds = new List<int>();
        int[] layersUnderWater = new int[0];
        LCGRandom rnd;
        int mapheight;
        ClampedSimplexNoise grassDensity;
        ClampedSimplexNoise grassHeight;
        RockStrataVariant dummyRock;
        public BlockLayerConfig blockLayerConfig;
        SimplexNoise distort2dx;
        SimplexNoise distort2dz;
        int boilingWaterBlockId;


        public override bool ShouldLoad(EnumAppSide side)
        {
            return side == EnumAppSide.Server;
        }

        public override double ExecuteOrder()
        {
            return 0.4;
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            this.api = api;

            if (TerraGenConfig.DoDecorationPass)
            {
                this.api.Event.InitWorldGenerator(InitWorldGen, "standard");
                this.api.Event.InitWorldGenerator(InitWorldGen, "superflat"); // Just the Init so that BlockSoil can grow grass
                this.api.Event.MapRegionGeneration(OnMapRegionGen, "standard");
                this.api.Event.ChunkColumnGeneration(this.OnChunkColumnGeneration, EnumWorldGenPass.Terrain, "standard");
            }

            dummyRock = new RockStrataVariant() { SoilpH = 6.5f, WeatheringFactor = 1f };

            distort2dx = new SimplexNoise(new double[] { 14, 9, 6, 3 }, new double[] { 1 / 100.0, 1 / 50.0, 1 / 25.0, 1 / 12.5 }, api.World.SeaLevel + 20980);
            distort2dz = new SimplexNoise(new double[] { 14, 9, 6, 3 }, new double[] { 1 / 100.0, 1 / 50.0, 1 / 25.0, 1 / 12.5 }, api.World.SeaLevel + 20981);
        }

        private void OnMapRegionGen(IMapRegion mapRegion, int regionX, int regionZ, ITreeAttribute chunkGenParams = null)
        {
            /*int noiseSize = api.WorldManager.RegionSize / TerraGenConfig.rockStrataScale;
            int pad = 2;

            mapRegion.RockStrata = new IntMap[strata.Variants.Length];
            for (int i = 0; i < strata.Variants.Length; i++)
            {
                IntMap intmap = new IntMap();
                mapRegion.RockStrata[i] = intmap;
                intmap.Data = strataNoises[i].GenLayerMax0(
                    regionX * noiseSize - pad,
                    regionZ * noiseSize - pad,
                    noiseSize + 2 * pad,
                    noiseSize + 2 * pad
                );

                intmap.Size = noiseSize + 2 * pad;
                intmap.TopLeftPadding = intmap.BottomRightPadding = pad;
            }*/
        }





        public void InitWorldGen()
        {
            LoadGlobalConfig(api);

            IAsset asset = api.Assets.Get("worldgen/rockstrata.json");
            RockStrataConfig rockstrata = asset.ToObject<RockStrataConfig>();

            asset = api.Assets.Get("worldgen/blocklayers.json");
            blockLayerConfig = asset.ToObject<BlockLayerConfig>();
            blockLayerConfig.ResolveBlockIds(api, rockstrata);

            rnd = new LCGRandom(api.WorldManager.Seed);
            grassDensity = new ClampedSimplexNoise(new double[] { 4 }, new double[] { 0.5 }, rnd.NextInt());
            grassHeight = new ClampedSimplexNoise(new double[] { 1.5 }, new double[] { 0.5 }, rnd.NextInt());

            mapheight = api.WorldManager.MapSizeY;

            boilingWaterBlockId = api.World.GetBlock(new AssetLocation("boilingwater-still-7")).Id;
        }

        

        private void OnChunkColumnGeneration(IServerChunk[] chunks, int chunkX, int chunkZ, ITreeAttribute chunkGenParams = null)
        {
            rnd.InitPositionSeed(chunkX, chunkZ);

            IntDataMap2D forestMap = chunks[0].MapChunk.MapRegion.ForestMap;
            IntDataMap2D climateMap = chunks[0].MapChunk.MapRegion.ClimateMap;
            IntDataMap2D beachMap = chunks[0].MapChunk.MapRegion.BeachMap;

            ushort[] heightMap = chunks[0].MapChunk.RainHeightMap;

            int regionChunkSize = api.WorldManager.RegionSize / chunksize;
            int rdx = chunkX % regionChunkSize;
            int rdz = chunkZ % regionChunkSize;

            // Amount of data points per chunk
            float climateStep = (float)climateMap.InnerSize / regionChunkSize;
            float forestStep = (float)forestMap.InnerSize / regionChunkSize;
            float beachStep = (float)beachMap.InnerSize / regionChunkSize;

            // Retrieves the map data on the chunk edges
            int forestUpLeft = forestMap.GetUnpaddedInt((int)(rdx * forestStep), (int)(rdz * forestStep));
            int forestUpRight = forestMap.GetUnpaddedInt((int)(rdx * forestStep + forestStep), (int)(rdz * forestStep));
            int forestBotLeft = forestMap.GetUnpaddedInt((int)(rdx * forestStep), (int)(rdz * forestStep + forestStep));
            int forestBotRight = forestMap.GetUnpaddedInt((int)(rdx * forestStep + forestStep), (int)(rdz * forestStep + forestStep));

            int beachUpLeft = beachMap.GetUnpaddedInt((int)(rdx * beachStep), (int)(rdz * beachStep));
            int beachUpRight = beachMap.GetUnpaddedInt((int)(rdx * beachStep + beachStep), (int)(rdz * beachStep));
            int beachBotLeft = beachMap.GetUnpaddedInt((int)(rdx * beachStep), (int)(rdz * beachStep + beachStep));
            int beachBotRight = beachMap.GetUnpaddedInt((int)(rdx * beachStep + beachStep), (int)(rdz * beachStep + beachStep));


            // increasing x -> left to right
            // increasing z -> top to bottom
            float transitionSize = blockLayerConfig.blockLayerTransitionSize;
            BlockPos herePos = new BlockPos();


            for (int x = 0; x < chunksize; x++)
            {
                for (int z = 0; z < chunksize; z++)
                {
                    herePos.Set(chunkX * chunksize + x, 1, chunkZ * chunksize + z);
                    // Some weird randomnes stuff to hide fundamental bugs in the climate transition system :D T_T   (maybe not bugs but just fundamental shortcomings of using lerp on a very low resolution map)
                    float distx = (float)distort2dx.Noise(herePos.X, herePos.Z);
                    float distz = (float)distort2dz.Noise(herePos.X, herePos.Z);

                    int disty = (int)(distort2dx.Noise(-herePos.X, -herePos.Z) / 4.0);

                    double posRand = (double)GameMath.MurmurHash3(herePos.X, 1, herePos.Z)/int.MaxValue;
                    double transitionRand = (posRand + 1) * transitionSize;

                    int posY = heightMap[z * chunksize + x];
                    if (posY >= mapheight) continue;

                    int climate = climateMap.GetUnpaddedColorLerped(
                        rdx * climateStep + climateStep * (float)(x + distx) / chunksize, 
                        rdz * climateStep + climateStep * (float)(z + distz) / chunksize
                    );
                    
                    int tempUnscaled = (climate >> 16) & 0xff;
                    int rnd = (int)(distx / 5);
                    float temp = TerraGenConfig.GetScaledAdjustedTemperatureFloat(tempUnscaled, posY - TerraGenConfig.seaLevel + rnd);
                    float tempRel = TerraGenConfig.GetAdjustedTemperature(tempUnscaled, posY - TerraGenConfig.seaLevel + rnd) / 255f;
                    float rainRel = TerraGenConfig.GetRainFall((climate >> 8) & 0xff, posY + rnd) / 255f;
                    float forestRel = GameMath.BiLerp(forestUpLeft, forestUpRight, forestBotLeft, forestBotRight, (float)x / chunksize, (float)z / chunksize) / 255f;

                    int prevY = posY;

                    int rocky = chunks[0].MapChunk.WorldGenTerrainHeightMap[z * chunksize + x];
                    int chunkY = rocky / chunksize;
                    int lY = rocky % chunksize;
                    int index3d = (chunksize * lY + z) * chunksize + x;

                    int rockblockID = chunks[chunkY].Data.GetBlockIdUnsafe(index3d);
                    var hereblock = api.World.Blocks[rockblockID];
                    if (hereblock.BlockMaterial == EnumBlockMaterial.Soil || hereblock.BlockMaterial == EnumBlockMaterial.Gravel)
                    {
                        continue;
                    }

                    if (rocky < TerraGenConfig.seaLevel)
                    {
                        // If rain below 50%, raise by up to 12 blocks
                        float raise = Math.Max(0, (0.5f - rainRel) * 24);

                        int sealevelrise = (int)Math.Min(raise, TerraGenConfig.seaLevel - rocky);
                        int curSealevel = chunks[0].MapChunk.WorldGenTerrainHeightMap[z * chunksize + x];
                        chunks[0].MapChunk.WorldGenTerrainHeightMap[z * chunksize + x] = (ushort)Math.Max(rocky + sealevelrise - 1, curSealevel);

                        while (sealevelrise-- > 0)
                        {
                            chunkY = rocky / chunksize;
                            lY = rocky % chunksize;
                            index3d = (chunksize * lY + z) * chunksize + x;

                            IChunkBlocks chunkdata = chunks[chunkY].Data;
                            chunkdata.SetBlockUnsafe(index3d, rockblockID);
                            chunkdata.SetFluid(index3d, 0);
                            rocky++;
                        }
                    }

                    herePos.Y = posY;
                    posY = PutLayers(transitionRand, x, z, disty, herePos, chunks, rainRel, temp, tempUnscaled, heightMap);

                    if (prevY == TerraGenConfig.seaLevel - 1)
                    {
                        float beachRel = GameMath.BiLerp(beachUpLeft, beachUpRight, beachBotLeft, beachBotRight, (float)x / chunksize, (float)z / chunksize) / 255f;
                        GenBeach(x, prevY, z, chunks, rainRel, temp, beachRel, rockblockID);
                    }

                    PlaceTallGrass(x, prevY, z, chunks, rainRel, tempRel, temp, forestRel);

                    
                    // Try again to put layers if above sealevel and we found over 10 air blocks
                    int foundAir = 0;
                    while (posY >= TerraGenConfig.seaLevel - 1)
                    {
                        chunkY = posY / chunksize;
                        lY = posY % chunksize;
                        index3d = (chunksize * lY + z) * chunksize + x;
                        int blockId = chunks[chunkY].Data.GetBlockIdUnsafe(index3d);

                        if (blockId == 0)
                        {
                            foundAir++;
                        } else
                        {
                            if (foundAir >= 8)
                            {
                                //temp = TerraGenConfig.GetScaledAdjustedTemperatureFloat(tempUnscaled, posY - TerraGenConfig.seaLevel);
                                //rainRel = TerraGenConfig.GetRainFall((climate >> 8) & 0xff, posY) / 255f;
                                //PutLayers(transitionRand, x, posY, z, chunks, rainRel, temp, tempUnscaled, null);
                                break;
                            } else
                            {
                                foundAir = 0;
                            }
                            
                        }

                        posY--;
                    }


                    
                }
            }
        }


        private int PutLayers(double posRand, int lx, int lz, int posyoffs, BlockPos pos, IServerChunk[] chunks, float rainRel, float temp, int unscaledTemp, ushort[] heightMap)
        {
            int i = 0;
            int j = 0;
            
            bool underWater = false;
            bool first = true;
            int startPosY = pos.Y;
            

            while (pos.Y > 0)
            {
                int chunkY = pos.Y / chunksize;
                int lY = pos.Y % chunksize;
                int index3d = (chunksize * lY + lz) * chunksize + lx;
                int blockId = chunks[chunkY].Data.GetBlockIdUnsafe(index3d);
                if (blockId == 0) blockId = chunks[chunkY].Data.GetFluid(index3d);

                pos.Y--;

                if (blockId == GlobalConfig.waterBlockId || blockId == boilingWaterBlockId)
                {
                    underWater = true;   
                    continue;
                }

                // Don't generate on ice (would otherwise cause snow above water, which collapses with block gravity enabled, causing massive lag)
                if (blockId == GlobalConfig.lakeIceBlockId)
                {
                    break;
                }

                if (blockId != 0)
                {
                    if (heightMap != null && first)
                    {
                        chunks[0].MapChunk.TopRockIdMap[lz * chunksize + lx] = blockId;

                        LoadBlockLayers(posRand, rainRel, temp, unscaledTemp, startPosY + posyoffs, pos, blockId);
                        first = false;

                        if (!underWater) heightMap[lz * chunksize + lx] = (ushort)(pos.Y + 1);
                    }

                    if (i >= BlockLayersIds.Count || (underWater && j >= layersUnderWater.Length))
                    {
                        return pos.Y;
                    }


                    IChunkBlocks chunkdata = chunks[chunkY].Data;
                    chunkdata.SetBlockUnsafe(index3d, underWater ? layersUnderWater[j++] : BlockLayersIds[i++]);
                    chunkdata.SetFluid(index3d, 0);
                }
                else
                {
                    if ((i > 0 && temp > -18) || j > 0) return pos.Y;
                    //                 ^ Hardcoding crime here. Please don't look. Makes deep layers of ice work
                }
            }

            return pos.Y;
        }




        private void GenBeach(int x, int posY, int z, IServerChunk[] chunks, float rainRel, float temp, float beachRel, int topRockId)
        {
            int sandBlockId = blockLayerConfig.BeachLayer.BlockId;

            if (blockLayerConfig.BeachLayer.BlockIdMapping != null && !blockLayerConfig.BeachLayer.BlockIdMapping.TryGetValue(topRockId, out sandBlockId))
            {
                return;
            }

            int index3d = (chunksize * (posY % chunksize) + z) * chunksize + x;
            if (beachRel > 0.5)
            {
                IChunkBlocks chunkdata = chunks[posY / chunksize].Data;
                int blockId = chunkdata.GetBlockIdUnsafe(index3d);
                if (chunkdata.GetFluid(index3d) != GlobalConfig.waterBlockId && blockId != GlobalConfig.lakeIceBlockId && blockId != 0)
                {
                    chunkdata.SetBlockUnsafe(index3d, sandBlockId);
                    chunkdata.SetFluid(index3d, 0);
                }
            }
        }

        void PlaceTallGrass(int x, int posY, int z, IServerChunk[] chunks, float rainRel, float tempRel, float temp, float forestRel)
        {
            double rndVal = blockLayerConfig.Tallgrass.RndWeight * rnd.NextDouble() + blockLayerConfig.Tallgrass.PerlinWeight * grassDensity.Noise(x, z, -0.5f);

            double extraGrass = Math.Max(0, rainRel * tempRel - 0.25);

            if (rndVal <= GameMath.Clamp(forestRel - extraGrass, 0.05, 0.99) || posY >= mapheight - 1 || posY < 1) return;
            
            int blockId = chunks[posY / chunksize].Data[(chunksize * (posY % chunksize) + z) * chunksize + x];

            if (api.World.Blocks[blockId].Fertility <= rnd.NextInt(100)) return;

            double gheight = Math.Max(0, grassHeight.Noise(x, z) * blockLayerConfig.Tallgrass.BlockCodeByMin.Length - 1);
            int start = (int)gheight + (rnd.NextDouble() < gheight ? 1 : 0);

            for (int i = start; i < blockLayerConfig.Tallgrass.BlockCodeByMin.Length; i++)
            {
                TallGrassBlockCodeByMin bcbymin = blockLayerConfig.Tallgrass.BlockCodeByMin[i];

                if (forestRel <= bcbymin.MaxForest && rainRel >= bcbymin.MinRain && temp >= bcbymin.MinTemp)
                {
                    chunks[(posY + 1) / chunksize].Data[(chunksize * ((posY + 1) % chunksize) + z) * chunksize + x] = bcbymin.BlockId;
                    return;
                }
            }
        }


        private void LoadBlockLayers(double posRand, float rainRel, float temperature, int unscaledTemp, int posY, BlockPos pos, int firstBlockId)
        {
            float heightRel = ((float)posY - TerraGenConfig.seaLevel) / ((float)api.WorldManager.MapSizeY - TerraGenConfig.seaLevel);
            float fertilityRel = TerraGenConfig.GetFertilityFromUnscaledTemp((int)(rainRel * 255), unscaledTemp, heightRel) / 255f;
            
            float depthf = TerraGenConfig.SoilThickness(rainRel, temperature, posY - TerraGenConfig.seaLevel, 1f);
            int depth = (int)depthf;
            if (depthf - depth > rnd.NextFloat()) depth++;

            // Hardcoding crime here. Please don't look. Makes deep layers of ice work
            if (temperature < -16) depth += 10;

            BlockLayersIds.Clear();
            for (int j = 0; j < blockLayerConfig.Blocklayers.Length; j++)
            {
                BlockLayer bl = blockLayerConfig.Blocklayers[j];

                float yrel = (float)posY / mapheight;
                float tempDist = Math.Abs(temperature - GameMath.Clamp(temperature, bl.MinTemp, bl.MaxTemp));
                float rainDist = Math.Abs(rainRel - GameMath.Clamp(rainRel, bl.MinRain, bl.MaxRain)) * 10f;
                float fertDist = Math.Abs(fertilityRel - GameMath.Clamp(fertilityRel, bl.MinFertility, bl.MaxFertility)) * 10f;
                float yDist = Math.Abs(yrel - GameMath.Clamp(yrel, bl.MinY, bl.MaxY)) * 10f;
                float trfDist = tempDist + rainDist + fertDist;

                if (trfDist + yDist <= posRand)
                {
                    int blockId = bl.GetBlockId(posRand, temperature, rainRel, fertilityRel, firstBlockId, pos);
                    if (blockId != 0)
                    {
                        BlockLayersIds.Add(blockId);
                        if (bl.Thickness > 1)
                        {
                            for (int i = 1; i < bl.Thickness * (1 - trfDist * yDist); i++)
                            {
                                BlockLayersIds.Add(blockId);
                                yDist = Math.Abs((float)posY-- / mapheight - GameMath.Min((float)posY-- / mapheight, bl.MaxY));
                            }
                        }

                        posY--;
                        temperature = TerraGenConfig.GetScaledAdjustedTemperatureFloat(unscaledTemp, posY - TerraGenConfig.seaLevel);
                        // Would be correct, but doesn't seem to cause noticable problems
                        // so lets not add it for faster chunk gen
                        //  rainRel = TerraGenConfig.GetRainFall(unscaledRain, posY) / 255f;
                        heightRel = ((float)posY - TerraGenConfig.seaLevel) / ((float)api.WorldManager.MapSizeY - TerraGenConfig.seaLevel);
                        fertilityRel = TerraGenConfig.GetFertilityFromUnscaledTemp((int)(rainRel * 255), unscaledTemp, heightRel) / 255f;
                    }
                }

                if (BlockLayersIds.Count >= depth) break;
            }


            layersUnderWater = null;
            for (int j = 0; j < blockLayerConfig.LakeBedLayer.BlockCodeByMin.Length; j++)
            {
                LakeBedBlockCodeByMin lbbc = blockLayerConfig.LakeBedLayer.BlockCodeByMin[j];
                if (lbbc.Suitable(temperature, rainRel, (float)posY / api.WorldManager.MapSizeY, rnd))
                {
                    layersUnderWater = new int[] { lbbc.GetBlockForMotherRock(firstBlockId) };
                    break;
                }
            }
            if (layersUnderWater == null) layersUnderWater = new int[0];

        }

     
    }
}
