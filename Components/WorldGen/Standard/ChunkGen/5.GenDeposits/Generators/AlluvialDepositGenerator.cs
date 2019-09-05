using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

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
        
        protected int chunksize;
        protected int worldheight;

        protected BlockPos targetPos = new BlockPos();
        protected int radiusX, radiusZ;




        public AlluvialDepositGenerator(ICoreServerAPI api, DepositVariant variant, LCGRandom depositRand, NormalizedSimplexNoise noiseGen) : base(api, variant, depositRand, noiseGen)
        {
            chunksize = api.World.BlockAccessor.ChunkSize;
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
            float depoitThickness = (int)th + (DepositRand.NextFloat() < th - (int)th ? 1 : 0);

            float xRadSqInv = 1f / (radiusX * radiusX);
            float zRadSqInv = 1f / (radiusZ * radiusZ);

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

            float invChunkAreaSize = 1f / (chunksize * chunksize);
            double val = 1;

            List<Block> blocktypes = Api.World.Blocks;

            bool doGravel = DepositRand.NextFloat() > 0.33;

            for (int posx = minx; posx < maxx; posx++)
            {
                targetPos.X = posx;
                lx = targetPos.X - baseX;
                distx = posx - depoCenterPos.X;

                float xSq = distx * distx * xRadSqInv;

                for (int posz = minz; posz < maxz; posz++)
                {
                    targetPos.Z = posz;
                    lz = targetPos.Z - baseZ;
                    distz = posz - depoCenterPos.Z;

                    // Kinda weird mathematically speaking, but seems to work as a means to distort the perfect circleness of deposits ¯\_(ツ)_/¯
                    // Also not very efficient to use direct perlin noise in here :/
                    // But after ~10 hours of failing (=weird lines of missing deposit material) with a pre-generated 2d distortion map i give up >.>
                    val = 1 - DistortNoiseGen.Noise(targetPos.X / 3.0, targetPos.Z / 3.0) * 1.5 + 0.15;
                    double distanceToEdge = val - (xSq + distz * distz * zRadSqInv);

                    if (distanceToEdge < 0 || lx < 0 || lz < 0 || lx >= chunksize || lz >= chunksize) continue;

                    targetPos.Y = heremapchunk.WorldGenTerrainHeightMap[lz * chunksize + lx];

                    // Some deposits may not appear all over cliffs
                    if (Math.Abs(depoCenterPos.Y - targetPos.Y) > MaxYRoughness) continue;

                    int rockblockid = heremapchunk.TopRockIdMap[lz * chunksize + lx];

                    Block rockblock = blocktypes[rockblockid];
                    if (!rockblock.Variant.ContainsKey("rock")) continue;

                    Block alluvialblock;

                    if (doGravel)
                    {
                        alluvialblock = Api.World.GetBlock(new AssetLocation("gravel-" + rockblock.Variant["rock"]));
                    }
                    else
                    {
                        alluvialblock = Api.World.GetBlock(new AssetLocation("sand-" + rockblock.Variant["rock"]));
                    }
                    

                    for (int y = 0; y < depoitThickness; y++)
                    {
                        if (targetPos.Y <= 1 || targetPos.Y >= worldheight) continue;

                        int index3d = ((targetPos.Y % chunksize) * chunksize + lz) * chunksize + lx;
                        int blockId = chunks[targetPos.Y / chunksize].Blocks[index3d];

                        Block block = blocktypes[blockId];

                        if (block.BlockMaterial != EnumBlockMaterial.Soil) continue;

                        if (alluvialblock != null)
                        {
                            chunks[targetPos.Y / chunksize].Blocks[index3d] = alluvialblock.BlockId;
                        }

                        targetPos.Y--;
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
            return new int[0];
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
