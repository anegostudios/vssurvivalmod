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
        public BlockFacing GetOrientation()
        {
            string[] parts = Code.Path.Split('-');
            return BlockFacing.FromCode(parts[parts.Length - 1]);
        }

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
        }

        public override void OnJsonTesselation(ref MeshData sourceMesh, ref int[] lightRgbsByCorner, BlockPos pos, Block[] chunkExtBlocks, int extIndex3d)
        {
            if (VertexFlags.LeavesWindWave)
            {
                int leavesNoWaveTileSide = 0;  //any bit set to 1 means no Wave on that tileSide
                for (int tileSide = 0; tileSide < TileSideEnum.Down; tileSide++)  // VINES are free to move on the down side :)
                {
                    Block nblock = chunkExtBlocks[extIndex3d + TileSideEnum.MoveIndex[tileSide]];
                    if (!nblock.VertexFlags.LeavesWindWave && nblock.SideSolid[TileSideEnum.GetOpposite(tileSide)]) leavesNoWaveTileSide |= (1 << tileSide);
                }

                int groundOffset = 0;

                bool waveoff = (byte)(lightRgbsByCorner[24] >> 24) < 159;  //corresponds with a sunlight level of less than 14

                if (!waveoff)
                {
                    // We could invert the ground offset, have vines bend more the further they descend ...
                    groundOffset = 1;
                }

                BlockWithLeavesMotion.SetLeaveWaveFlags(sourceMesh, leavesNoWaveTileSide, waveoff, VertexFlags.LeavesWindWaveBitMask, groundOffset);
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
                BlockFacing facing = ((BlockVines)upBlock).GetOrientation();
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
                BlockFacing facing = ((BlockVines)upBlock).GetOrientation();
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
            BlockFacing facing = GetOrientation();
            Block block = world.BlockAccessor.GetBlock(world.BlockAccessor.GetBlockId(pos.AddCopy(facing.Opposite)));

            return block.CanAttachBlockAt(world.BlockAccessor, this, pos, facing) || world.BlockAccessor.GetBlock(pos.UpCopy()) is BlockVines;
        }

        public override AssetLocation GetRotatedBlockCode(int angle)
        {
            BlockFacing newFacing = BlockFacing.HORIZONTALS_ANGLEORDER[(angle / 90 + BlockFacing.FromCode(LastCodePart()).HorizontalAngleIndex) % 4];
            return CodeWithParts(newFacing.Code);
        }
    }
}
