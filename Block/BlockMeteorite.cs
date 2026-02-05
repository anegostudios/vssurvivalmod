using System;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent
{
    public class BlockMeteorite : Block
    {
        BlockPos tmpPos = new BlockPos(API.Config.Dimensions.NormalWorld);    // Worldgen only runs in normal world, currently

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
                    Block surfaceblock = GetBlockAndBEdata(blAcc, tmpPos, out ItemStack surfaceBEStack, out TreeAttribute surfaceBETree);
                    tmpPos.Y++;
                    Block abovesurfaceblock = GetBlockAndBEdata(blAcc, tmpPos, out ItemStack aboveBEStack, out TreeAttribute aboveBETree);
                    tmpPos.Y++;
                    Block above2surfaceblock = GetBlockAndBEdata(blAcc, tmpPos, out ItemStack above2BEStack, out TreeAttribute above2BETree);

                    for (int i = -2; i <= (int)q; i++)
                    {
                        tmpPos.Y = surfaceY - i;
                        int id = i == (int)q ? surfaceblock.BlockId : 0;

                        Block bblock = blAcc.GetBlock(tmpPos, BlockLayersAccess.Fluid);
                        if (!bblock.IsLiquid())   // true for no fluid block, i.e. the normal case
                        {
                            blAcc.SetBlock(id, tmpPos);
                            if (id > 0) MaybeSpawnBlockEntity(surfaceblock, blAcc, tmpPos, surfaceBEStack, surfaceBETree);  // Restore its blockEntity if appropriate
                        }
                    }

                    mapchunk.WorldGenTerrainHeightMap[(tmpPos.Z % chunksize) * chunksize + (tmpPos.X % chunksize)] -= (ushort)q;

                    tmpPos.Y = blAcc.GetTerrainMapheightAt(tmpPos) + 1;

                    if (abovesurfaceblock.BlockId > 0)
                    {
                        blAcc.SetBlock(abovesurfaceblock.BlockId, tmpPos);
                        MaybeSpawnBlockEntity(abovesurfaceblock, blAcc, tmpPos, aboveBEStack, aboveBETree);
                    }

                    tmpPos.Y++;

                    if (above2surfaceblock.BlockId > 0)
                    {
                        blAcc.SetBlock(above2surfaceblock.BlockId, tmpPos);
                        MaybeSpawnBlockEntity(above2surfaceblock, blAcc, tmpPos, above2BEStack, above2BETree);
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

        private Block GetBlockAndBEdata(IBlockAccessor blAcc, BlockPos pos, out ItemStack BEStack, out TreeAttribute BETree)
        {
            BEStack = null;
            BETree = null;
            Block block = blAcc.GetBlock(pos, BlockLayersAccess.Solid);
            try
            {
                if (block.EntityClass != null)
                {
                    // Try to re-build a good ItemStack for this BlockEntity to be supplied to .SpawnBlockEntity below, to rebuild the exact same BE (necessary to avoid ? blocks for microblocks, for example)
                    // Priorities:
                    //   1. the be.stackForWorldgen - this is a BE not yet fully placed, being placed by a BlockRandomizer or similar (possibly even by another meteorite in the tiny chance two are close to each other)
                    //   2. recreate the tree - this works for a BE which has been placed by a schematic, for example a microblock in a ruin (even though not yet initialised because the chunk column is probably still generating if we are here)
                    //   3. worst case, try .OnPickBlock - depending on the block type, this works OK for some blocks, but for others this will probably not work if the chunk has not already generated (it probably hasn't been generated, but some chance the meteorite is displacing blocks in a neighbouring chunk, for example)
                    var be = blAcc.GetBlockEntity(pos);
                    if (be != null)
                    {
                        BEStack = be.stackForWorldgen;
                        if (BEStack == null)
                        {
                            BETree = new TreeAttribute();
                            be.ToTreeAttributes(BETree);
                        }
                    }
                    if (BEStack == null && BETree == null) BEStack = block.OnPickBlock(api.World, pos);
                }
            }
            catch   // Just in case modded BlockEntity are not expecting to be dealt with in this way
            {
                // May result in ? blocks after all, but that's better than crashing!
                BEStack = null;
                BETree = null;
            }
            return block;
        }

        private void MaybeSpawnBlockEntity(Block block, IBlockAccessor blAcc, BlockPos pos, ItemStack BEStack, TreeAttribute BETree)
        {
            if (block.EntityClass != null)
            {
                try
                {
                    blAcc.SpawnBlockEntity(block.EntityClass, pos, BEStack);
                    if (BETree != null)
                    {
                        BlockEntity be = blAcc.GetBlockEntity(pos);
                        BETree.SetInt("posy", pos.Y);
                        be?.FromTreeAttributes(BETree, api.World);
                    }
                }
                catch (Exception e)   // Just in case modded BlockEntity are not expecting to be dealt with in this way
                {
                    api.Logger.Error(e);
                }
            }
        }

        private bool IsSolid(IBlockAccessor blAcc, int x, int y, int z)
        {
            return blAcc.IsSideSolid(x, y, z, BlockFacing.UP);
        }
    }
}
