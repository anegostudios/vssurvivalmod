using System;
using Vintagestory.API.Client;
using Vintagestory.API.Client.Tesselation;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockVines : Block
    {
        public BlockFacing VineFacing;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            VineFacing = BlockFacing.FromCode(Variant["horizontalorientation"]);
        }

        int[] origWindMode;
        BlockPos tmpPos = new BlockPos();

        public override void OnJsonTesselation(ref MeshData sourceMesh, ref int[] lightRgbsByCorner, BlockPos pos, Block[] chunkExtBlocks, int extIndex3d)
        {
            if (origWindMode == null)
            {
                int cnt = sourceMesh.FlagsCount;
                origWindMode = (int[])sourceMesh.Flags.Clone();
                for (int i = 0; i < cnt; i++) origWindMode[i] &= VertexFlags.WindModeBitsMask;
            }

            int verticesCount = sourceMesh.VerticesCount;
            bool enableWind = (byte)(lightRgbsByCorner[24] >> 24) >= 159;  //corresponds with a sunlight level of less than 14

            // Are we fully attached? => No wave
            Block ablock = chunkExtBlocks[extIndex3d + TileSideEnum.MoveIndex[VineFacing.Opposite.Index]];
            if (!enableWind || (ablock.Id != 0 && ablock.CanAttachBlockAt(api.World.BlockAccessor, this, tmpPos.Set(pos).Add(VineFacing.Opposite), VineFacing)))
            {
                for (int i = 0; i < verticesCount; i++)
                {
                    sourceMesh.Flags[i] &= VertexFlags.ClearWindModeBitsMask;
                }
                return;
            }

            int windData =
                ((api.World.BlockAccessor.GetBlock(pos.X, pos.Y + 1, pos.Z) is BlockVines) ? 1 : 0)
                + ((api.World.BlockAccessor.GetBlock(pos.X, pos.Y + 2, pos.Z) is BlockVines) ? 1 : 0)
                + ((api.World.BlockAccessor.GetBlock(pos.X, pos.Y + 3, pos.Z) is BlockVines) ? 1 : 0)
            ;

            int windDatam1;
            
            if (windData == 3 && api.World.BlockAccessor.GetBlock(pos.X, pos.Y + 4, pos.Z) is BlockVines)
            {
                windDatam1 = windData << VertexFlags.WindDataBitsPos;
            } else
            {
                windDatam1 = (Math.Max(0, windData - 1) << VertexFlags.WindDataBitsPos);
            }

            windData = windData << VertexFlags.WindDataBitsPos;

            // Is there a vine above thats attached? => Wave for the bottom half
            Block ublock = chunkExtBlocks[extIndex3d + TileSideEnum.MoveIndex[BlockFacing.UP.Index]];
            if (ublock is BlockVines)
            {
                Block uablock = chunkExtBlocks[extIndex3d + TileSideEnum.MoveIndex[VineFacing.Opposite.Index] + TileSideEnum.MoveIndex[BlockFacing.UP.Index]];
                if (uablock.Id != 0 && uablock.CanAttachBlockAt(api.World.BlockAccessor, this, tmpPos.Set(pos).Up().Add(VineFacing.Opposite), VineFacing))
                {
                    for (int i = 0; i < verticesCount; i++)
                    {
                        float y = sourceMesh.xyz[i * 3 + 1];

                        if (y > 0.5)
                        {
                            sourceMesh.Flags[i] &= VertexFlags.ClearWindModeBitsMask;
                        } else
                        {
                            sourceMesh.Flags[i] = (sourceMesh.Flags[i] & VertexFlags.ClearWindBitsMask) | origWindMode[i] | windData;
                        }
                    }
                    return;
                }
            }

            // Otherwise all wave
            for (int i = 0; i < verticesCount; i++)
            {
                float y = sourceMesh.xyz[i * 3 + 1];

                if (y > 0.5)
                {
                    sourceMesh.Flags[i] = (sourceMesh.Flags[i] & VertexFlags.ClearWindBitsMask) | origWindMode[i] | windDatam1;
                }
                else
                {
                    sourceMesh.Flags[i] = (sourceMesh.Flags[i] & VertexFlags.ClearWindBitsMask) | origWindMode[i] | windData;
                }
            }
        }


        public override bool TryPlaceBlockForWorldGen(IBlockAccessor blockAccessor, BlockPos pos, BlockFacing onBlockFace, LCGRandom worldGenRand)
        {
            if (!blockAccessor.GetBlock(pos).IsReplacableBy(this))
            {
                return false;
            }

            if (onBlockFace.IsHorizontal)
            {
                if (TryAttachTo(blockAccessor, pos, onBlockFace)) return true;
            }

            Block upBlock = blockAccessor.GetBlock(pos.UpCopy());
            if (upBlock is BlockVines)
            {
                BlockFacing facing = ((BlockVines)upBlock).VineFacing;
                Block block = blockAccessor.GetBlock(CodeWithParts(facing.Code));
                blockAccessor.SetBlock(block == null ? upBlock.BlockId : block.BlockId, pos);
                return true;
            }

            // Try attach anywhere
            for (int i = 0; i < BlockFacing.HORIZONTALS.Length; i++)
            {
                if (TryAttachTo(blockAccessor, pos, BlockFacing.HORIZONTALS[i])) return true;
            }

            return false;
        }

        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
        {
            if (!CanPlaceBlock(world, byPlayer, blockSel, ref failureCode))
            {
                return false;
            }

            if (blockSel.Face.IsHorizontal)
            {
                if (TryAttachTo(world.BlockAccessor, blockSel.Position, blockSel.Face)) return true;
            }

            Block upBlock = world.BlockAccessor.GetBlock(blockSel.Position.UpCopy());
            if (upBlock is BlockVines)
            {
                BlockFacing facing = ((BlockVines)upBlock).VineFacing;
                Block block = world.BlockAccessor.GetBlock(CodeWithParts(facing.Code));
                world.BlockAccessor.SetBlock(block == null ? upBlock.BlockId : block.BlockId, blockSel.Position);
                return true;
            }

            failureCode = "requirevineattachable";

            return false;
        }


        public override BlockDropItemStack[] GetDropsForHandbook(ItemStack handbookStack, IPlayer forPlayer)
        {
            return GetHandbookDropsFromBreakDrops(handbookStack, forPlayer);
        }

        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1f)
        {
            // fix for tropical vines (e.g. wildvine-tropical-section-east) to drop regular vine block
            string[] parts = Code.Path.Split('-');
            Block block = world.BlockAccessor.GetBlock(new AssetLocation(parts[0] + "-" + parts[parts.Length - 2] + "-north"));
            return new ItemStack[] { new ItemStack(block) };
        }

        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            // fix for tropical vines (e.g. wildvine-tropical-section-east) to drop regular vine block
            string[] parts = Code.Path.Split('-');
            Block block = world.BlockAccessor.GetBlock(new AssetLocation(parts[0] + "-" + parts[parts.Length - 2] + "-north"));
            return new ItemStack(block);
        }



        bool TryAttachTo(IBlockAccessor blockAccessor, BlockPos blockpos, BlockFacing onBlockFace)
        {
            BlockPos attachingBlockPos = blockpos.AddCopy(onBlockFace.Opposite);
            Block block = blockAccessor.GetBlock(blockAccessor.GetBlockId(attachingBlockPos));

            if (block.CanAttachBlockAt(blockAccessor, this, attachingBlockPos, onBlockFace))
            {
                int blockId = blockAccessor.GetBlock(CodeWithParts(onBlockFace.Code)).BlockId;
                blockAccessor.SetBlock(blockId, blockpos);
                return true;
            }

            return false;
        }


        public override void OnNeighbourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos)
        {
            if (!CanVineStay(world, pos))
            {
                world.BlockAccessor.SetBlock(0, pos);
                world.BlockAccessor.TriggerNeighbourBlockUpdate(pos);
                return;
            }
        }

        bool CanVineStay(IWorldAccessor world, BlockPos pos)
        {
            BlockPos apos = pos.AddCopy(VineFacing.Opposite);
            Block block = world.BlockAccessor.GetBlock(world.BlockAccessor.GetBlockId(apos));

            return block.CanAttachBlockAt(world.BlockAccessor, this, apos, VineFacing) || world.BlockAccessor.GetBlock(pos.UpCopy()) is BlockVines;
        }

        public override AssetLocation GetRotatedBlockCode(int angle)
        {
            BlockFacing newFacing = BlockFacing.HORIZONTALS_ANGLEORDER[(angle / 90 + BlockFacing.FromCode(LastCodePart()).HorizontalAngleIndex) % 4];
            return CodeWithParts(newFacing.Code);
        }
    }
}
