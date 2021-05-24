using System;
using Vintagestory.API;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockFern : BlockPlant
    {
        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            WaveFlagMinY = 0.25f;
        }

        public override void OnJsonTesselation(ref MeshData sourceMesh, ref int[] lightRgbsByCorner, BlockPos pos, Block[] chunkExtBlocks, int extIndex3d)
        {
            base.OnJsonTesselation(ref sourceMesh, ref lightRgbsByCorner, pos, chunkExtBlocks, extIndex3d);

            for (int i = 0; i < sourceMesh.FlagsCount; i++)
            {
                sourceMesh.Flags[i] &= VertexFlags.clearNormalBits;
                sourceMesh.Flags[i] |= VertexFlags.NormalToPackedInt(0, 1, 0) << 15;
            }
        }
    }

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
                if (this.RandomDrawOffset > 0)
                {
                    JsonObject overrider = Attributes?["overrideRandomDrawOffset"];
                    if (overrider?.Exists == true) this.RandomDrawOffset = overrider.AsInt(1);
                }
            }

            WaveFlagMinY = 0.5f;
        }

        public override void OnJsonTesselation(ref MeshData sourceMesh, ref int[] lightRgbsByCorner, BlockPos pos, Block[] chunkExtBlocks, int extIndex3d)
        {
            if (VertexFlags.GrassWindWave)
            {
                bool waveOff = (byte)(lightRgbsByCorner[24] >> 24) < 159;  //corresponds with a sunlight level of less than 14

                setLeaveWaveFlags(sourceMesh, waveOff);
            }
        }


        void setLeaveWaveFlags(MeshData sourceMesh, bool off)
        {
            int grassWave = VertexFlags.All;
            int clearFlags = VertexFlags.clearWaveBits;
            int verticesCount = sourceMesh.VerticesCount;

            // Iterate over each element face
            for (int vertexNum = 0; vertexNum < verticesCount; vertexNum++)
            {
                int flag = sourceMesh.Flags[vertexNum] & clearFlags;

                if (!off && sourceMesh.xyz[vertexNum * 3 + 1] > 0.5)
                {
                    flag |= grassWave;
                }
                sourceMesh.Flags[vertexNum] = flag;
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


        public override string GetPlacedBlockName(IWorldAccessor world, BlockPos pos)
        {
            if (EntityClass != null)
            {
                BlockEntity be = world.BlockAccessor.GetBlockEntity(pos);
                if (be is BlockEntitySapling bes) return bes.GetBlockName();
            }

            return base.GetPlacedBlockName(world, pos);
        }

        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
        {
            if (EntityClass != null)
            {
                BlockEntity be = world.BlockAccessor.GetBlockEntity(pos);
                if (be is BlockEntitySapling bes) return bes.GetDrops();
            }

            return base.GetDrops(world, pos, byPlayer, dropQuantityMultiplier);
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

        public override Cuboidf[] GetSelectionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            // Return an unshifted selection box if looking at or breaking a snowy block, or a block with only small random drawoffset (e.g. berry bush)
            if (this.snowLevel > 1 || this.RandomDrawOffset > 7)
            {
                return SelectionBoxes;
            }

            return base.GetSelectionBoxes(blockAccessor, pos);
        }

    }
}