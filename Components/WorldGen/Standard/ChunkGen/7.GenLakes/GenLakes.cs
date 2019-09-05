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
    public class GenLakes : ModStdWorldGen
    {
        ICoreServerAPI api;
        LCGRandom rand;
        int mapheight;
        IBlockAccessor blockAccessor;
        Queue<Vec2i> searchPositionsDeltas = new Queue<Vec2i>();
        Queue<Vec2i> lakePositions = new Queue<Vec2i>();
        int searchSize;
        int mapOffset;
        int minBoundary;
        int maxBoundary;

        int ndx, ndz;
        int lakeYPos;

        int climateUpLeft;
        int climateUpRight;
        int climateBotLeft;
        int climateBotRight;
        bool[] didCheckPosition;

        LakeBedLayerProperties lakebedLayerConfig;


        public override double ExecuteOrder()
        {
            return 0.4;
        }

        public override bool ShouldLoad(EnumAppSide side)
        {
            return side == EnumAppSide.Server;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="api"></param>
        public override void StartServerSide(ICoreServerAPI api)
        {
            this.api = api;

            if (DoDecorationPass)
            {
                api.Event.InitWorldGenerator(initWorldGen, "standard");
                api.Event.ChunkColumnGeneration(OnChunkColumnGen, EnumWorldGenPass.TerrainFeatures, "standard");
                api.Event.GetWorldgenBlockAccessor(OnWorldGenBlockAccessor);
            }
        }

        private void OnWorldGenBlockAccessor(IChunkProviderThread chunkProvider)
        {
            blockAccessor = chunkProvider.GetBlockAccessor(true);
        }


        public void initWorldGen()
        {
            LoadGlobalConfig(api);

            rand = new LCGRandom(api.WorldManager.Seed - 12);
            searchSize = 3 * chunksize;
            mapOffset = chunksize;
            minBoundary = -chunksize + 1;
            maxBoundary = 2 * chunksize - 1;
            mapheight = api.WorldManager.MapSizeY;
            didCheckPosition = new bool[searchSize * searchSize];

            IAsset asset = api.Assets.Get("worldgen/rockstrata.json");
            RockStrataConfig rockstrata = asset.ToObject<RockStrataConfig>();

            asset = api.Assets.Get("worldgen/blocklayers.json");
            BlockLayerConfig blockLayerConfig = asset.ToObject<BlockLayerConfig>();
            blockLayerConfig.ResolveBlockIds(api, rockstrata);

            lakebedLayerConfig = blockLayerConfig.LakeBedLayer;
        }

        private void OnChunkColumnGen(IServerChunk[] chunks, int chunkX, int chunkZ, ITreeAttribute chunkGenParams = null)
        {
            rand.InitPositionSeed(chunkX, chunkZ);

            ushort[] heightmap = chunks[0].MapChunk.RainHeightMap;

            IntMap climateMap = chunks[0].MapChunk.MapRegion.ClimateMap;
            int regionChunkSize = api.WorldManager.RegionSize / chunksize;
            float fac = (float)climateMap.InnerSize / regionChunkSize;
            int rlX = chunkX % regionChunkSize;
            int rlZ = chunkZ % regionChunkSize;

            climateUpLeft = climateMap.GetUnpaddedInt((int)(rlX * fac), (int)(rlZ * fac));
            climateUpRight = climateMap.GetUnpaddedInt((int)(rlX * fac + fac), (int)(rlZ * fac));
            climateBotLeft = climateMap.GetUnpaddedInt((int)(rlX * fac), (int)(rlZ * fac + fac));
            climateBotRight = climateMap.GetUnpaddedInt((int)(rlX * fac + fac), (int)(rlZ * fac + fac));

            int climateMid = GameMath.BiLerpRgbColor(0.5f, 0.5f, climateUpLeft, climateUpRight, climateBotLeft, climateBotRight);

            // 16-23 bits = Red = temperature
            // 8-15 bits = Green = rain
            // 0-7 bits = Blue = humidity

            int rain = (climateMid >> 8) & 0xff;
            int humidity = climateMid & 0xff;

            // Lake density at chunk center
            float lakeDensity = 4 * (rain + humidity) / 255f;


            float maxTries = lakeDensity * 10;

            int dx, dz;



            while (maxTries-- > 0)
            {
                if (maxTries < 1 && rand.NextDouble() > maxTries) break;

                dx = rand.NextInt(chunksize);
                dz = rand.NextInt(chunksize);

                TryPlaceLakeAt(dx, dz, chunkX, chunkZ, heightmap);

                //ushort blockId = blockAccessor.GetBlock(new AssetLocation("creativeblock-37")).BlockId;
                //blockAccessor.SetBlock(blockId, new BlockPos(chunkX * chunksize + dx, heightmap[dz * chunksize + dx] + 1, chunkZ * chunksize + dz));
            }
        }



        public void TryPlaceLakeAt(int dx, int dz, int chunkX, int chunkZ, ushort[] heightmap, int depth = 0)
        {
            searchPositionsDeltas.Clear();
            lakePositions.Clear();

            // Clear Array
            for (int i = 0; i < didCheckPosition.Length; i++) didCheckPosition[i] = false;

            lakeYPos = heightmap[dz * chunksize + dx] + 1;

            if (lakeYPos <= 0 || lakeYPos >= mapheight - 1) return;

            int basePosX = chunkX * chunksize;
            int basePosZ = chunkZ * chunksize;
            Vec2i tmp = new Vec2i();

            

            searchPositionsDeltas.Enqueue(new Vec2i(dx, dz));
            lakePositions.Enqueue(new Vec2i(basePosX + dx, basePosZ + dz));
            didCheckPosition[(dz + mapOffset) * searchSize + dx + mapOffset] = true;

            

            while (searchPositionsDeltas.Count > 0)
            {
                Vec2i p = searchPositionsDeltas.Dequeue();
               
                foreach (BlockFacing facing in BlockFacing.HORIZONTALS)
                {
                    ndx = p.X + facing.Normali.X;
                    ndz = p.Y + facing.Normali.Z;

                    tmp.Set(chunkX * chunksize + ndx, chunkZ * chunksize + ndz);

                    Block belowBlock = blockAccessor.GetBlock(tmp.X, lakeYPos - 1, tmp.Y);

                    bool inBoundary = ndx > minBoundary && ndz > minBoundary && ndx < maxBoundary && ndz < maxBoundary;

                    // Only continue when within our 3x3 chunk search area and having a more or less solid block below (or water)
                    if (inBoundary && (belowBlock.Replaceable < 6000 || belowBlock.BlockId == GlobalConfig.waterBlockId))
                    {
                        int arrayIndex = (ndz + mapOffset) * searchSize + ndx + mapOffset;
                        
                        // Already checked or did we reach a lake border? 
                        if (!didCheckPosition[arrayIndex] && blockAccessor.GetBlock(tmp.X, lakeYPos, tmp.Y).Replaceable >= 6000)
                        {
                            searchPositionsDeltas.Enqueue(new Vec2i(ndx, ndz));
                            lakePositions.Enqueue(tmp.Copy());

                            didCheckPosition[arrayIndex] = true;
                        }

                    }
                    else
                    {
                        lakePositions.Clear();
                        searchPositionsDeltas.Clear();
                        return;
                    }
                }
            }

            if (lakePositions.Count == 0) return;


            int curChunkX, curChunkZ;
            int prevChunkX=-1, prevChunkZ=-1;
            int regionChunkSize = api.WorldManager.RegionSize / chunksize;
            IMapChunk mapchunk=null;
            IServerChunk chunk = null;
            IServerChunk chunkOneBlockBelow = null;

            int ly = GameMath.Mod(lakeYPos, chunksize);

            bool extraLakeDepth = rand.NextDouble() > 0.5;
            bool withSeabed = extraLakeDepth || lakePositions.Count > 16;

            foreach (Vec2i p in lakePositions)
            {
                curChunkX = p.X / chunksize;
                curChunkZ = p.Y / chunksize;

                int lx = GameMath.Mod(p.X, chunksize);
                int lz = GameMath.Mod(p.Y, chunksize);

                // Get correct chunk and correct climate data if we don't have it already
                if (curChunkX != prevChunkX || curChunkZ != prevChunkZ)
                {
                    chunk = (IServerChunk)blockAccessor.GetChunk(curChunkX, lakeYPos / chunksize, curChunkZ);
                    if (chunk == null) chunk = api.WorldManager.GetChunk(curChunkX, lakeYPos / chunksize, curChunkZ);
                    chunk.Unpack();

                    if (ly == 0)
                    {
                        chunkOneBlockBelow = ((IServerChunk)blockAccessor.GetChunk(curChunkX, (lakeYPos - 1) / chunksize, curChunkZ));
                        chunkOneBlockBelow.Unpack();
                    } else
                    {
                        chunkOneBlockBelow = chunk;
                    }

                    mapchunk = chunk.MapChunk;
                    IntMap climateMap = mapchunk.MapRegion.ClimateMap;
                    
                    float fac = (float)climateMap.InnerSize / regionChunkSize;
                    int rlX = curChunkX % regionChunkSize;
                    int rlZ = curChunkZ % regionChunkSize;

                    climateUpLeft = climateMap.GetUnpaddedInt((int)(rlX * fac), (int)(rlZ * fac));
                    climateUpRight = climateMap.GetUnpaddedInt((int)(rlX * fac + fac), (int)(rlZ * fac));
                    climateBotLeft = climateMap.GetUnpaddedInt((int)(rlX * fac), (int)(rlZ * fac + fac));
                    climateBotRight = climateMap.GetUnpaddedInt((int)(rlX * fac + fac), (int)(rlZ * fac + fac));

                    prevChunkX = curChunkX;
                    prevChunkZ = curChunkZ;
                }


                // Raise heightmap by 1
                mapchunk.RainHeightMap[lz * chunksize + lx] = (ushort)lakeYPos;

                // Identify correct climate at this position
                int climate = GameMath.BiLerpRgbColor((float)lx / chunksize, (float)lz / chunksize, climateUpLeft, climateUpRight, climateBotLeft, climateBotRight);
                float temp = TerraGenConfig.GetScaledAdjustedTemperatureFloat((climate >> 16) & 0xff, lakeYPos - TerraGenConfig.seaLevel);


                // 1. Place water or ice block 
                chunk.Blocks[(ly * chunksize + lz) * chunksize + lx] = temp < -5 ? GlobalConfig.lakeIceBlockId : GlobalConfig.waterBlockId;


                // 2. Let's make a nice muddy gravely sea bed
                if (!withSeabed) continue;

                // Need to check the block below first
                int index = ly == 0 ? 
                    ((31 * chunksize + lz) * chunksize + lx) : 
                    (((ly - 1) * chunksize + lz) * chunksize + lx)
                ;
                
                Block belowBlock = api.World.Blocks[chunkOneBlockBelow.Blocks[index]];

                // Water below? Seabed already placed
                if (belowBlock.IsLiquid()) continue;
                
                float rainRel = TerraGenConfig.GetRainFall((climate >> 8) & 0xff, lakeYPos) / 255f;
                int rockBlockId = mapchunk.TopRockIdMap[lz * chunksize + lx];
                if (rockBlockId == 0) continue;
                
                for (int i = 0; i < lakebedLayerConfig.BlockCodeByMin.Length; i++)
                {
                    if (lakebedLayerConfig.BlockCodeByMin[i].Suitable(temp, rainRel, (float)lakeYPos / mapheight, rand))
                    {
                        chunkOneBlockBelow.Blocks[index] = lakebedLayerConfig.BlockCodeByMin[i].GetBlockForMotherRock(rockBlockId);
                        break;
                    }
                }
            }


            if (lakePositions.Count > 0 && extraLakeDepth)
            {
                TryPlaceLakeAt(dx, dz, chunkX, chunkZ, heightmap, depth + 1);
            }
        }
    }
}
