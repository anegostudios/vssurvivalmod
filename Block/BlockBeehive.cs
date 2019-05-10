using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockBeehive : Block
    {

        public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
        {
            base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);

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

        public override bool TryPlaceBlockForWorldGen(IBlockAccessor blockAccessor, BlockPos pos, BlockFacing onBlockFace, Random worldgenRand)
        {
            //blockAccessor.SetBlock(blockAccessor.GetBlock(new AssetLocation("creativeblock-60")).BlockId, pos);
            
            for (int i = 1; i < 4; i++)
            {
                Block aboveBlock = blockAccessor.GetBlock(pos.X, pos.Y - i, pos.Z);
                
                if  (
                    (aboveBlock.BlockMaterial == EnumBlockMaterial.Wood || aboveBlock.BlockMaterial == EnumBlockMaterial.Plant) && aboveBlock.SideSolid[BlockFacing.DOWN.Index]
                )
                {
                    BlockPos atpos = new BlockPos(pos.X, pos.Y - i - 1, pos.Z);

                    Block block = blockAccessor.GetBlock(atpos);

                    if (
                        block.BlockMaterial == EnumBlockMaterial.Wood && 
                        aboveBlock.BlockMaterial == EnumBlockMaterial.Wood &&
                        blockAccessor.GetBlock(pos.X, pos.Y - i - 2, pos.Z).BlockMaterial == EnumBlockMaterial.Wood &&
                        aboveBlock.LastCodePart() == "ud"
                    )
                    {   
                        blockAccessor.SetBlock(blockAccessor.GetBlock(new AssetLocation("wildbeehive-inlog-" + aboveBlock.FirstCodePart(2))).BlockId, atpos);
                        if (EntityClass != null)
                        {
                            blockAccessor.SpawnBlockEntity(EntityClass, atpos);
                        }
                     
                        return true;
                    }

                    if (block.BlockMaterial != EnumBlockMaterial.Plant && block.BlockMaterial != EnumBlockMaterial.Air) continue;

                    int dx = pos.X % blockAccessor.ChunkSize;
                    int dz = pos.Z % blockAccessor.ChunkSize;
                    int surfacey = blockAccessor.GetMapChunkAtBlockPos(atpos).WorldGenTerrainHeightMap[dz * blockAccessor.ChunkSize + dx];
                   
                    if (pos.Y - surfacey < 4) return false;
                    
                    blockAccessor.SetBlock(BlockId, atpos);
                    if (EntityClass != null)
                    {
                        blockAccessor.SpawnBlockEntity(EntityClass, atpos);
                    }
                    return true;
                }
            }

            return false;
        }
    }
}
