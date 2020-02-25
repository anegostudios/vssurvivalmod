using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockPlant : Block
    {
        public override void OnJsonTesselation(ref MeshData sourceMesh, BlockPos pos, int[] chunkExtIds, ushort[] chunkLightExt, int extIndex3d)
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
            int grassWave = VertexFlags.FoliageWindWaveBitMask;
            int clearFlags = (~VertexFlags.FoliageWindWaveBitMask) & (~VertexFlags.GroundDistanceBitMask);

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

        
        public override void OnNeighourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos)
        {
            if (!CanPlantStay(world.BlockAccessor, pos))
            {
                world.BlockAccessor.BreakBlock(pos, null);
                world.BlockAccessor.TriggerNeighbourBlockUpdate(pos);
            }
        }

        internal virtual bool CanPlantStay(IBlockAccessor blockAccessor, BlockPos pos)
        {
            Block block = blockAccessor.GetBlock(pos.X, pos.Y - 1, pos.Z);
            return block.Fertility > 0;
        }


        public override bool TryPlaceBlockForWorldGen(IBlockAccessor blockAccessor, BlockPos pos, BlockFacing onBlockFace, LCGRandom worldGenRand)
        {
            if (!CanPlantStay(blockAccessor, pos)) return false;
            return base.TryPlaceBlockForWorldGen(blockAccessor, pos, onBlockFace, worldGenRand);
        }

        public override int GetRandomColor(ICoreClientAPI capi, BlockPos pos, BlockFacing facing)
        {
            int color = base.GetRandomColor(capi, pos, facing);

            if (EntityClass == "Sapling")
            {
                color = capi.ApplyColorTintOnRgba(1, color, pos.X, pos.Y, pos.Z);
            }

            return color;
        }
        
    }
}
