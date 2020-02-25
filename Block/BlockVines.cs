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

        bool[] leavesWaveTileSide = new bool[6];
        RoomRegistry roomreg;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            roomreg = api.ModLoader.GetModSystem<RoomRegistry>();
        }

        public override void OnJsonTesselation(ref MeshData sourceMesh, BlockPos pos, int[] chunkExtIds, ushort[] chunkLightExt, int extIndex3d)
        {
            if (VertexFlags.LeavesWindWave)
            {
                for (int tileSide = 0; tileSide < TileSideEnum.SideCount; tileSide++)
                {
                    int nBlockId = chunkExtIds[extIndex3d + TileSideEnum.MoveIndex[tileSide]];
                    Block nblock = api.World.Blocks[nBlockId];
                    leavesWaveTileSide[tileSide] = !nblock.SideSolid[BlockFacing.ALLFACES[tileSide].GetOpposite().Index] || nblock.BlockMaterial == EnumBlockMaterial.Leaves;
                }

                bool waveoff = false;
                int groundOffset = 0;

                waveoff = api.World.BlockAccessor.GetLightLevel(pos, EnumLightLevelType.OnlySunLight) < 14;

                if (!waveoff)
                {
                    groundOffset = 1;
                }

                setLeaveWaveFlags(sourceMesh, leavesWaveTileSide, waveoff, groundOffset);
            }
        }



        void setLeaveWaveFlags(MeshData sourceMesh, bool[] leavesWaveTileSide, bool off, int groundOffset)
        {
            int leaveWave = VertexFlags.LeavesWindWaveBitMask;
            int clearFlags = (~VertexFlags.LeavesWindWaveBitMask) & (~VertexFlags.GroundDistanceBitMask);

            // Iterate over each element face
            for (int vertexNum = 0; vertexNum < sourceMesh.GetVerticesCount(); vertexNum++)
            {
                float x = sourceMesh.xyz[vertexNum * 3 + 0];
                float y = sourceMesh.xyz[vertexNum * 3 + 1];
                float z = sourceMesh.xyz[vertexNum * 3 + 2];

                // Is there some pretty math formula for this? :<
                bool notwaving =
                    off ||
                    (y > 0.5 && !leavesWaveTileSide[BlockFacing.UP.Index]) ||
                    (y < 0.5 && !leavesWaveTileSide[BlockFacing.DOWN.Index]) ||
                    (z < 0.5 && !leavesWaveTileSide[BlockFacing.NORTH.Index]) ||
                    (z > 0.5 && !leavesWaveTileSide[BlockFacing.SOUTH.Index]) ||
                    (x > 0.5 && !leavesWaveTileSide[BlockFacing.EAST.Index]) ||
                    (x < 0.5 && !leavesWaveTileSide[BlockFacing.WEST.Index])
                ;

                sourceMesh.Flags[vertexNum] &= clearFlags;

                if (!notwaving)
                {
                    sourceMesh.Flags[vertexNum] |= leaveWave | (groundOffset << 28);
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
            Block block = world.BlockAccessor.GetBlock(CodeWithParts("north"));
            return new ItemStack[] { new ItemStack(block) };
        }

        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            Block block = world.BlockAccessor.GetBlock(CodeWithParts("north"));
            return new ItemStack(block);
        }



        bool TryAttachTo(IBlockAccessor blockAccessor, BlockPos blockpos, BlockFacing onBlockFace)
        {
            BlockPos attachingBlockPos = blockpos.AddCopy(onBlockFace.GetOpposite());
            Block block = blockAccessor.GetBlock(blockAccessor.GetBlockId(attachingBlockPos));

            if (block.CanAttachBlockAt(blockAccessor, this, attachingBlockPos, onBlockFace))
            {
                int blockId = blockAccessor.GetBlock(CodeWithParts(onBlockFace.Code)).BlockId;
                blockAccessor.SetBlock(blockId, blockpos);
                return true;
            }

            return false;
        }


        public override void OnNeighourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos)
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
            Block block = world.BlockAccessor.GetBlock(world.BlockAccessor.GetBlockId(pos.AddCopy(facing.GetOpposite())));

            return block.CanAttachBlockAt(world.BlockAccessor, this, pos, facing) || world.BlockAccessor.GetBlock(pos.UpCopy()) is BlockVines;
        }

        public override AssetLocation GetRotatedBlockCode(int angle)
        {
            BlockFacing newFacing = BlockFacing.HORIZONTALS_ANGLEORDER[(angle / 90 + BlockFacing.FromCode(LastCodePart()).HorizontalAngleIndex) % 4];
            return CodeWithParts(newFacing.Code);
        }
    }
}
