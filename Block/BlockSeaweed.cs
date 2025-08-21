using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent
{
    public class PlantAirParticles : ParticlesProviderBase
    {
        Random rand = new Random();
        public Vec3d BasePos = new Vec3d();
        public Vec3d AddPos = new Vec3d();

        public override bool DieInAir => false;
        public override bool DieInLiquid => false; public override float GravityEffect => 1f; public override float LifeLength => 2.25f; public override bool SwimOnLiquid => true;
        public override Vec3d Pos => new Vec3d(BasePos.X + rand.NextDouble() * AddPos.X, BasePos.Y + rand.NextDouble() * AddPos.Y, BasePos.Z + AddPos.Z * rand.NextDouble());

        public override float Quantity => 1;

        public override int GetRgbaColor(ICoreClientAPI capi)
        {
            return ColorUtil.HsvToRgba(110, 40 + rand.Next(50), 200 + rand.Next(30), 50 + rand.Next(40));
        }

        public override float Size => 0.07f;

        public override EvolvingNatFloat SizeEvolve => new EvolvingNatFloat(EnumTransformFunction.LINEAR, 0.2f);

        public override EvolvingNatFloat OpacityEvolve => new EvolvingNatFloat(EnumTransformFunction.QUADRATIC, -6);

        public override Vec3f GetVelocity(Vec3d pos)
        {
            return new Vec3f(((float)rand.NextDouble() - 0.5f)/4f, (float)(rand.NextDouble() + 1f) / 10f , ((float)rand.NextDouble() - 0.5f)/4f);
        }
    }


    public class BlockSeaweed : BlockWaterPlant
    {
        public override string RemapToLiquidsLayer { get { return "water-still-7"; } }

        protected Block[] blocks;

        PlantAirParticles splashParticleProps = new PlantAirParticles();

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            blocks = new Block[]
            {
                api.World.BlockAccessor.GetBlock(CodeWithParts("section")),
                api.World.BlockAccessor.GetBlock(CodeWithParts("top")),
            };
        }

        public override bool CanPlantStay(IBlockAccessor blockAccessor, BlockPos pos)
        {
            Block blockBelow = blockAccessor.GetBlockBelow(pos, 1, BlockLayersAccess.Solid);
            return (blockBelow.Fertility > 0) || (blockBelow is BlockSeaweed && blockBelow.Variant["part"] == "section");
        }

        public override void OnAsyncClientParticleTick(IAsyncParticleManager manager, BlockPos pos, float windAffectednessAtPos, float secondsTicking)
        {
            if (api.World.Rand.NextDouble() < 0.0025)
            {
                splashParticleProps.BasePos.Set(pos.X + 0.33f, pos.Y, pos.Z + 0.33f);
                splashParticleProps.AddPos.Set(0.33f,1,0.33f);
                manager.Spawn(splashParticleProps);
            }
        }

        public override bool ShouldReceiveClientParticleTicks(IWorldAccessor world, IPlayer player, BlockPos pos, out bool isWindAffected)
        {
            isWindAffected = false;
            return true;
        }

        public override void OnJsonTesselation(ref MeshData sourceMesh, ref int[] lightRgbsByCorner, BlockPos pos, Block[] chunkExtBlocks, int extIndex3d)
        {
            var blockAccessor = api.World.BlockAccessor;
            int windData =
                ((blockAccessor.GetBlockBelow(pos, 1, BlockLayersAccess.Solid) is BlockSeaweed) ? 1 : 0)
                + ((blockAccessor.GetBlockBelow(pos, 2, BlockLayersAccess.Solid) is BlockSeaweed) ? 1 : 0)
                + ((blockAccessor.GetBlockBelow(pos, 3, BlockLayersAccess.Solid) is BlockSeaweed) ? 1 : 0)
                + ((blockAccessor.GetBlockBelow(pos, 4, BlockLayersAccess.Solid) is BlockSeaweed) ? 1 : 0)
            ;

            var sourceMeshXyz = sourceMesh.xyz;
            var sourceMeshFlags = sourceMesh.Flags;
            var sourceFlagsCount = sourceMesh.FlagsCount;
            for (int i = 0; i < sourceFlagsCount; i++)
            {
                float y = sourceMeshXyz[i * 3 + 1];
                VertexFlags.ReplaceWindData(ref sourceMeshFlags[i], windData + (y > 0 ? 1 : 0));
            }
        }

        public override bool TryPlaceBlockForWorldGenUnderwater(IBlockAccessor blockAccessor, BlockPos pos, BlockFacing onBlockFace, IRandom worldGenRand, int minWaterDepth, int maxWaterDepth, BlockPatchAttributes attributes = null)
        {
            var height = attributes?.Height ?? NatFloat.createGauss(3, 3);

            BlockPos belowPos = pos.DownCopy();

            Block block;

            int depth = 1;
            while (depth < maxWaterDepth)
            {
                belowPos.Down();
                block = blockAccessor.GetBlock(belowPos);
                if (block is BlockWaterPlant) return false;
                if (block.Fertility > 0)
                {
                    PlaceSeaweed(blockAccessor, belowPos, depth, worldGenRand, height);
                    return true;
                }
                if (!block.IsLiquid()) return false;   // Prevent placing seaweed over seaweed (for example might result on a 3-deep plant placed on top of a 5-deep plant's existing position, giving a plant with 2 tops at positions 3 and 5)

                depth++;
            }

            return false;

        }


        internal void PlaceSeaweed(IBlockAccessor blockAccessor, BlockPos pos, int depth, IRandom random, NatFloat heightNatFloat)
        {
            var height = Math.Min(depth, (int)heightNatFloat.nextFloat(1f, random));
            while (height-- > 1)
            {
                pos.Up();
                blockAccessor.SetBlock(blocks[0].BlockId, pos);   // section
            }
            pos.Up();

            if (blocks[1] == null)
            {
                // spawn section if there is no top, (seegrass)
                blockAccessor.SetBlock(blocks[0].BlockId, pos);   // top
            }
            else
            {
                blockAccessor.SetBlock(blocks[1].BlockId, pos);   // top
            }
        }
    }
}
