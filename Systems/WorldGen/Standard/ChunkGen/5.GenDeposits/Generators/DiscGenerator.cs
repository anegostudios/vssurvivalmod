using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using System.IO;

#nullable disable

namespace Vintagestory.ServerMods
{
    public enum EnumGradeDistribution
    {
        Random,
        RandomPlusDepthBonus
    }

    [JsonObject(MemberSerialization.OptIn)]
    public abstract class DiscDepositGenerator : DepositGeneratorBase
    {
        /// <summary>
        /// Mother rock
        /// </summary>
        [JsonProperty]
        public DepositBlock InBlock;

        /// <summary>
        /// Block to be placed
        /// </summary>
        [JsonProperty]
        public DepositBlock PlaceBlock;

        /// <summary>
        /// Block to be placed on the surface if near surface
        /// </summary>
        [JsonProperty]
        public DepositBlock SurfaceBlock;

        /// <summary>
        /// Radius in blocks, capped 64 blocks
        /// </summary>
        [JsonProperty]
        public NatFloat Radius;

        /// <summary>
        /// Thickness in blocks
        /// </summary>
        [JsonProperty]
        public NatFloat Thickness;

        /// <summary>
        /// for Placement=FollowSurfaceBelow depth is absolute blocks below surface
        /// for Placement=FollowSurface depth in percent. 0 = bottom, 1=surface at current pos
        /// for Placement=Straight depth in percent. 0 = bottom, 1=surface at current pos
        /// for Placement=Anywhere depth in percent. 0 = bottom, 1=map height
        /// for Placement=FollowSeaLevel depth in percent. 0 = bottom, 1=sealevel
        /// </summary>
        [JsonProperty]
        public NatFloat Depth;

        [JsonProperty]
        public float SurfaceBlockChance = 0.05f;

        [JsonProperty]
        public float GenSurfaceBlockChance = 1f;

        [JsonProperty]
        public bool IgnoreParentTestPerBlock = false;

        [JsonProperty]
        public int MaxYRoughness = 999;

        [JsonProperty]
        public bool WithLastLayerBlockCallback;

        [JsonProperty]
        public EnumGradeDistribution GradeDistribution = EnumGradeDistribution.Random;
        protected float currentRelativeDepth;



        // Resolved values
        protected Dictionary<int, ResolvedDepositBlock> placeBlockByInBlockId = new Dictionary<int, ResolvedDepositBlock>();
        protected Dictionary<int, ResolvedDepositBlock> surfaceBlockByInBlockId = new Dictionary<int, ResolvedDepositBlock>();

        public MapLayerBase OreMap;


        protected int worldheight;
        protected int regionChunkSize;
        protected int noiseSizeClimate;
        protected int noiseSizeOre;
        protected int regionSize;

        protected BlockPos targetPos = new BlockPos();
        protected int radiusX, radiusZ;
        protected float ypos;
        protected int posyi;
        protected int depoitThickness;
        protected int hereThickness;

        public double absAvgQuantity;


        protected DiscDepositGenerator(ICoreServerAPI api, DepositVariant variant, LCGRandom depositRand, NormalizedSimplexNoise noiseGen) : base(api, variant, depositRand, noiseGen)
        {
            worldheight = api.World.BlockAccessor.MapSizeY;

            regionSize = api.WorldManager.RegionSize;
            regionChunkSize = api.WorldManager.RegionSize / chunksize;
            noiseSizeClimate = regionSize / TerraGenConfig.climateMapScale;
            noiseSizeOre = regionSize / TerraGenConfig.oreMapScale;
        }



        public override void Init()
        {
            if (Radius == null)
            {
                Api.Server.LogWarning("Deposit {0} has no radius property defined. Defaulting to uniform radius 10", variant.fromFile);
                Radius = NatFloat.createUniform(10, 0);
            }
            if (variant.Climate != null && Radius.avg + Radius.var >= 32)
            {
                Api.Server.LogWarning("Deposit {0} has CheckClimate=true and radius > 32 blocks - this is not supported, sorry. Defaulting to uniform radius 10", variant.fromFile);
                Radius = NatFloat.createUniform(10, 0);
            }


            if (InBlock != null)
            {
                Block[] blocks = Api.World.SearchBlocks(InBlock.Code);
                if (blocks.Length == 0)
                {
                    Api.Server.LogWarning("Deposit in file {0}, no such blocks found by code/wildcard '{1}'. Deposit will never spawn.", variant.fromFile, InBlock.Code);
                }

                foreach (var block in blocks)
                {
                    if (InBlock.AllowedVariants != null && !WildcardUtil.Match(InBlock.Code, block.Code, InBlock.AllowedVariants)) continue;
                    if (InBlock.AllowedVariantsByInBlock != null && !InBlock.AllowedVariantsByInBlock.ContainsKey(block.Code)) continue;

                    string key = InBlock.Name;
                    string value = WildcardUtil.GetWildcardValue(InBlock.Code, block.Code);

                    ResolvedDepositBlock depositBlocks = placeBlockByInBlockId[block.BlockId] = PlaceBlock.Resolve(variant.fromFile, Api, block, key, value);
                    if (SurfaceBlock != null)
                    {
                        surfaceBlockByInBlockId[block.BlockId] = SurfaceBlock.Resolve(variant.fromFile, Api, block, key, value);
                    }

                    Block[] placeBlocks = depositBlocks.Blocks;

                    if (variant.ChildDeposits != null)
                    {
                        foreach (var val in variant.ChildDeposits)
                        {
                            if (val.GeneratorInst == null)
                            {
                                val.InitWithoutGenerator(Api);
                                val.GeneratorInst = new ChildDepositGenerator(Api, val, DepositRand, DistortNoiseGen);
                                JsonUtil.Populate(val.Attributes.Token, val.GeneratorInst);
                            }


                            foreach (Block depositblock in placeBlocks)
                            {
                                (val.GeneratorInst as ChildDepositGenerator).ResolveAdd(depositblock, key, value);
                            }


                        }
                    }

                    // Host rock for
                    if (block.Id != 0 && variant.addHandbookAttributes)
                    {
                        if (block.Attributes == null) block.Attributes = new JsonObject(JToken.Parse("{}"));
                        int[] oreIds = block.Attributes["hostRockFor"].AsArray(Array.Empty<int>());

                        oreIds = oreIds.Append(placeBlocks.Select(b => b.BlockId).ToArray());
                        block.Attributes.Token["hostRockFor"] = JToken.FromObject(oreIds);

                        // In host rock
                        for (int i = 0; i < placeBlocks.Length; i++)
                        {
                            Block pblock = placeBlocks[i];
                            if (pblock.Attributes == null) pblock.Attributes = new JsonObject(JToken.Parse("{}"));
                            oreIds = pblock.Attributes["hostRock"].AsArray(Array.Empty<int>());
                            oreIds = oreIds.Append(block.BlockId);
                            pblock.Attributes.Token["hostRock"] = JToken.FromObject(oreIds);
                        }
                    }
                }
            }
            else
            {
                Api.Server.LogWarning("Deposit in file {0} has no inblock defined, it will never spawn.", variant.fromFile);
            }

            var rnd = new LCGRandom(Api.World.Seed);
            absAvgQuantity = GetAbsAvgQuantity(rnd);
        }


        // Fast hash function returning 0-1
        float FastNoise(int x, int z)
        {
            uint hash = (uint)(x * 1619 + z * 31337);
            hash = (hash ^ 61) ^ (hash >> 16);
            hash = hash * 9;
            hash = hash ^ (hash >> 4);
            hash = hash * 0x27d4eb2d;
            hash = hash ^ (hash >> 15);

            // Convert to the range [0, 1]
            return hash / (float)uint.MaxValue;
        }

        // With interpolation for smoothness, returns 0-1
        float SmoothNoise(int x, int z, float frequency = 1f)
        {
            float fx = x * frequency;
            float fz = z * frequency;

            int ix = (int)Math.Floor(fx);
            int iz = (int)Math.Floor(fz);
            float tx = fx - ix;
            float tz = fz - iz;

            // Bilinear interpolation between 4 points
            float n00 = FastNoise(ix, iz);
            float n10 = FastNoise(ix + 1, iz);
            float n01 = FastNoise(ix, iz + 1);
            float n11 = FastNoise(ix + 1, iz + 1);

            // Anti-aliasing
            tx = tx * tx * (3 - 2 * tx);
            tz = tz * tz * (3 - 2 * tz);

            // Bi-linear interpolation
            float nx0 = n00 * (1 - tx) + n10 * tx;
            float nx1 = n01 * (1 - tx) + n11 * tx;
            return nx0 * (1 - tz) + nx1 * tz;
        }





        public override void GenDeposit(IBlockAccessor blockAccessor, IServerChunk[] chunks, int chunkX, int chunkZ, BlockPos depoCenterPos, ref Dictionary<BlockPos, DepositVariant> subDepositsToPlace)
        {
            int radius = Math.Min(64, (int)Radius.nextFloat(1, DepositRand));
            if (radius <= 0) return;

            // Let's deform that perfect circle a bit (+/- 25%)
            //float deform = GameMath.Clamp(DepositRand.NextFloat() - 0.5f, -0.25f, 0.25f);
            //radiusX = radius - (int)(radius * deform);
            //radiusZ = radius + (int)(radius * deform);

            int deformation = (int)(radius * (DepositRand.NextFloat() - 0.5f) * 0.5f);
            radiusX = radius - deformation;
            radiusZ = radius + deformation;

            int baseX = chunkX * chunksize;
            int baseZ = chunkZ * chunksize;

            // No need to caluclate further if this deposit won't be part of this chunk
            if (depoCenterPos.X + radiusX < baseX - 6 || depoCenterPos.Z + radiusZ < baseZ - 6 || depoCenterPos.X - radiusX >= baseX + chunksize + 6 || depoCenterPos.Z - radiusZ >= baseZ + chunksize + 6) return;

            IMapChunk heremapchunk = chunks[0].MapChunk;

            beforeGenDeposit(heremapchunk, depoCenterPos);

            if (!shouldGenDepositHere(depoCenterPos)) return;


            int extraGrade = GradeDistribution == EnumGradeDistribution.RandomPlusDepthBonus ? GameMath.RoundRandom(DepositRand, GameMath.Clamp(1 - currentRelativeDepth, 0, 1)) : 0;
            int depositGradeIndex = PlaceBlock.MaxGrade == 0 ? 0 : Math.Min(PlaceBlock.MaxGrade - 1, DepositRand.NextInt(PlaceBlock.MaxGrade) + extraGrade);

            // Ok generate
            float th = Thickness.nextFloat(1, DepositRand);
            depoitThickness = (int)th + (DepositRand.NextFloat() < th - (int)th ? 1 : 0);

            float xRadSqInv = 1f / (radiusX * radiusX);
            float zRadSqInv = 1f / (radiusZ * radiusZ);

            bool parentBlockOk = false;
            ResolvedDepositBlock resolvedPlaceBlock = null;


            bool shouldGenSurfaceDeposit = DepositRand.NextFloat() <= GenSurfaceBlockChance && SurfaceBlock != null;

            int lx;
            int lz;
            int distx, distz;

            // No need to go search far beyond chunk boundaries
            int minx = baseX - 6;
            int maxx = baseX + chunksize + 6;
            int minz = baseZ - 6;
            int maxz = baseZ + chunksize + 6;

            minx = GameMath.Clamp(depoCenterPos.X - radiusX, minx, maxx);
            maxx = GameMath.Clamp(depoCenterPos.X + radiusX, minx, maxx);
            minz = GameMath.Clamp(depoCenterPos.Z - radiusZ, minz, maxz);
            maxz = GameMath.Clamp(depoCenterPos.Z + radiusZ, minz, maxz);
            if (minx < baseX) minx = baseX;
            if (maxx > baseX + chunksize) maxx = baseX + chunksize;
            if (minz < baseZ) minz = baseZ;
            if (maxz > baseZ + chunksize) maxz = baseZ + chunksize;

            //int placed = 0;

            float invChunkAreaSize = 1f / (chunksize * chunksize);
            double val;

            int posy;

            for (int posx = minx; posx < maxx; posx++)
            {
                lx = posx - baseX;
                distx = posx - depoCenterPos.X;

                float xSq = distx * distx * xRadSqInv;

                for (int posz = minz; posz < maxz; posz++)
                {
                    posy = depoCenterPos.Y;
                    lz = posz - baseZ;
                    distz = posz - depoCenterPos.Z;


                    // Kinda weird mathematically speaking, but seems to work as a means to distort the perfect circleness of deposits ¯\_(ツ)_/¯
                    // Also not very efficient to use direct perlin noise in here :/
                    // But after ~10 hours of failing (=weird lines of missing deposit material) with a pre-generated 2d distortion map i give up >.>
                    //val = 1 - (radius > 3 ? DistortNoiseGen.Noise(posx / 3.0, posz / 3.0) * 0.2 : 0);

                    val = 1 - (radius > 3 ? (SmoothNoise(posx, posz, 1.0f) * 0.2f) : 0);



                    double distanceToEdge = val - (xSq + distz * distz * zRadSqInv);
                    if (distanceToEdge < 0) continue;

                    targetPos.Set(posx, posy, posz);
                    loadYPosAndThickness(heremapchunk, lx, lz, targetPos, distanceToEdge);
                    posy = targetPos.Y;
                    if (posy >= worldheight) continue;


                    // Some deposits may not appear all over cliffs
                    if (Math.Abs(depoCenterPos.Y - posy) > MaxYRoughness) continue;


                    for (int y = 0; y < hereThickness; y++)
                    {
                        if (posy <= 1) continue;

                        int index3d = ((posy % chunksize) * chunksize + lz) * chunksize + lx;
                        int blockId = chunks[posy / chunksize].Data.GetBlockIdUnsafe(index3d);


                        if (!IgnoreParentTestPerBlock || !parentBlockOk)
                        {
                            parentBlockOk = placeBlockByInBlockId.TryGetValue(blockId, out resolvedPlaceBlock);
                        }

                        if (parentBlockOk && resolvedPlaceBlock.Blocks.Length > 0)
                        {
                            int gradeIndex = Math.Min(resolvedPlaceBlock.Blocks.Length - 1, depositGradeIndex);

                            Block placeblock = resolvedPlaceBlock.Blocks[gradeIndex];

                            if (variant.WithBlockCallback || (WithLastLayerBlockCallback && y == hereThickness - 1))
                            {
                                targetPos.Y = posy;
                                placeblock.TryPlaceBlockForWorldGen(blockAccessor, targetPos, BlockFacing.UP, DepositRand);
                            }
                            else
                            {
                                IChunkBlocks chunkdata = chunks[posy / chunksize].Data;
                                chunkdata.SetBlockUnsafe(index3d, placeblock.BlockId);
                                chunkdata.SetFluid(index3d, 0);
                            }

                            DepositVariant[] childDeposits = variant.ChildDeposits;
                            if (childDeposits != null)
                            {
                                for (int i = 0; i < childDeposits.Length; i++)
                                {
                                    float rndVal = DepositRand.NextFloat();
                                    float quantity = childDeposits[i].TriesPerChunk * invChunkAreaSize;

                                    if (quantity > rndVal)
                                    {
                                        if (ShouldPlaceAdjustedForOreMap(childDeposits[i], posx, posz, quantity, rndVal))
                                        {
                                            subDepositsToPlace[new BlockPos(posx, posy, posz)] = childDeposits[i];
                                        }
                                    }
                                }
                            }

                            if (shouldGenSurfaceDeposit)
                            {
                                int surfaceY = heremapchunk.RainHeightMap[lz * chunksize + lx];
                                int depth = surfaceY - posy;
                                float seaLevelScale = 9f * (TerraGenConfig.seaLevel / 110f); // Default sea level for world height 256
                                float chance = SurfaceBlockChance * Math.Max(0, 1.11f - depth / seaLevelScale);
                                if (surfaceY < worldheight - 1 && DepositRand.NextFloat() < chance)
                                {
                                    Block belowBlock = Api.World.Blocks[chunks[surfaceY / chunksize].Data.GetBlockIdUnsafe(((surfaceY % chunksize) * chunksize + lz) * chunksize + lx)];
                                    if (belowBlock.SideSolid[BlockFacing.UP.Index])
                                    {
                                        index3d = (((surfaceY + 1) % chunksize) * chunksize + lz) * chunksize + lx;
                                        IChunkBlocks chunkBlockData = chunks[(surfaceY + 1) / chunksize].Data;
                                        if (chunkBlockData.GetBlockIdUnsafe(index3d) == 0)
                                        {
                                            chunkBlockData[index3d] = surfaceBlockByInBlockId[blockId].Blocks[0].BlockId;
                                        }
                                    }
                                }
                            }
                        }

                        posy--;
                    }
                }
            }
        }

        protected virtual bool shouldGenDepositHere(BlockPos depoCenterPos)
        {
            return true;
        }

        protected abstract void beforeGenDeposit(IMapChunk mapChunk, BlockPos pos);

        protected abstract void loadYPosAndThickness(IMapChunk heremapchunk, int lx, int lz, BlockPos pos, double distanceToEdge);




        public float getDepositYDistort(BlockPos pos, int lx, int lz, float step, IMapChunk heremapchunk)
        {
            int rdx = (pos.X / chunksize) % regionChunkSize;
            int rdz = (pos.Z / chunksize) % regionChunkSize;


            IMapRegion reg = heremapchunk.MapRegion;

            // repetitive calculations and step are moved out of parentheses for optimization
            var numx = step * (rdx + ((float)lx / chunksize));
            var numz = step * (rdz + ((float)lz / chunksize));

            float yOffTop = reg.OreMapVerticalDistortTop.GetIntLerpedCorrectly(numx, numz) - 20;
            float yOffBot = reg.OreMapVerticalDistortBottom.GetIntLerpedCorrectly(numx, numz) - 20;

            float yRel = (float)pos.Y / worldheight;
            return yOffBot * (1 - yRel) + yOffTop * yRel;

        }



        private bool ShouldPlaceAdjustedForOreMap(DepositVariant variant, int posX, int posZ, float quantity, float rndVal)
        {
            return !variant.WithOreMap || (variant.GetOreMapFactor(posX / chunksize, posZ / chunksize) * quantity > rndVal);
        }





        public override void GetPropickReading(BlockPos pos, int oreDist, int[] blockColumn, out double ppt, out double totalFactor)
        {
            int mapheight = Api.World.BlockAccessor.GetTerrainMapheightAt(pos);
            int qchunkblocks = mapheight * chunksize * chunksize;

            double oreMapFactor = (oreDist & 0xff) / 255.0;
            double rockFactor = oreBearingBlockQuantityRelative(pos, variant.Code, blockColumn);
            totalFactor = oreMapFactor * rockFactor;

            double quantityOres = totalFactor * absAvgQuantity;

            double relq = quantityOres / qchunkblocks;
            ppt = relq * 1000;
        }



        private double oreBearingBlockQuantityRelative(BlockPos pos, string oreCode, int[] blockColumn)
        {
            HashSet<int> oreBearingBlocks = new HashSet<int>();

            if (variant == null) return 0;

            int[] blocks = GetBearingBlocks();
            if (blocks == null) return 1;

            foreach (var val in blocks)
            {
                oreBearingBlocks.Add(val);
            }

            GetYMinMax(pos, out double minYAvg, out double maxYAvg);

            int q = 0;
            for (int ypos = 0; ypos < blockColumn.Length; ypos++)
            {
                if (ypos < minYAvg || ypos > maxYAvg) continue;

                if (oreBearingBlocks.Contains(blockColumn[ypos])) q++;
            }

            return (double)q / blockColumn.Length;
        }

        /// <summary>
        /// This will always return the same result for the same ore generator if Radius or Thickness have not been changed.
        /// <see cref="GetAbsAvgQuantity(LCGRandom)"/>
        /// </summary>
        /// <returns></returns>
        [Obsolete("Use GetAbsAvgQuantity(LCGRandom rnd) instead to ensure your code is seed deterministic.")]
        public float GetAbsAvgQuantity()
        {
            return GetAbsAvgQuantity(new LCGRandom(Api.World.Seed));
        }

        public float GetAbsAvgQuantity(LCGRandom rnd)
        {
            float radius = 0;
            float thickness = 0;
            for (int j = 0; j < 100; j++)
            {
                radius += Radius.nextFloat(1, rnd);
                thickness += Thickness.nextFloat(1, rnd);
            }
            radius /= 100;
            thickness /= 100;

            return thickness * radius * radius * GameMath.PI * variant.TriesPerChunk;
        }


        public int[] GetBearingBlocks()
        {
            return placeBlockByInBlockId.Keys.ToArray();
        }

        public override float GetMaxRadius()
        {
            return (Radius.avg + Radius.var) * 1.3f;
        }
    }
}
