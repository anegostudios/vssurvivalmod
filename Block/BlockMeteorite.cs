using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockMeteorite : Block
    {
        BlockPos tmpPos = new BlockPos();

        public override bool TryPlaceBlockForWorldGen(IBlockAccessor blAcc, BlockPos pos, BlockFacing onBlockFace, Random worldgenRand)
        {
            int cnt = 2 + worldgenRand.Next(25);
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
                tmpPos.X += worldgenRand.Next(3) == 0 ? (worldgenRand.Next(3) - 1) : 0;
                tmpPos.Y += worldgenRand.Next(8) == 0 ? (worldgenRand.Next(3) - 1) : 0;
                tmpPos.Z += worldgenRand.Next(3) == 0 ? (worldgenRand.Next(3) - 1) : 0;

                blAcc.SetBlock(this.BlockId, tmpPos);
            }
            


            ushort sueviteBlockId = api.World.GetBlock(new AssetLocation("rock-suevite")).BlockId;
            ushort fragmentBlockId = api.World.GetBlock(new AssetLocation("loosestones-meteorite-iron")).BlockId;
            ushort looseSueviteBlockId = api.World.GetBlock(new AssetLocation("loosestones-suevite")).BlockId;

            float impactRockRadius = craterRadius * 1.2f;
            int range = (int)Math.Ceiling(impactRockRadius);
            int chunksize = api.World.BlockAccessor.ChunkSize;
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
                        ushort id = i == (int)q ? surfaceblock.BlockId : (ushort)0;
                        blAcc.SetBlock(id, tmpPos);
                    }

                    mapchunk.WorldGenTerrainHeightMap[(tmpPos.Z % chunksize) * chunksize + (tmpPos.X % chunksize)] -= (ushort)q;
                    
                    tmpPos.Y = blAcc.GetTerrainMapheightAt(tmpPos) + 1;
                    
                    blAcc.SetBlock(abovesurfaceblock.BlockId, tmpPos);
                }
            }

            int quantityFragments = 0;
            if (worldgenRand.Next(10) == 0) quantityFragments = worldgenRand.Next(10);
            else if (worldgenRand.Next(5) == 0) quantityFragments = worldgenRand.Next(5);
            
            while (quantityFragments-- > 0)
            {
                tmpPos.Set(
                    pos.X + (worldgenRand.Next(11) + worldgenRand.Next(11)) / 2 - 5,
                    0,
                    pos.Z + (worldgenRand.Next(11) + worldgenRand.Next(11)) / 2 - 5
                );
                tmpPos.Y = blAcc.GetTerrainMapheightAt(tmpPos) + 1;

                if (!blAcc.GetBlock(tmpPos.X, tmpPos.Y-1, tmpPos.Z).SideSolid[BlockFacing.UP.Index]) continue;

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
            return blAcc.GetBlock(x, y, z).SideSolid[BlockFacing.UP.Index];
        }
    }
}
