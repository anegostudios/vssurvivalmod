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
        Deposits deposits;

        internal override int chunkRange { get { return 3; } }
        public override double ExecuteOrder() { return 0.2; }


        int regionChunkSize;
        int noiseSizeClimate;
        int noiseSizeOre;
        int regionSize;

        /*int depDistUpLeft;
        int depDistUpRight;
        int depDistBotLeft;
        int depDistBotRight;*/

        IBlockAccessor blockAccessor;

        internal FastRandom depositRand;

        Block[] blockTypes;

        public override bool ShouldLoad(EnumAppSide side)
        {
            return side == EnumAppSide.Server;
        }


        public override void StartServerSide(ICoreServerAPI api)
        {
            IAsset asset = api.Assets.Get("worldgen/terrain/standard/deposits.json");
            deposits = asset.ToObject<Deposits>();

            base.StartServerSide(api);
            
            api.Event.ChunkColumnGeneration(GenChunkColumn, EnumWorldGenPass.TerrainFeatures);
            api.Event.GetWorldgenBlockAccessor(OnWorldGenBlockAccessor);
            api.Event.MapRegionGeneration(OnMapRegionGen, EnumPlayStyleFlag.All);
        }




        private void OnWorldGenBlockAccessor(IChunkProviderThread chunkProvider)
        {
            blockAccessor = chunkProvider.GetBlockAccessor(true);
        }

        internal override void OnGameWorldLoaded()
        {
            base.OnGameWorldLoaded();

            regionSize = api.WorldManager.RegionSize;
            regionChunkSize = api.WorldManager.RegionSize / chunksize;
            noiseSizeClimate = regionSize / TerraGenConfig.climateMapScale;
            noiseSizeOre = regionSize / TerraGenConfig.oreMapScale;

            int seed = api.WorldManager.Seed;

            for (int i = 0; i < deposits.variants.Length; i++)
            {
                DepositVariant variant = deposits.variants[i];
                variant.Init(api);

                if (variant.WithOreMap)
                {
                    NoiseOre noiseOre = new NoiseOre(seed++);
                    variant.OreMap = GenMaps.GetOreMap(seed++, noiseOre);
                }

                if (variant.ChildDeposits != null)
                {
                    for (int k = 0; k < variant.ChildDeposits.Length; k++)
                    {
                        DepositVariant childVariant = variant.ChildDeposits[k];
                        if (childVariant.WithOreMap)
                        {
                            NoiseOre noiseOre = new NoiseOre(seed++);
                            childVariant.OreMap = GenMaps.GetOreMap(seed++, noiseOre);
                        }
                    }
                }
            }

            depositRand = new FastRandom(api.WorldManager.Seed + 34613);

            blockTypes = api.World.Blocks;
        }

        

        private void OnMapRegionGen(IMapRegion mapRegion, int regionX, int regionZ)
        {
            IntMap map;

            for (int i = 0; i < deposits.variants.Length; i++)
            {
                DepositVariant variant = deposits.variants[i];
                if (variant.OreMap != null)
                {
                    map = new IntMap();
                    map.Size = noiseSizeOre + 1;
                    map.BottomRightPadding = 1;
                    map.Data = variant.OreMap.GenLayer(regionX * noiseSizeOre, regionZ * noiseSizeOre, noiseSizeOre + 1, noiseSizeOre + 1);
                    mapRegion.OreMaps[variant.Code] = map;
                }

                if (variant.ChildDeposits != null)
                {
                    for (int k = 0; k < variant.ChildDeposits.Length; k++)
                    {
                        DepositVariant childVariant = variant.ChildDeposits[k];
                        if (childVariant.OreMap != null)
                        {
                            map = new IntMap();
                            map.Size = noiseSizeOre + 1;
                            map.BottomRightPadding = 1;
                            map.Data = childVariant.OreMap.GenLayer(regionX * noiseSizeOre, regionZ * noiseSizeOre, noiseSizeOre + 1, noiseSizeOre + 1);
                            mapRegion.OreMaps[childVariant.Code] = map;
                        }
                    }
                }
            }
        }


        internal override void GenChunkColumn(IServerChunk[] chunks, int chunkX, int chunkZ)
        {
            /*IntMap depDistortionMap = chunks[0].MapChunk.MapRegion.DepositDistortionMap;
            // Null check to not crash old worlds
            if (depDistortionMap != null && depDistortionMap.Size > 0)
            {
                
                int rlX = chunkX % regionChunkSize;
                int rlZ = chunkZ % regionChunkSize;

                float facD = (float)depDistortionMap.InnerSize / regionChunkSize;
                depDistUpLeft = depDistortionMap.GetUnpaddedInt((int)(rlX * facD), (int)(rlZ * facD));
                depDistUpRight = depDistortionMap.GetUnpaddedInt((int)(rlX * facD + facD), (int)(rlZ * facD));
                depDistBotLeft = depDistortionMap.GetUnpaddedInt((int)(rlX * facD), (int)(rlZ * facD + facD));
                depDistBotRight = depDistortionMap.GetUnpaddedInt((int)(rlX * facD + facD), (int)(rlZ * facD + facD));
            }*/

            base.GenChunkColumn(chunks, chunkX, chunkZ);
        }

        internal override void GeneratePartial(IServerChunk[] chunks, int intoChunkX, int intoChunkZ, int fromChunkdX, int fromChunkdZ)
        {
            
            for (int i = 0; i < deposits.variants.Length; i++)
            {
                DepositVariant variant = deposits.variants[i];

                float quantityFactor = 1;
                int originChunkX = intoChunkX + fromChunkdX;
                int originChunkZ = intoChunkZ + fromChunkdZ;

                if (variant.WithOreMap)
                {
                    IMapRegion originMapRegion = api.WorldManager.GetMapRegion((originChunkX * chunksize) / regionSize, (originChunkZ * chunksize) / regionSize);
                    if (originMapRegion == null) continue;
                    int lx = (originChunkX * chunksize + chunksize / 2) % regionSize;
                    int lz = (originChunkZ * chunksize + chunksize / 2) % regionSize;

                    IntMap map = null;
                    originMapRegion.OreMaps.TryGetValue(variant.Code, out map);
                    if (map != null)
                    {
                        float posXInRegionOre = GameMath.Clamp((float)lx / regionSize * noiseSizeOre, 0, noiseSizeOre - 1);
                        float posZInRegionOre = GameMath.Clamp((float)lz / regionSize * noiseSizeOre, 0, noiseSizeOre - 1);

                        int oreDist = originMapRegion.OreMaps[variant.Code].GetUnpaddedColorLerped(posXInRegionOre, posZInRegionOre);

                        quantityFactor = (oreDist & 0xff) / 255f;
                    }
                }

                float qModified = variant.Quantity * quantityFactor;
                int quantity = (int)qModified;
                quantity += chunkRand.NextInt(100) < 100 * (qModified - quantity) ? 1 : 0;
                

                while (quantity-- > 0)
                {
                    int offsetX = chunksize * fromChunkdX + chunkRand.NextInt(chunksize);
                    int offsetZ = chunksize * fromChunkdZ + chunkRand.NextInt(chunksize);

                    depositRand.SetWorldSeed(chunkRand.NextInt(10000000));
                    depositRand.InitPositionSeed(intoChunkX + fromChunkdX, intoChunkZ + fromChunkdZ);

                    Dictionary<Vec3i, DepositVariant> SubDepositsToPlace = GenDeposit(chunks, intoChunkX, intoChunkZ, offsetX, offsetZ, variant);


                    foreach (var val in SubDepositsToPlace)
                    {
                        depositRand.SetWorldSeed(chunkRand.NextInt(10000000));
                        depositRand.InitPositionSeed(intoChunkX + fromChunkdX, intoChunkZ + fromChunkdZ);

                        GenDeposit(chunks, intoChunkX, intoChunkZ, val.Key.X, val.Key.Z, val.Value, val.Key.Y);
                    }

                }
            }
        }


        BlockPos tmpPos = new BlockPos();
        

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
        Dictionary<Vec3i, DepositVariant> GenDeposit(IServerChunk[] chunks, int chunkX, int chunkZ, int offsetX, int offsetZ, DepositVariant variant, int? forceInitialPosY = null)
        {
            Dictionary<Vec3i, DepositVariant> SubDepositsToPlace = new Dictionary<Vec3i, DepositVariant>();

            IMapChunk mapchunk = chunks[0].MapChunk;
            
            int radius = Math.Min(64, (int)variant.Radius.nextFloat(1, depositRand));

            if (radius <= 0) return SubDepositsToPlace;
            
            // Let's deform that perfect circle a bit (+/- 25%)
            float deform = GameMath.Clamp(depositRand.NextFloat() - 0.5f, -0.25f, 0.25f);
            int radiusX = radius - (int)(radius * deform);
            int radiusZ = radius + (int)(radius * deform);
            int posY;

            // No need to caluclate further if this deposit won't be part of this chunk
            if (radiusX + offsetX < 0 || radiusZ + offsetZ < 0 || offsetX - radiusX >= chunksize || offsetZ - radiusZ >= chunksize) return SubDepositsToPlace;


            IMapChunk originMapchunk = null;
            int origPosY = 0;

            int lx = GameMath.Mod(offsetX, chunksize);
            int lz = GameMath.Mod(offsetZ, chunksize);

            if (variant.MaxY < 1 || variant.CheckClimate)
            {
                originMapchunk = api.WorldManager.GetMapChunk((chunkX * chunksize + offsetX) / chunksize, (chunkZ * chunksize + offsetZ) / chunksize);
                if (originMapchunk == null) return SubDepositsToPlace; // argh >.<

                origPosY = originMapchunk.RainHeightMap[lz * chunksize + lx];
                if ((float)origPosY / api.World.BlockAccessor.MapSizeY > variant.MaxY) return SubDepositsToPlace;
            }


            
            
            

            // Check if suited for this area, climate wise
            if (variant.CheckClimate) {
                IntMap climateMap = api.World.BlockAccessor.GetMapRegion((chunkX * chunksize + offsetX) / regionSize, (chunkZ * chunksize + offsetZ) / regionSize).ClimateMap;
                
                float posXInRegionClimate = ((float)lx / regionSize - lx / regionSize) * noiseSizeClimate;
                float posZInRegionClimate = ((float)lz / regionSize - lz / regionSize) * noiseSizeClimate;
                
                int climate = climateMap.GetUnpaddedColorLerped(posXInRegionClimate, posZInRegionClimate);
                float temp = TerraGenConfig.GetScaledAdjustedTemperatureFloat((climate >> 16) & 0xff, origPosY - TerraGenConfig.seaLevel);
                float rainRel = TerraGenConfig.GetRainFall((climate >> 8) & 0xff, origPosY) / 255f;

                if (rainRel < variant.MinRain || rainRel > variant.MaxRain || temp < variant.MinTemp || temp > variant.MaxTemp) return SubDepositsToPlace;
            }


            // Ok generate
            float th = variant.Thickness.nextFloat(1, depositRand);
            int thickness = (int)th + (depositRand.NextFloat() < th - (int)th ? 1 : 0);

            float xRadSqInv = 1f / (radiusX * radiusX);
            float zRadSqInv = 1f / (radiusZ * radiusZ);

            int blockIndex = 0;
            bool parentBlockOk = false;

            float depthf;

            bool shouldGenSurfaceDeposit = depositRand.NextFloat() > 0.35f && variant.SurfaceBlockCode != null;

            if (forceInitialPosY != null)
            {
                depthf = (float)forceInitialPosY / mapchunk.RainHeightMap[offsetX * chunksize + offsetZ];
            } else
            {
                depthf = variant.Depth.nextFloat(1, depositRand);
            }

            int depthi = (int)depthf;

            int topLeft = 2 * depositRand.NextInt(radiusX + 1) - radiusX;
            int topRight = 2 * depositRand.NextInt(radiusZ + 1) - radiusZ;
            int botLeft = 2 * depositRand.NextInt(radiusX + 1) - radiusX;
            int botRight = 2 * depositRand.NextInt(radiusZ + 1) - radiusZ;
            int yOff = 0;

            // Only generate inside this current chunk column
            int minx = GameMath.Clamp(offsetX - radiusX, 0, chunksize);
            int maxx = GameMath.Clamp(offsetX + radiusX, 0, chunksize);
            int minz = GameMath.Clamp(offsetZ - radiusZ, 0, chunksize);
            int maxz = GameMath.Clamp(offsetZ + radiusZ, 0, chunksize);

            float invChunkAreaSize = 1f / (chunksize * chunksize);

            for (int x = minx; x < maxx; x++)
            {
                float xSq = (x-offsetX) * (x- offsetX) * xRadSqInv;
                for (int z = minz; z < maxz; z++)
                {
                    if (xSq + (z - offsetZ) * (z - offsetZ) * zRadSqInv > 1) continue;

                    if (variant.Placement == EnumDepositPlacement.FollowSurfaceBelow)
                    {
                        posY = mapchunk.RainHeightMap[z * chunksize + x] - depthi;
                    }
                    else if (variant.Placement == EnumDepositPlacement.FollowSurface)
                    {
                        yOff = (int)GameMath.BiLerp(topLeft, topRight, botLeft, botRight, (x - offsetX + radiusX) / (2f * radiusX), (z - offsetZ + radiusZ) / (2f * radiusZ));

                        posY = (int)(depthf * mapchunk.RainHeightMap[z * chunksize + x]) + yOff / 2;
                    }
                    else if (variant.Placement == EnumDepositPlacement.Straight)
                    {
                        posY = (int)(depthf * mapchunk.RainHeightMap[z * chunksize + x]);
                    }
                    else
                    {
                        yOff = (int)GameMath.BiLerp(topLeft, topRight, botLeft, botRight, (x - offsetX + radiusX) / (2f * radiusX), (z - offsetZ + radiusZ) / (2f * radiusZ));

                        posY = depthi + yOff;
                    }

                    // Some deposits may not appear all over cliffs
                    if (variant.CheckClimate && Math.Abs(origPosY - posY) > variant.MaxYRoughness) continue;

                    for (int y = 0; y < thickness; y++)
                    {  
                        if (posY <= 1 || posY >= worldheight) continue;

                        long index3d = ((posY % chunksize) * chunksize + z) * chunksize + x;
                        ushort blockId = chunks[posY / chunksize].Blocks[index3d];

                        // Check if we are in mother material, but only if it has changed since last iteration (should reduce amount of these checks by 50-100%)
                        parentBlockOk = false;
                        for (int i = 0; i < variant.ParentBlockIds.Length; i++)
                        {
                            if (variant.ParentBlockIds[i] == blockId)
                            {
                                parentBlockOk = true;
                                blockIndex = i;
                                break;
                            }
                        }

                        if (parentBlockOk)
                        {
                            if (variant.WithBlockCallback)
                            {
                                tmpPos.Set(chunkX * chunksize + x, posY, chunkZ * chunksize + z);
                                blockTypes[variant.BlockIds[blockIndex]].TryPlaceBlockForWorldGen(blockAccessor, tmpPos, BlockFacing.UP);
                            } else
                            {
                                chunks[posY / chunksize].Blocks[index3d] = variant.BlockIds[blockIndex];
                            }


                            for (int i = 0; i < variant.ChildDeposits.Length; i++)
                            {
                                float rndVal = depositRand.NextFloat();
                                float quantity = variant.ChildDeposits[i].Quantity * invChunkAreaSize;

                                if (quantity > rndVal)
                                {
                                    Vec3i pos = new Vec3i(x, posY, z);

                                    if (ShouldPlaceAdjustedForOreMap(variant.ChildDeposits[i], chunkX * chunksize + x, chunkZ * chunksize + z, quantity, rndVal))
                                    {
                                        SubDepositsToPlace[pos] = variant.ChildDeposits[i];
                                    }
                                }
                            }

                            if (shouldGenSurfaceDeposit)
                            {
                                int surfaceY = mapchunk.RainHeightMap[z * chunksize + x];
                                int depth = surfaceY - posY;
                                float chance = variant.SurfaceBlockChance * Math.Max(0, 1 - depth / 8f);
                                if (depositRand.NextFloat() < chance)
                                {
                                    index3d = (((surfaceY+1) % chunksize) * chunksize + z) * chunksize + x;

                                    Block belowBlock = api.World.Blocks[chunks[surfaceY / chunksize].Blocks[((surfaceY % chunksize) * chunksize + z) * chunksize + x]];

                                    if (belowBlock.SideSolid[BlockFacing.UP.Index] && chunks[(surfaceY + 1) / chunksize].Blocks[index3d] == 0)
                                    {
                                        chunks[(surfaceY + 1) / chunksize].Blocks[index3d] = variant.SurfaceBlockIds[blockIndex];
                                    }
                                }
                            }
                        }

                        posY--;
                    }
                }
            }

            return SubDepositsToPlace;
        }


        private bool ShouldPlaceAdjustedForOreMap(DepositVariant variant, int posX, int posZ, float quantity, float rndVal)
        {
            if (!variant.WithOreMap) return true;

            float quantityFactor = 1;
            IMapRegion originMapRegion = api.WorldManager.GetMapRegion((posX) / regionSize, (posZ) / regionSize);
            if (originMapRegion == null) return false;
            int lx = posX % regionSize;
            int lz = posZ % regionSize;

            IntMap map = null;
            originMapRegion.OreMaps.TryGetValue(variant.Code, out map);
            if (map != null)
            {
                float posXInRegionOre = (float)lx / regionSize * noiseSizeOre;
                float posZInRegionOre = (float)lz / regionSize * noiseSizeOre;

                int oreDist = originMapRegion.OreMaps[variant.Code].GetUnpaddedColorLerped(posXInRegionOre, posZInRegionOre);

                quantityFactor = (oreDist & 0xff) / 255f;
            }

            return quantity * quantityFactor > rndVal;
        }
    }
}
