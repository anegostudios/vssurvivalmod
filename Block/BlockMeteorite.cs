using System;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockMeteorite : Block
    {
        BlockPos tmpPos = new BlockPos();

        public override bool TryPlaceBlockForWorldGen(IBlockAccessor blAcc, BlockPos pos, BlockFacing onBlockFace, IRandom worldgenRand, BlockPatchAttributes attributes = null)
        {
            int cnt = 2 + worldgenRand.NextInt(25);
            float depth = GameMath.Sqrt(GameMath.Sqrt(cnt));
            float craterRadius = GameMath.Sqrt(cnt) * 1.25f;

            // Look for even or downwards curved ground
            if (pos.Y > 250 ||
                !IsSolid(blAcc, pos.X, pos.Y - 3, pos.Z) ||
                !IsSolid(blAcc, pos.X, pos.Y - (int)depth, pos.Z) ||
                !IsSolid(blAcc, pos.X + (int)craterRadius, pos.Y - 1, pos.Z) ||
                !IsSolid(blAcc, pos.X - (int)craterRadius, pos.Y - 1, pos.Z) ||
                !IsSolid(blAcc, pos.X, pos.Y - 1, pos.Z - (int)craterRadius) ||
                !IsSolid(blAcc, pos.X, pos.Y - 1, pos.Z + (int)craterRadius)
            )
            {
                return false;
            }

            int y1 = blAcc.GetTerrainMapheightAt(tmpPos.Set(pos.X - 5, pos.Y, pos.Z));
            int y2 = blAcc.GetTerrainMapheightAt(tmpPos.Set(pos.X + 5, pos.Y, pos.Z));
            int y3 = blAcc.GetTerrainMapheightAt(tmpPos.Set(pos.X - 0, pos.Y, pos.Z + 5));
            int y4 = blAcc.GetTerrainMapheightAt(tmpPos.Set(pos.X - 0, pos.Y, pos.Z - 5));

            if ((GameMath.Max(y1, y2, y3, y4) - GameMath.Min(y1, y2, y3, y4)) > 4) return false;



            tmpPos = tmpPos.Set(pos.X, pos.Y - (int)depth-2, pos.Z);
            while (cnt-- > 0)
            {
                tmpPos.X += worldgenRand.NextInt(3) == 0 ? (worldgenRand.NextInt(3) - 1) : 0;
                tmpPos.Y += worldgenRand.NextInt(8) == 0 ? (worldgenRand.NextInt(3) - 1) : 0;
                tmpPos.Z += worldgenRand.NextInt(3) == 0 ? (worldgenRand.NextInt(3) - 1) : 0;

                blAcc.SetBlock(this.BlockId, tmpPos);
            }



            int sueviteBlockId = api.World.GetBlock(new AssetLocation("rock-suevite")).BlockId;
            int fragmentBlockId = api.World.GetBlock(new AssetLocation("loosestones-meteorite-iron-free")).BlockId;
            int looseSueviteBlockId = api.World.GetBlock(new AssetLocation("loosestones-suevite-free")).BlockId;

            float impactRockRadius = craterRadius * 1.2f;
            int range = (int)Math.Ceiling(impactRockRadius);
            const int chunksize = GlobalConstants.ChunkSize;
            Vec2i vecTmp = new Vec2i();

            // 1. Generate a basin of suevite and lower terrain
            for (int dx = -range; dx <= range; dx++)
            {
                for (int dz = -range; dz <= range; dz++)
                {
                    float distImpactRockEdge = (dx * dx + dz * dz) / (impactRockRadius * impactRockRadius);
                    if (distImpactRockEdge > 1) continue;

                    tmpPos.X = pos.X + dx;
                    tmpPos.Z = pos.Z + dz;
                    int surfaceY = blAcc.GetTerrainMapheightAt(tmpPos);
                    tmpPos.Y = surfaceY - (int)depth;

                    vecTmp.X = tmpPos.X / chunksize;
                    vecTmp.Y = tmpPos.Z / chunksize;
                    IMapChunk mapchunk = blAcc.GetMapChunk(vecTmp);


                    float q = 3 * Math.Max(0, 2 * (1-distImpactRockEdge) - 0.2f);
                    tmpPos.Y -= (int)q+1;


                    while (q > 0)
                    {
                        if (q < 1 && worldgenRand.NextDouble() > q) break;

                        Block block = blAcc.GetBlock(tmpPos);

                        if (block != this && block.BlockMaterial == EnumBlockMaterial.Stone)
                        {
                            blAcc.SetBlock(sueviteBlockId, tmpPos);
                        }
                        q--;
                        tmpPos.Y++;
                    }

                    float distToCraterEdge = (dx * dx + dz * dz) / (craterRadius * craterRadius) + (float)worldgenRand.NextDouble() * 0.1f;
                    if (distToCraterEdge > 1) continue;

                    q = depth * (1 - distToCraterEdge);

                    tmpPos.Y = surfaceY;
                    Block surfaceblock = blAcc.GetBlock(tmpPos);
                    Block abovesurfaceblock = blAcc.GetBlock(tmpPos.X, tmpPos.Y + 1, tmpPos.Z);

                    for (int i = -1; i <= (int)q; i++)
                    {
                        tmpPos.Y = surfaceY - i;
                        int id = i == (int)q ? surfaceblock.BlockId : 0;

                        Block bblock = blAcc.GetBlock(tmpPos, BlockLayersAccess.Fluid);
                        if (!bblock.IsLiquid())
                        {
                            blAcc.SetBlock(id, tmpPos);
                        }
                    }

                    mapchunk.WorldGenTerrainHeightMap[(tmpPos.Z % chunksize) * chunksize + (tmpPos.X % chunksize)] -= (ushort)q;

                    tmpPos.Y = blAcc.GetTerrainMapheightAt(tmpPos) + 1;

                    if (abovesurfaceblock.BlockId > 0)
                    {
                        blAcc.SetBlock(abovesurfaceblock.BlockId, tmpPos);
                        if (abovesurfaceblock.EntityClass != null) blAcc.SpawnBlockEntity(abovesurfaceblock.EntityClass, tmpPos);   // Restore its blockEntity if appropriate
                    }
                }
            }

            int quantityFragments = 0;
            if (worldgenRand.NextInt(10) == 0) quantityFragments = worldgenRand.NextInt(10);
            else if (worldgenRand.NextInt(5) == 0) quantityFragments = worldgenRand.NextInt(5);

            while (quantityFragments-- > 0)
            {
                tmpPos.Set(
                    pos.X + (worldgenRand.NextInt(11) + worldgenRand.NextInt(11)) / 2 - 5,
                    0,
                    pos.Z + (worldgenRand.NextInt(11) + worldgenRand.NextInt(11)) / 2 - 5
                );
                tmpPos.Y = blAcc.GetTerrainMapheightAt(tmpPos) + 1;

                if (!blAcc.IsSideSolid(tmpPos.X, tmpPos.Y-1, tmpPos.Z, BlockFacing.UP)) continue;

                if (worldgenRand.NextDouble() < 0.3)
                {
                    blAcc.SetBlock(fragmentBlockId, tmpPos);
                } else
                {
                    blAcc.SetBlock(looseSueviteBlockId, tmpPos);
                }
            }

            //blAcc.SetBlock(61, pos.AddCopy(0, 20, 0));


            return true;
        }

        private bool IsSolid(IBlockAccessor blAcc, int x, int y, int z)
        {
            return blAcc.IsSideSolid(x, y, z, BlockFacing.UP);
        }
    }
}
