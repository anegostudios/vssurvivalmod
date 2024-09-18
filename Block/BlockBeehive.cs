using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockBeehive : Block
    {

        public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
        {
            base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);

            if (world.Side == EnumAppSide.Client) return;   // Only spawn the entity on the server side

            EntityProperties type = world.GetEntityType(new AssetLocation("beemob"));
            Entity entity = world.ClassRegistry.CreateEntity(type);

            if (entity != null)
            {
                entity.ServerPos.X = pos.X + 0.5f;
                entity.ServerPos.Y = pos.Y + 0.5f;
                entity.ServerPos.Z = pos.Z + 0.5f;
                entity.ServerPos.Yaw = (float)world.Rand.NextDouble() * 2 * GameMath.PI;
                entity.Pos.SetFrom(entity.ServerPos);

                entity.Attributes.SetString("origin", "brokenbeehive");
                world.SpawnEntity(entity);
            }
        }

        BlockPos atPos = new BlockPos();

        Cuboidf[] nocoll = new Cuboidf[0];

        public override Cuboidf[] GetParticleCollisionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            return nocoll;
        }

        public override bool TryPlaceBlockForWorldGen(IBlockAccessor blockAccessor, BlockPos pos, BlockFacing onBlockFace, IRandom worldgenRand, BlockPatchAttributes attributes = null)
        {
            //blockAccessor.SetBlock(blockAccessor.GetBlock(new AssetLocation("creativeblock-60")).BlockId, pos);

            for (int i = 2; i < 7; i++)
            {
                atPos.Set(pos.X, pos.Y - i, pos.Z);
                Block aboveBlock = blockAccessor.GetBlock(atPos);

                var abovemat = aboveBlock.GetBlockMaterial(blockAccessor, atPos);

                if ((abovemat == EnumBlockMaterial.Wood || abovemat == EnumBlockMaterial.Leaves) && aboveBlock.SideSolid[BlockFacing.DOWN.Index])
                {
                    atPos.Set(pos.X, pos.Y - i - 1, pos.Z);

                    Block block = blockAccessor.GetBlock(atPos);
                    var mat = block.GetBlockMaterial(blockAccessor, atPos);

                    BlockPos belowPos = atPos.DownCopy();

                    if (
                        mat == EnumBlockMaterial.Wood &&
                        abovemat == EnumBlockMaterial.Wood &&
                        blockAccessor.GetBlock(belowPos).GetBlockMaterial(blockAccessor, belowPos) == EnumBlockMaterial.Wood &&
                        block.Variant["rotation"] == "ud"
                    )
                    {
                        Block inlogblock = blockAccessor.GetBlock(new AssetLocation("wildbeehive-inlog-" + aboveBlock.Variant["wood"]));

                        blockAccessor.SetBlock(inlogblock.BlockId, atPos);
                        if (EntityClass != null)
                        {
                            blockAccessor.SpawnBlockEntity(EntityClass, atPos);
                        }

                        return true;
                    }

                    if (mat != EnumBlockMaterial.Leaves && mat != EnumBlockMaterial.Air) continue;

                    int dx = pos.X % GlobalConstants.ChunkSize;
                    int dz = pos.Z % GlobalConstants.ChunkSize;
                    int surfacey = blockAccessor.GetMapChunkAtBlockPos(atPos).WorldGenTerrainHeightMap[dz * GlobalConstants.ChunkSize + dx];

                    if (pos.Y - surfacey < 4) return false;

                    blockAccessor.SetBlock(BlockId, atPos);
                    if (EntityClass != null)
                    {
                        blockAccessor.SpawnBlockEntity(EntityClass, atPos);
                    }

                    //BlockPos test = pos.Copy();
                    //test.Y = 160;
                    //blockAccessor.SetBlock(blockAccessor.GetBlock(new AssetLocation("creativeblock-60")).BlockId, test);

                    return true;
                }
            }

            return false;
        }
    }
}
