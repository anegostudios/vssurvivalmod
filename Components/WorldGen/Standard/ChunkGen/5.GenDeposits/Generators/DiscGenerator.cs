using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace Vintagestory.ServerMods
{
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




        // Resolved values
        protected Dictionary<int, ResolvedDepositBlock> placeBlockByInBlockId = new Dictionary<int, ResolvedDepositBlock>();
        protected Dictionary<int, ResolvedDepositBlock> surfaceBlockByInBlockId = new Dictionary<int, ResolvedDepositBlock>();
        //protected Dictionary<int, ChildDepositGenerator> childGeneratorsByInBlockId = new Dictionary<int, ChildDepositGenerator>();

        public MapLayerBase OreMap;


        protected int chunksize;
        protected int worldheight;
        protected int regionChunkSize;
        protected int noiseSizeClimate;
        protected int noiseSizeOre;
        protected int regionSize;

        protected BlockPos targetPos = new BlockPos();
        protected int radiusX, radiusZ;
        protected float depthf;
        protected int depthi;
        protected int depoitThickness;
        protected int hereThickness;

        LCGRandom childDepositRand;

        protected DiscDepositGenerator(ICoreServerAPI api, DepositVariant variant, LCGRandom depositRand, NormalizedSimplexNoise noiseGen) : base(api, variant, depositRand, noiseGen)
        {
            chunksize = api.World.BlockAccessor.ChunkSize;
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

                    placeBlockByInBlockId[block.BlockId] = PlaceBlock.Resolve(variant.fromFile, Api, block, key, value);
                    if (SurfaceBlock != null)
                    {
                        surfaceBlockByInBlockId[block.BlockId] = SurfaceBlock.Resolve(variant.fromFile, Api, block, key, value);
                    }

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

                            
                            foreach (Block depositblock in placeBlockByInBlockId[block.BlockId].Blocks)
                            {
                                (val.GeneratorInst as ChildDepositGenerator).ResolveAdd(depositblock, key, value);
                            }

                            
                        }
                    }

                    // Host rock for
                    if (block.Attributes == null) block.Attributes = new JsonObject(JToken.Parse("{}"));
                    int[] oreIds = block.Attributes["hostRockFor"].AsArray<int>(new int[0]);
                    oreIds = oreIds.Append(placeBlockByInBlockId[block.BlockId].Blocks.Select(b => b.BlockId).ToArray());
                    block.Attributes.Token["hostRockFor"] = JToken.FromObject(oreIds);

                    // In host rock
                    Block[] placeBlocks = placeBlockByInBlockId[block.BlockId].Blocks;
                    for (int i = 0; i < placeBlocks.Length; i++)
                    {
                        Block pblock = placeBlocks[i];
                        if (pblock.Attributes == null) pblock.Attributes = new JsonObject(JToken.Parse("{}"));
                        oreIds = pblock.Attributes["hostRock"].AsArray<int>(new int[0]);
                        oreIds = oreIds.Append(block.BlockId);
                        pblock.Attributes.Token["hostRock"] = JToken.FromObject(oreIds);
                    }

                }
            }
            else
            {
                Api.Server.LogWarning("Deposit in file {0} has no inblock defined, it will never spawn.", variant.fromFile);
            }
        }




        public override void GenDeposit(IBlockAccessor blockAccessor, IServerChunk[] chunks, int chunkX, int chunkZ, BlockPos depoCenterPos, ref Dictionary<BlockPos, DepositVariant> subDepositsToPlace)
        {
            int depositGradeIndex = PlaceBlock.MaxGrade == 0 ? 0 : DepositRand.NextInt(PlaceBlock.MaxGrade);

            int radius = Math.Min(64, (int)Radius.nextFloat(1, DepositRand));
            if (radius <= 0) return;


            // Let's deform that perfect circle a bit (+/- 25%)
            float deform = GameMath.Clamp(DepositRand.NextFloat() - 0.5f, -0.25f, 0.25f);
            radiusX = radius - (int)(radius * deform);
            radiusZ = radius + (int)(radius * deform);
            
            
            int baseX = chunkX * chunksize;
            int baseZ = chunkZ * chunksize;


            // No need to caluclate further if this deposit won't be part of this chunk
            if (depoCenterPos.X + radiusX < baseX - 6 || depoCenterPos.Z + radiusZ < baseZ - 6 || depoCenterPos.X - radiusX >= baseX + chunksize + 6 || depoCenterPos.Z - radiusZ >= baseZ + chunksize + 6) return;

            IMapChunk heremapchunk = chunks[0].MapChunk;

            
            beforeGenDeposit(heremapchunk, depoCenterPos);
            
            // Ok generate
            float th = Thickness.nextFloat(1, DepositRand);
            depoitThickness = (int)th + (DepositRand.NextFloat() < th - (int)th ? 1 : 0);

            float xRadSqInv = 1f / (radiusX * radiusX);
            float zRadSqInv = 1f / (radiusZ * radiusZ);

            bool parentBlockOk = false;
            ResolvedDepositBlock resolvedPlaceBlock = null;


            bool shouldGenSurfaceDeposit = DepositRand.NextFloat() <= GenSurfaceBlockChance && SurfaceBlock != null;

            int lx = GameMath.Mod(depoCenterPos.X, chunksize);
            int lz = GameMath.Mod(depoCenterPos.Z, chunksize);
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

            //int placed = 0;

            float invChunkAreaSize = 1f / (chunksize * chunksize);
            double val = 1;
            
            for (int posx = minx; posx < maxx; posx++)
            {
                targetPos.X = posx;
                lx = targetPos.X - baseX;
                distx = posx - depoCenterPos.X;

                float xSq = distx * distx * xRadSqInv;

                for (int posz = minz; posz < maxz; posz++)
                {

                    targetPos.Y = depoCenterPos.Y;
                    targetPos.Z = posz;
                    lz = targetPos.Z - baseZ;
                    distz = posz - depoCenterPos.Z;


                    // Kinda weird mathematically speaking, but seems to work as a means to distort the perfect circleness of deposits ¯\_(ツ)_/¯
                    // Also not very efficient to use direct perlin noise in here :/
                    // But after ~10 hours of failing (=weird lines of missing deposit material) with a pre-generated 2d distortion map i give up >.>
                    val = 1 - (radius > 3 ? DistortNoiseGen.Noise(targetPos.X / 3.0, targetPos.Z / 3.0) * 0.2 : 0);
                    double distanceToEdge = val - (xSq + distz * distz * zRadSqInv);

                    if (distanceToEdge < 0 || lx < 0 || lz < 0 || lx >= chunksize || lz >= chunksize) continue;

                    
                    loadYPosAndThickness(heremapchunk, lx, lz, targetPos, distanceToEdge);


                    // Some deposits may not appear all over cliffs
                    if (Math.Abs(depoCenterPos.Y - targetPos.Y) > MaxYRoughness) continue;


                    for (int y = 0; y < hereThickness; y++)
                    {
                        if (targetPos.Y <= 1 || targetPos.Y >= worldheight) continue;

                        int index3d = ((targetPos.Y % chunksize) * chunksize + lz) * chunksize + lx;
                        int blockId = chunks[targetPos.Y / chunksize].Blocks[index3d];


                        if (!IgnoreParentTestPerBlock || !parentBlockOk)
                        {
                            parentBlockOk = placeBlockByInBlockId.TryGetValue(blockId, out resolvedPlaceBlock);
                        }

                        if (parentBlockOk && resolvedPlaceBlock.Blocks.Length > 0)
                        {
                            int gradeIndex = Math.Min(resolvedPlaceBlock.Blocks.Length - 1, depositGradeIndex);
                            
                            Block placeblock = resolvedPlaceBlock.Blocks[gradeIndex];

                            if (variant.WithBlockCallback || (WithLastLayerBlockCallback && y == hereThickness-1))
                            {
                                placeblock.TryPlaceBlockForWorldGen(blockAccessor, targetPos.Copy(), BlockFacing.UP, rand);
                            }
                            else
                            {
                                chunks[targetPos.Y / chunksize].Blocks[index3d] = placeblock.BlockId;
                                //placed++;
                            }

                            if (variant.ChildDeposits != null)
                            {
                                for (int i = 0; i < variant.ChildDeposits.Length; i++)
                                {
                                    float rndVal = DepositRand.NextFloat();
                                    float quantity = variant.ChildDeposits[i].TriesPerChunk * invChunkAreaSize;

                                    if (quantity > rndVal)
                                    {
                                        if (ShouldPlaceAdjustedForOreMap(variant.ChildDeposits[i], targetPos.X, targetPos.Z, quantity, rndVal))
                                        {
                                            subDepositsToPlace[targetPos.Copy()] = variant.ChildDeposits[i];
                                        }
                                    }
                                }
                            }

                            if (shouldGenSurfaceDeposit)
                            {
                                int surfaceY = heremapchunk.RainHeightMap[lz * chunksize + lx];
                                int depth = surfaceY - targetPos.Y;
                                float chance = SurfaceBlockChance * Math.Max(0, 1.11f - depth / 9f);
                                if (surfaceY < worldheight && DepositRand.NextFloat() < chance)
                                {
                                    Block belowBlock = Api.World.Blocks[chunks[surfaceY / chunksize].Blocks[((surfaceY % chunksize) * chunksize + lz) * chunksize + lx]];

                                    index3d = (((surfaceY + 1) % chunksize) * chunksize + lz) * chunksize + lx;
                                    if (belowBlock.SideSolid[BlockFacing.UP.Index] && chunks[(surfaceY + 1) / chunksize].Blocks[index3d] == 0)
                                    {
                                        chunks[(surfaceY + 1) / chunksize].Blocks[index3d] = surfaceBlockByInBlockId[blockId].Blocks[0].BlockId;
                                    }
                                }
                            }
                        }

                        targetPos.Y--;
                    }
                }
            }

            //Console.WriteLine("placed {0} blocks", placed);
        }


        protected abstract void beforeGenDeposit(IMapChunk mapChunk, BlockPos pos);

        protected abstract void loadYPosAndThickness(IMapChunk heremapchunk, int lx, int lz, BlockPos pos, double distanceToEdge);


        public float getDepositYDistort(BlockPos pos, int lx, int lz, float step, IMapChunk heremapchunk)
        {
            int rdx = (pos.X / chunksize) % regionChunkSize;
            int rdz = (pos.Z / chunksize) % regionChunkSize;

            
            IMapRegion reg = heremapchunk.MapRegion;
            float yOffTop = reg.OreMapVerticalDistortTop.GetIntLerpedCorrectly(rdx * step + step * ((float)lx / chunksize), rdz * step + step * ((float)lz / chunksize)) - 20;
            float yOffBot = reg.OreMapVerticalDistortBottom.GetIntLerpedCorrectly(rdx * step + step * ((float)lx / chunksize), rdz * step + step * ((float)lz / chunksize)) - 20;

            float yRel = (float)pos.Y / worldheight;
            return yOffBot * (1 - yRel) + yOffTop * yRel;
            
        }



        private bool ShouldPlaceAdjustedForOreMap(DepositVariant variant, int posX, int posZ, float quantity, float rndVal)
        {
            return !variant.WithOreMap || (variant.GetOreMapFactor(posX / chunksize, posZ / chunksize) * quantity > rndVal);
        }





        Random avgQRand = new Random();
        public override float GetAbsAvgQuantity()
        {
            float radius = 0;
            float thickness = 0;
            for (int j = 0; j < 100; j++)
            {
                radius += Radius.nextFloat(1, avgQRand);
                thickness += Thickness.nextFloat(1, avgQRand);
            }
            radius /= 100;
            thickness /= 100;

            return thickness * radius * radius * GameMath.PI * variant.TriesPerChunk;
        }


        public override int[] GetBearingBlocks()
        {
            return placeBlockByInBlockId.Keys.ToArray();
        }

        public override float GetMaxRadius()
        {
            return (Radius.avg + Radius.var) * 1.3f;
        }
    }
}
