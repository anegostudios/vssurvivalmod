﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

#nullable disable

namespace Vintagestory.ServerMods
{
    [JsonObject(MemberSerialization.OptIn)]
    public class AlluvialDepositGenerator : DepositGeneratorBase
    {
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
        public int MaxYRoughness = 999;
        
        protected int worldheight;

        protected int radiusX, radiusZ;




        public AlluvialDepositGenerator(ICoreServerAPI api, DepositVariant variant, LCGRandom depositRand, NormalizedSimplexNoise noiseGen) : base(api, variant, depositRand, noiseGen)
        {
            worldheight = api.World.BlockAccessor.MapSizeY;
        }



        public override void Init()
        {
            if (Radius == null)
            {
                Api.Server.LogWarning("Alluvial Deposit {0} has no radius property defined. Defaulting to uniform radius 10", variant.fromFile);
                Radius = NatFloat.createUniform(10, 0);
            }
            if (variant.Climate != null && Radius.avg + Radius.var >= 32)
            {
                Api.Server.LogWarning("Alluvial Deposit {0} has CheckClimate=true and radius > 32 blocks - this is not supported, sorry. Defaulting to uniform radius 10", variant.fromFile);
                Radius = NatFloat.createUniform(10, 0);
            }
        }




        public override void GenDeposit(IBlockAccessor blockAccessor, IServerChunk[] chunks, int chunkX, int chunkZ, BlockPos depoCenterPos, ref Dictionary<BlockPos, DepositVariant> subDepositsToPlace)
        {
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

            
            // Ok generate
            float th = Thickness.nextFloat(1, DepositRand);
            float depositThickness = (int)th + (DepositRand.NextFloat() < th - (int)th ? 1 : 0);

            float xRadSqInv = 1f / (radiusX * radiusX);
            float zRadSqInv = 1f / (radiusZ * radiusZ);

            int lx, lz;
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

            double val;

            IList<Block> blocktypes = Api.World.Blocks;

            double sandMaxY = Api.World.BlockAccessor.MapSizeY * 0.8;
            bool doGravel = depoCenterPos.Y > sandMaxY || DepositRand.NextFloat() > 0.33;

            int posy;
            int rockblockCached = -1;
            Block alluvialblock = null;

            for (int posx = minx; posx < maxx; posx++)
            {
                lx = posx - baseX;
                distx = posx - depoCenterPos.X;

                float xSq = distx * distx * xRadSqInv;

                for (int posz = minz; posz < maxz; posz++)
                {
                    lz = posz - baseZ;
                    distz = posz - depoCenterPos.Z;

                    posy = heremapchunk.WorldGenTerrainHeightMap[lz * chunksize + lx];
                    if (posy >= worldheight) continue;

                    // Some deposits may not appear all over cliffs
                    if (Math.Abs(depoCenterPos.Y - posy) > MaxYRoughness) continue;

                    int rockblockid = heremapchunk.TopRockIdMap[lz * chunksize + lx];
                    if (rockblockid != rockblockCached)
                    {
                        rockblockCached = rockblockid;
                        Block rockblock = blocktypes[rockblockid];
                        if (!rockblock.Variant.ContainsKey("rock"))
                        {
                            alluvialblock = null;
                        }
                        else
                        {
                            alluvialblock = Api.World.GetBlock(new AssetLocation((doGravel ? "gravel-" : "sand-") + rockblock.Variant["rock"]));
                        }
                    }
                    if (alluvialblock == null) continue;

                    // Kinda weird mathematically speaking, but seems to work as a means to distort the perfect circleness of deposits ¯\_(ツ)_/¯
                    // Also not very efficient to use direct perlin noise in here :/
                    // But after ~10 hours of failing (=weird lines of missing deposit material) with a pre-generated 2d distortion map i give up >.>
                    val = 1.0 - DistortNoiseGen.Noise(posx / 3.0, posz / 3.0) * 1.5 + 0.15;
                    double distanceToEdge = val - (xSq + distz * distz * zRadSqInv);
                    if (distanceToEdge < 0.0) continue;

                    for (int yy = 0; yy < depositThickness; yy++)
                    {
                        if (posy <= 1) continue;

                        int index3d = ((posy % chunksize) * chunksize + lz) * chunksize + lx;
                        IChunkBlocks chunkdata = chunks[posy / chunksize].Data;
                        int blockId = chunkdata.GetBlockIdUnsafe(index3d);

                        Block block = blocktypes[blockId];
                        if (alluvialblock.BlockMaterial == EnumBlockMaterial.Soil && block.BlockMaterial != EnumBlockMaterial.Soil) continue;

                        chunkdata.SetBlockUnsafe(index3d, alluvialblock.BlockId);
                        chunkdata.SetFluid(index3d, 0);
                        posy--;
                    }
                }
            }
        }



        Random avgQRand = new Random();
        public float GetAbsAvgQuantity()
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


        public int[] GetBearingBlocks()
        {
            return Array.Empty<int>();
        }

        public override float GetMaxRadius()
        {
            return (Radius.avg + Radius.var) * 1.3f;
        }

        public override void GetPropickReading(BlockPos pos, int oreDist, int[] blockColumn, out double ppt, out double totalFactor)
        {
            throw new NotImplementedException();
        }

        public override void GetYMinMax(BlockPos pos, out double miny, out double maxy)
        {
            throw new NotImplementedException();
        }
    }
}
