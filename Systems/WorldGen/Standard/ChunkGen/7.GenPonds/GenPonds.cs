using System;
using System.Collections.Generic;
using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.ServerMods.NoObf;

namespace Vintagestory.ServerMods
{
    public class GenPonds : ModStdWorldGen
    {
        ICoreServerAPI api;
        LCGRandom rand;
        int mapheight;
        IWorldGenBlockAccessor blockAccessor;
        readonly QueueOfInt searchPositionsDeltas = new QueueOfInt();
        readonly QueueOfInt pondPositions = new QueueOfInt();
        int searchSize;
        int mapOffset;
        int minBoundary;
        int maxBoundary;

        int climateUpLeft;
        int climateUpRight;
        int climateBotLeft;
        int climateBotRight;
        int[] didCheckPosition;
        int iteration;

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

            if (TerraGenConfig.DoDecorationPass)
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
            didCheckPosition = new int[searchSize * searchSize];

            IAsset asset = api.Assets.Get("worldgen/rockstrata.json");
            RockStrataConfig rockstrata = asset.ToObject<RockStrataConfig>();

            asset = api.Assets.Get("worldgen/blocklayers.json");
            BlockLayerConfig blockLayerConfig = asset.ToObject<BlockLayerConfig>();
            blockLayerConfig.ResolveBlockIds(api, rockstrata);

            lakebedLayerConfig = blockLayerConfig.LakeBedLayer;
        }

        private void OnChunkColumnGen(IChunkColumnGenerateRequest request)
        {
            var chunks = request.Chunks;
            int chunkX = request.ChunkX;
            int chunkZ = request.ChunkZ;

            blockAccessor.BeginColumn();
            LCGRandom rand = this.rand;
            rand.InitPositionSeed(chunkX, chunkZ);
            int chunksize = this.chunksize;
            int maxHeight = mapheight - 1;
            int pondYPos;

            ushort[] heightmap = chunks[0].MapChunk.RainHeightMap;

            IntDataMap2D climateMap = chunks[0].MapChunk.MapRegion.ClimateMap;
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
            int temp = (climateMid >> 16) & 0xff;

            // Lake density at chunk center
            float pondDensity = Math.Max(0, 4 * (rain - 10) / 255f);

            float sealeveltemp = TerraGenConfig.GetScaledAdjustedTemperatureFloat(temp, 0);

            // Less lakes where its below -5 degrees
            pondDensity -= Math.Max(0, 5 - sealeveltemp);

            float maxTries = pondDensity * 10;
            int dx, dz;

            // Surface ponds: starts the attempt at heightmap + 1
            while (maxTries-- > 0f)
            {
                if (maxTries < 1f && rand.NextFloat() > maxTries) break;

                dx = rand.NextInt(chunksize);
                dz = rand.NextInt(chunksize);

                pondYPos = heightmap[dz * chunksize + dx] + 1;
                if (pondYPos <= 0 || pondYPos >= maxHeight) return;

                TryPlacePondAt(dx, pondYPos, dz, chunkX, chunkZ);
            }

            // Look for underground airblocks and attempt to place an underground pond
            int iMaxTries = 600;
            while (iMaxTries-- > 0)
            {
                dx = rand.NextInt(chunksize);
                dz = rand.NextInt(chunksize);

                pondYPos = (int)(rand.NextFloat() * heightmap[dz * chunksize + dx]);
                if (pondYPos <= 0 || pondYPos >= maxHeight) return;  //randomly exits, e.g. 1/96 of the time pondYPos will be 0

                int chunkY = pondYPos / chunksize;
                int dy = pondYPos % chunksize;
                int blockID = chunks[chunkY].Data.GetBlockIdUnsafe((dy * chunksize + dz) * chunksize + dx);

                while (blockID == 0 && pondYPos > 20)
                {
                    pondYPos--;

                    chunkY = pondYPos / chunksize;
                    dy = pondYPos % chunksize;
                    blockID = chunks[chunkY].Data.GetBlockIdUnsafe((dy * chunksize + dz) * chunksize + dx);

                    if (blockID != 0)
                    {
                        TryPlacePondAt(dx, pondYPos, dz, chunkX, chunkZ);
                    }
                }
            }
        }


        /// <summary>
        /// Spreads one water layer of a pond - all at the same y-height.  This starts from the bottom of the pond.  Blocks below must all be solid, or water.<br/>
        /// It will not place any water if the pond would overflow - no solid base or larger than 3*chunksize
        /// </summary>
        public void TryPlacePondAt(int dx, int pondYPos, int dz, int chunkX, int chunkZ, int depth = 0)
        {
            int chunksize = this.chunksize;
            int mapOffset = this.mapOffset;
            int searchSize = this.searchSize;
            int minBoundary = this.minBoundary;
            int maxBoundary = this.maxBoundary;
            int waterID = GlobalConfig.waterBlockId;
            int ndx, ndz;
            searchPositionsDeltas.Clear();
            pondPositions.Clear();

            int basePosX = chunkX * chunksize;
            int basePosZ = chunkZ * chunksize;
            Vec2i tmp = new Vec2i();

            // The starting block is an air block
            int arrayIndex = (dz + mapOffset) * searchSize + dx + mapOffset;
            searchPositionsDeltas.Enqueue(arrayIndex);
            pondPositions.Enqueue(arrayIndex);
            int iteration = ++this.iteration;
            didCheckPosition[arrayIndex] = iteration;

            BlockPos tmpPos = new BlockPos();

            while (searchPositionsDeltas.Count > 0)
            {
                int p = searchPositionsDeltas.Dequeue();
                int px = p % searchSize - mapOffset;
                int pz = p / searchSize - mapOffset;

                foreach (BlockFacing facing in BlockFacing.HORIZONTALS)
                {
                    Vec3i facingNormal = facing.Normali;
                    ndx = px + facingNormal.X;
                    ndz = pz + facingNormal.Z;

                    arrayIndex = (ndz + mapOffset) * searchSize + ndx + mapOffset;

                    // If not already checked, see if we can spread water into this position (queue it) - or do nothing if it's a pond border
                    if (didCheckPosition[arrayIndex] != iteration)
                    {
                        didCheckPosition[arrayIndex] = iteration;

                        tmp.Set(basePosX + ndx, basePosZ + ndz);

                        tmpPos.Set(tmp.X, pondYPos - 1, tmp.Y);
                        Block belowBlock = blockAccessor.GetBlock(tmpPos);

                        bool inBoundary = ndx > minBoundary && ndz > minBoundary && ndx < maxBoundary && ndz < maxBoundary;

                        // Only continue when every position is within our 3x3 chunk search area and has a more or less solid block below (or water)
                        // Note from radfast: this actually runs this check on the edges as well (i.e. it is unnecessarily checking the banks have a solid block below them!) - but changing this could slightly alter worldgen
                        if (inBoundary && (belowBlock.GetLiquidBarrierHeightOnSide(BlockFacing.UP, tmpPos) >= 1.0 || belowBlock.BlockId == waterID))   // This test is OK even for waterID, as GetBlock will correctly return the liquid block if the solid blocks 'layer' is air
                        {
                            // If it's not a bank, spread water into it and queue for further checks from here
                            //if (blockAccessor.GetBlock(tmp.X, pondYPos, tmp.Y).Replaceable >= 6000)
                            tmpPos.Set(tmp.X, pondYPos, tmp.Y);
                            if (blockAccessor.GetBlock(tmpPos).GetLiquidBarrierHeightOnSide(facing.Opposite, tmpPos) < 0.9)
                            {
                                searchPositionsDeltas.Enqueue(arrayIndex);
                                pondPositions.Enqueue(arrayIndex);
                            }
                        }

                        // Exit if those conditions were failed - it means the pond is leaking from the bottom, or the sides (extends beyond min/max boundary)
                        else
                        {
                            pondPositions.Clear();
                            searchPositionsDeltas.Clear();
                            return;
                        }
                    }
                }
            }

            // Now place water into the pondPositions

            int curChunkX, curChunkZ;
            int prevChunkX=-1, prevChunkZ=-1;
            int regionChunkSize = api.WorldManager.RegionSize / chunksize;
            IMapChunk mapchunk=null;
            IServerChunk chunk = null;
            IServerChunk chunkOneBlockBelow = null;

            int ly = GameMath.Mod(pondYPos, chunksize);

            bool extraPondDepth = rand.NextFloat() > 0.5f;
            bool withSeabed = extraPondDepth || pondPositions.Count > 16;

            while (pondPositions.Count > 0)
            {
                int p = pondPositions.Dequeue();
                int px = p % searchSize - mapOffset + basePosX;
                int pz = p / searchSize - mapOffset + basePosZ;
                curChunkX = px / chunksize;
                curChunkZ = pz / chunksize;

                int lx = GameMath.Mod(px, chunksize);
                int lz = GameMath.Mod(pz, chunksize);

                // Get correct chunk and correct climate data if we don't have it already
                if (curChunkX != prevChunkX || curChunkZ != prevChunkZ)
                {
                    chunk = (IServerChunk)blockAccessor.GetChunk(curChunkX, pondYPos / chunksize, curChunkZ);
                    if (chunk == null) chunk = api.WorldManager.GetChunk(curChunkX, pondYPos / chunksize, curChunkZ);
                    chunk.Unpack();

                    if (ly == 0)
                    {
                        chunkOneBlockBelow = ((IServerChunk)blockAccessor.GetChunk(curChunkX, (pondYPos - 1) / chunksize, curChunkZ));
                        if (chunkOneBlockBelow == null) return;
                        chunkOneBlockBelow.Unpack();
                    } else
                    {
                        chunkOneBlockBelow = chunk;
                    }

                    mapchunk = chunk.MapChunk;
                    IntDataMap2D climateMap = mapchunk.MapRegion.ClimateMap;
                    
                    float fac = (float)climateMap.InnerSize / regionChunkSize;
                    int rlX = curChunkX % regionChunkSize;
                    int rlZ = curChunkZ % regionChunkSize;

                    climateUpLeft = climateMap.GetUnpaddedInt((int)(rlX * fac), (int)(rlZ * fac));
                    climateUpRight = climateMap.GetUnpaddedInt((int)(rlX * fac + fac), (int)(rlZ * fac));
                    climateBotLeft = climateMap.GetUnpaddedInt((int)(rlX * fac), (int)(rlZ * fac + fac));
                    climateBotRight = climateMap.GetUnpaddedInt((int)(rlX * fac + fac), (int)(rlZ * fac + fac));

                    prevChunkX = curChunkX;
                    prevChunkZ = curChunkZ;

                    chunkOneBlockBelow.MarkModified();
                    chunk.MarkModified();
                }


                // Raise heightmap by 1 (only relevant for above-ground ponds)
                if (mapchunk.RainHeightMap[lz * chunksize + lx] < pondYPos) mapchunk.RainHeightMap[lz * chunksize + lx] = (ushort)pondYPos;

                // Identify correct climate at this position - could be optimised if we place water into ponds in columns instead of layers
                int climate = GameMath.BiLerpRgbColor((float)lx / chunksize, (float)lz / chunksize, climateUpLeft, climateUpRight, climateBotLeft, climateBotRight);
                float temp = TerraGenConfig.GetScaledAdjustedTemperatureFloat((climate >> 16) & 0xff, pondYPos - TerraGenConfig.seaLevel);


                // 1. Place water or ice block
                int index3d = (ly * chunksize + lz) * chunksize + lx;
                Block existing = api.World.GetBlock(chunk.Data[index3d]);
                if (existing.BlockMaterial == EnumBlockMaterial.Plant) chunk.Data.SetBlockAir(index3d);
                chunk.Data.SetFluid(index3d, temp < -5 ? GlobalConfig.lakeIceBlockId : waterID);


                // 2. Let's make a nice muddy gravely sea bed
                if (!withSeabed) continue;

                // Need to check the block below first
                int index = ly == 0 ? 
                    ((31 * chunksize + lz) * chunksize + lx) : 
                    (((ly - 1) * chunksize + lz) * chunksize + lx)
                ;
                
                // again this would be more efficient if we place water in columns
                Block belowBlock = api.World.Blocks[chunkOneBlockBelow.Data.GetFluid(index)];

                // Water below? Seabed already placed
                if (belowBlock.IsLiquid()) continue;
                
                float rainRel = TerraGenConfig.GetRainFall((climate >> 8) & 0xff, pondYPos) / 255f;
                int rockBlockId = mapchunk.TopRockIdMap[lz * chunksize + lx];
                if (rockBlockId == 0) continue;

                LakeBedBlockCodeByMin[] codes = lakebedLayerConfig.BlockCodeByMin;
                for (int i = 0; i < codes.Length; i++)
                {
                    if (codes[i].Suitable(temp, rainRel, (float)pondYPos / mapheight, rand))
                    {
                        chunkOneBlockBelow.Data[index] = codes[i].GetBlockForMotherRock(rockBlockId);
                        break;
                    }
                }
            }


            if (extraPondDepth)
            {
                TryPlacePondAt(dx, pondYPos+1, dz, chunkX, chunkZ, depth + 1);
            }
        }
    }
}
