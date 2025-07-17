using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent
{
    public class BlockHangingLichen : Block
    {

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
        }


        public override void OnJsonTesselation(ref MeshData sourceMesh, ref int[] lightRgbsByCorner, BlockPos pos, Block[] chunkExtBlocks, int extIndex3d)
        {
            if (VertexFlags.WindMode == EnumWindBitMode.NoWind) return;

            int sideDisableWindWave = 0;  // Any bit set to 1 means no Wave on that tileSide

            // Disable motion if top side touching a solid block
            BlockFacing facing = BlockFacing.ALLFACES[4];
            Block nblock = api.World.BlockAccessor.GetBlock(pos.AddCopy(facing));
            if (nblock.BlockMaterial != EnumBlockMaterial.Leaves && nblock.SideSolid[BlockFacing.ALLFACES[4].Opposite.Index]) sideDisableWindWave |= (1 << 4);

            int groundOffset = 0;

            bool enableWind = (lightRgbsByCorner[24] >> 24 & 0xff) >= 159;  //corresponds with a sunlight level of less than 14

            if (enableWind)
            {
                // We could invert the ground offset, have vines bend more the further they descend ...
                groundOffset = 1;
            }

            sourceMesh.ToggleWindModeSetWindData(sideDisableWindWave, enableWind, groundOffset);
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

            Block upBlock = blockAccessor.GetBlock(pos.UpCopy());
            if (upBlock is BlockHangingLichen)
            {
                //BlockFacing facing = ((BlockHangingLichen)upBlock).VineFacing;
                //Block block = blockAccessor.GetBlock(CodeWithParts(facing.Code));
                blockAccessor.SetBlock(BlockId, pos);
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

            BlockPos upPos = blockSel.Position.UpCopy();
            Block upBlock = world.BlockAccessor.GetBlock(upPos);
            if (upBlock is BlockHangingLichen || upBlock.CanAttachBlockAt(world.BlockAccessor, this, upPos, BlockFacing.DOWN) || upBlock is BlockLeaves)
            {
                world.BlockAccessor.SetBlock(BlockId, blockSel.Position);
                return true;
            }

            failureCode = "requirelichenattachable";

            return false;
        }


        public override BlockDropItemStack[] GetDropsForHandbook(ItemStack handbookStack, IPlayer forPlayer)
        {
            return GetHandbookDropsFromBreakDrops(handbookStack, forPlayer);
        }

        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1f)
        {
            return System.Array.Empty<ItemStack>();
        }



        bool TryAttachTo(IBlockAccessor blockAccessor, BlockPos blockpos, BlockFacing onBlockFace)
        {
            if (!onBlockFace.IsVertical) return false;

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
            Block block = world.BlockAccessor.GetBlock(pos.UpCopy());

            return block is BlockLeaves || block is BlockHangingLichen || block.CanAttachBlockAt(world.BlockAccessor, this, pos.UpCopy(), BlockFacing.DOWN);
        }

    }
}
