using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockPlant : Block
    {
        Block snowLayerBlock;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            if (api.Side == EnumAppSide.Client)
            {
                snowLayerBlock = api.World.GetBlock(new AssetLocation("snowlayer-1"));
                tallGrassBlock = api.World.GetBlock(new AssetLocation("tallgrass-tall-free"));
            }

            WaveFlagMinY = 0.5f;
        }

        public override void OnJsonTesselation(ref MeshData sourceMesh, ref int[] lightRgbsByCorner, BlockPos pos, int[] chunkExtIds, ushort[] chunkLightExt, int extIndex3d)
        {
            int sunLightLevel = chunkLightExt[extIndex3d] & 31;
            bool waveOff = sunLightLevel < 14;

            if (VertexFlags.GrassWindWave)
            {
                setLeaveWaveFlags(sourceMesh, waveOff);
            }
        }


        void setLeaveWaveFlags(MeshData sourceMesh, bool off)
        {
            int grassWave = VertexFlags.All;
            int clearFlags = (~VertexFlags.LeavesWindWaveBitMask) & (~VertexFlags.FoliageWindWaveBitMask) & (~VertexFlags.GroundDistanceBitMask);

            // Iterate over each element face
            for (int vertexNum = 0; vertexNum < sourceMesh.GetVerticesCount(); vertexNum++)
            {
                float y = sourceMesh.xyz[vertexNum * 3 + 1];

                bool notwaving = off || y < 0.5;

                sourceMesh.Flags[vertexNum] &= clearFlags;

                if (!notwaving)
                {
                    sourceMesh.Flags[vertexNum] |= grassWave;
                }
            }
        }


        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
        {
            if (CanPlantStay(world.BlockAccessor, blockSel.Position))
            {
                return base.TryPlaceBlock(world, byPlayer, itemstack, blockSel, ref failureCode);
            }

            failureCode = "requirefertileground";

            return false;
        }

        
        public override void OnNeighbourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos)
        {
            if (!CanPlantStay(world.BlockAccessor, pos))
            {
                world.BlockAccessor.BreakBlock(pos, null);
                //world.BlockAccessor.TriggerNeighbourBlockUpdate(pos); - Why is this here. BreakBlock already updates neighbours
            }
        }

        public virtual bool CanPlantStay(IBlockAccessor blockAccessor, BlockPos pos)
        {
            Block block = blockAccessor.GetBlock(pos.X, pos.Y - 1, pos.Z);
            return block.Fertility > 0;
        }


        public override bool TryPlaceBlockForWorldGen(IBlockAccessor blockAccessor, BlockPos pos, BlockFacing onBlockFace, LCGRandom worldGenRand)
        {
            if (!CanPlantStay(blockAccessor, pos)) return false;
            return base.TryPlaceBlockForWorldGen(blockAccessor, pos, onBlockFace, worldGenRand);
        }

        Block tallGrassBlock;

        public override int GetRandomColor(ICoreClientAPI capi, BlockPos pos, BlockFacing facing)
        {
            if (snowLevel > 0) return snowLayerBlock.GetRandomColor(capi, pos, facing);

            int color = base.GetRandomColor(capi, pos, facing);

            if (EntityClass == "Sapling")
            {
                color = capi.World.ApplyColorMapOnRgba(ClimateColorMap, SeasonColorMap, color, pos.X, pos.Y, pos.Z);
            }
            if (Code.Path == "flower-lilyofthevalley-free")
            {
                color = tallGrassBlock.GetColor(capi, pos);
                color = capi.World.ApplyColorMapOnRgba("climatePlantTint", "seasonalGrass", color, pos.X, pos.Y, pos.Z);
            }

            return color;
        }

        public override int GetColor(ICoreClientAPI capi, BlockPos pos)
        {
            if (snowLevel > 0)
            {
                return snowLayerBlock.GetColor(capi, pos);
            }
            if (Code.Path == "flower-lilyofthevalley-free")
            {
                return capi.World.ApplyColorMapOnRgba("climatePlantTint", "seasonalGrass", tallGrassBlock.GetColor(capi, pos), pos.X, pos.Y, pos.Z);
            }

            return base.GetColor(capi, pos);
        }

        

    }
}
