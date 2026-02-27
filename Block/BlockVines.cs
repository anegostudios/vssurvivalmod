using System;
using Vintagestory.API.Client;
using Vintagestory.API.Client.Tesselation;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

#nullable disable

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
        BlockPos tmpPos = new BlockPos(API.Config.Dimensions.WillSetLater);

        public override void OnDecalTesselation(IWorldAccessor world, MeshData decalMesh, BlockPos pos)
        {
            int verticesCount = decalMesh.VerticesCount;
            var ba = api.World.BlockAccessor;

            // Are we fully attached? => No wave
            Block ablock = ba.GetBlockOnSide(pos, VineFacing.Opposite);
            if (ablock.Id != 0 && ablock.CanAttachBlockAt(ba, this, tmpPos.Set(pos, pos.dimension).Add(VineFacing.Opposite), VineFacing) && !(ablock is BlockLeaves))
            {
                for (int i = 0; i < verticesCount; i++)
                {
                    decalMesh.Flags[i] &= VertexFlags.ClearWindModeBitsMask;
                }
                return;
            }

            int windData =
                ((ba.GetBlockAbove(pos, 1, BlockLayersAccess.Solid) is BlockVines) ? 1 : 0)
                + ((ba.GetBlockAbove(pos, 2, BlockLayersAccess.Solid) is BlockVines) ? 1 : 0)
                + ((ba.GetBlockAbove(pos, 3, BlockLayersAccess.Solid) is BlockVines) ? 1 : 0)
            ;

            int windDatam1;

            if (windData == 3 && ba.GetBlockAbove(pos, 4, BlockLayersAccess.Solid) is BlockVines)
            {
                windDatam1 = windData << VertexFlags.WindDataBitsPos;
            }
            else
            {
                windDatam1 = Math.Max(0, windData - 1) << VertexFlags.WindDataBitsPos;
            }

            windData = windData << VertexFlags.WindDataBitsPos;

            // Is there a vine above thats attached? => Wave for the bottom half
            Block ublock = ba.GetBlockAbove(pos, 1, BlockLayersAccess.Solid);
            if (ublock is BlockVines)
            {
                tmpPos.Set(pos, pos.dimension).Up().Add(VineFacing.Opposite);
                Block uablock = ba.GetBlock(tmpPos);
                if (uablock.Id != 0 && uablock.CanAttachBlockAt(ba, this, tmpPos, VineFacing) && !(ablock is BlockLeaves))
                {
                    for (int i = 0; i < verticesCount; i++)
                    {
                        float y = decalMesh.xyz[i * 3 + 1];

                        if (y > 0.5)
                        {
                            decalMesh.Flags[i] &= VertexFlags.ClearWindModeBitsMask;
                        }
                        else
                        {
                            decalMesh.Flags[i] = (decalMesh.Flags[i] & VertexFlags.ClearWindBitsMask) | origWindMode[i] | windData;
                        }
                    }
                    return;
                }
            }

            otherwiseAllWave(decalMesh, verticesCount, windData, windDatam1);
        }

        public override void OnJsonTesselation(ref MeshData sourceMesh, ref int[] lightRgbsByCorner, BlockPos pos, Block[] chunkExtBlocks, int extIndex3d)
        {
            if (origWindMode == null)
            {
                int cnt = sourceMesh.FlagsCount;
                origWindMode = (int[])sourceMesh.Flags.Clone();
                for (int i = 0; i < cnt; i++) origWindMode[i] &= VertexFlags.WindModeBitsMask;
            }

            int verticesCount = sourceMesh.VerticesCount;
            bool enableWind = (lightRgbsByCorner[24] >> 24 & 0xff) >= 159;  //corresponds with a sunlight level of less than 14

            // Are we fully attached? => No wave
            Block ablock = chunkExtBlocks[extIndex3d + TileSideEnum.MoveIndex[VineFacing.Opposite.Index]];
            if (!enableWind || (ablock.Id != 0 && ablock.CanAttachBlockAt(api.World.BlockAccessor, this, tmpPos.Set(pos, pos.dimension).Add(VineFacing.Opposite), VineFacing) && !(ablock is BlockLeaves)))
            {
                for (int i = 0; i < verticesCount; i++)
                {
                    sourceMesh.Flags[i] &= VertexFlags.ClearWindModeBitsMask;
                }
                return;
            }

            int windData =
                ((api.World.BlockAccessor.GetBlockAbove(pos, 1, BlockLayersAccess.Solid) is BlockVines) ? 1 : 0)
                + ((api.World.BlockAccessor.GetBlockAbove(pos, 2, BlockLayersAccess.Solid) is BlockVines) ? 1 : 0)
                + ((api.World.BlockAccessor.GetBlockAbove(pos, 3, BlockLayersAccess.Solid) is BlockVines) ? 1 : 0)
            ;

            int windDatam1;

            if (windData == 3 && api.World.BlockAccessor.GetBlockAbove(pos, 4, BlockLayersAccess.Solid) is BlockVines)
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
                if (uablock.Id != 0 && uablock.CanAttachBlockAt(api.World.BlockAccessor, this, tmpPos.Set(pos, pos.dimension).Up().Add(VineFacing.Opposite), VineFacing) && !(ablock is BlockLeaves))
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

            otherwiseAllWave(sourceMesh, verticesCount, windData, windDatam1);
        }


        private void otherwiseAllWave(MeshData decalMesh, int verticesCount, int windData, int windDatam1)
        {
            for (int i = 0; i < verticesCount; i++)
            {
                float y = decalMesh.xyz[i * 3 + 1];

                if (y > 0.5)
                {
                    decalMesh.Flags[i] = (decalMesh.Flags[i] & VertexFlags.ClearWindBitsMask) | origWindMode[i] | windDatam1;
                }
                else
                {
                    decalMesh.Flags[i] = (decalMesh.Flags[i] & VertexFlags.ClearWindBitsMask) | origWindMode[i] | windData;
                }
            }
        }


        public override bool TryPlaceBlockForWorldGen(IBlockAccessor blockAccessor, BlockPos pos, BlockFacing onBlockFace, IRandom worldGenRand, BlockPatchAttributes attributes = null)
        {
            if (!blockAccessor.GetBlock(pos).IsReplacableBy(this))
            {
                return false;
            }

            if (onBlockFace.IsHorizontal)
            {
                if (TryAttachTo(blockAccessor, pos, onBlockFace)) return true;
            }

            Block upBlock = blockAccessor.GetBlockAbove(pos, 1, BlockLayersAccess.Solid);
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

            Block upBlock = world.BlockAccessor.GetBlockAbove(blockSel.Position, 1, BlockLayersAccess.Solid);
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
            Block block = world.BlockAccessor.GetBlock(new AssetLocation(parts[0] + "-" + parts[parts.Length - 2].Replace("end", "section") + "-north"));
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
            Block block = blockAccessor.GetBlock(attachingBlockPos);

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
            Block block = world.BlockAccessor.GetBlock(apos);

            return block.CanAttachBlockAt(world.BlockAccessor, this, apos, VineFacing) || world.BlockAccessor.GetBlock(pos.UpCopy()) is BlockVines;
        }

        public override AssetLocation GetRotatedBlockCode(int angle)
        {
            var blockFacing = BlockFacing.FromCode(LastCodePart());
            var angleIndex = angle == 180 ? blockFacing.HorizontalAngleIndex : blockFacing.Opposite.HorizontalAngleIndex;
            var rotatedIndex = angleIndex + angle / 90;
            BlockFacing newFacing = BlockFacing.HORIZONTALS_ANGLEORDER[GameMath.Mod(rotatedIndex, 4)];
            
            return CodeWithParts(newFacing.Code);
        }


        public override bool ShouldReceiveServerGameTicks(IWorldAccessor world, BlockPos pos, Random offThreadRandom, out object extra)
        {
            extra = null;
            if (offThreadRandom.NextDouble() > 0.1) return false;

            var attachFace = VineFacing.Opposite;
            BlockPos npos = pos.AddCopy(attachFace);
            Block block = world.BlockAccessor.GetBlock(npos);
            if (block.CanAttachBlockAt(world.BlockAccessor, this, npos, VineFacing) || block is BlockLeaves) return false;

            npos.Set(pos);
            int i = 0;
            for (; i < 5; i++)
            {
                npos.Y++;
                var upblock = world.BlockAccessor.GetBlock(npos);

                if (upblock is BlockLeaves || upblock.CanAttachBlockAt(world.BlockAccessor, this, npos, BlockFacing.DOWN)) return false;

                if (upblock is BlockVines)
                {
                    var ablock = world.BlockAccessor.GetBlockOnSide(npos, attachFace);

                    if (ablock.CanAttachBlockAt(world.BlockAccessor, this, npos, VineFacing)) return false;
                }
                else
                {
                    break;
                }
            }

            return i < 5;
        }


        public override void OnServerGameTick(IWorldAccessor world, BlockPos pos, object extra = null)
        {
            world.BlockAccessor.SetBlock(0, pos);
        }

    }
}
