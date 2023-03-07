using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockLooseRock : BlockRequireSolidGround
    {
        BlockPos tmpPos = new BlockPos();

        public override bool TryPlaceBlockForWorldGen(IBlockAccessor blockAccessor, BlockPos pos, BlockFacing onBlockFace, LCGRandom worldGenRand)
        {
            if (pos.Y < api.World.SeaLevel)
            {
                // Cave gen
                int q = 3 + worldGenRand.NextInt(6); 
                for (int i = 0; i < q; i++)
                {
                    tmpPos.Set(pos.X + worldGenRand.NextInt(7) - 3, pos.Y, pos.Z + worldGenRand.NextInt(7) - 3);

                    tryPlace(blockAccessor, tmpPos, worldGenRand);
                }
            }

            return tryPlace(blockAccessor, pos, worldGenRand);
        }

        private bool tryPlace(IBlockAccessor blockAccessor, BlockPos pos, LCGRandom worldGenRand)
        {
            for (int i = 0; i < 3; i++)
            {
                tmpPos.Set(pos.X, pos.Y - 1 - i, pos.Z);
                Block belowBlock = blockAccessor.GetBlock(tmpPos);
                if (belowBlock.BlockMaterial != EnumBlockMaterial.Ice && belowBlock.BlockMaterial != EnumBlockMaterial.Snow && belowBlock.CanAttachBlockAt(blockAccessor, this, tmpPos, BlockFacing.UP))
                {
                    tmpPos.Y++;
                    Block atBlock = blockAccessor.GetBlock(tmpPos);
                    if (atBlock.Replaceable < 6000) continue;

                    Block placeblock = this;
                    if (pos.Y < api.World.SeaLevel)
                    {
                        if (belowBlock.Variant["rock"] == null) return false;
                        placeblock = api.World.GetBlock(CodeWithVariant("rock", belowBlock.Variant["rock"]));
                        if (placeblock == null) return false;
                    }

                    generate(blockAccessor, placeblock, tmpPos, worldGenRand);

                    return true;
                }
            }

            return false;
        }

        protected virtual void generate(IBlockAccessor blockAccessor, Block block, BlockPos pos, LCGRandom worldGenRand)
        {
            blockAccessor.SetBlock(block.Id, pos);
        }

        public override int GetColor(ICoreClientAPI capi, BlockPos pos)
        {
            BlockPos down = pos.DownCopy();
            return capi.World.BlockAccessor.GetBlock(down).GetColor(capi, down);
        }

        public override int GetRandomColor(ICoreClientAPI capi, BlockPos pos, BlockFacing facing, int rndIndex = -1)
        {
            BlockPos down = pos.DownCopy();
            return capi.World.BlockAccessor.GetBlock(down).GetRandomColor(capi, down, facing, rndIndex);
        }
    }




    public class BlockRequireSolidGround : Block
    {
        public override bool TryPlaceBlockForWorldGen(IBlockAccessor blockAccessor, BlockPos pos, BlockFacing onBlockFace, LCGRandom worldGenRand)
        {
            if (HasSolidGround(blockAccessor, pos))
            {
                return base.TryPlaceBlockForWorldGen(blockAccessor, pos, onBlockFace, worldGenRand);
            }

            return false;
        }

        internal virtual bool HasSolidGround(IBlockAccessor blockAccessor, BlockPos pos)
        {
            Block block = blockAccessor.GetBlock(pos.DownCopy());
            return block.CanAttachBlockAt(blockAccessor, this, pos, BlockFacing.UP);
        }
    }
}
