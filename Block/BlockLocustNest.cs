using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.ServerMods;

namespace Vintagestory.GameContent
{
    public class BlockLocustNest : Block
    {
        public Block[] DecoBlocksCeiling;
        public Block[] DecoBlocksFloor;


        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            DecoBlocksCeiling = new Block[]
            {
                api.World.GetBlock(new AssetLocation("locustnest-cage")),
                api.World.GetBlock(new AssetLocation("locustnest-metalspike-none-upsidedown")),
                api.World.GetBlock(new AssetLocation("locustnest-metalspike-none-upsidedown")),
                api.World.GetBlock(new AssetLocation("locustnest-metalspike-none-upsidedown")),
                api.World.GetBlock(new AssetLocation("locustnest-stalagmite-main1")),
                api.World.GetBlock(new AssetLocation("locustnest-stalagmite-small1")),
                api.World.GetBlock(new AssetLocation("locustnest-stalagmite-small2"))
            };

            DecoBlocksFloor = new Block[]
            {
                api.World.GetBlock(new AssetLocation("locustnest-metalspike-none")),
                api.World.GetBlock(new AssetLocation("locustnest-metalspike-none")),
                api.World.GetBlock(new AssetLocation("locustnest-metalspike-none")),
                api.World.GetBlock(new AssetLocation("locustnest-metalspike-tiny")),
                api.World.GetBlock(new AssetLocation("locustnest-metalspike-tiny")),
                api.World.GetBlock(new AssetLocation("locustnest-metalspike-tiny")),
                api.World.GetBlock(new AssetLocation("locustnest-metalspike-small")),
                api.World.GetBlock(new AssetLocation("locustnest-metalspike-small")),
                api.World.GetBlock(new AssetLocation("locustnest-metalspike-medium")),
                api.World.GetBlock(new AssetLocation("locustnest-metalspike-large"))
            };
        }

        public override void OnBlockPlaced(IWorldAccessor world, BlockPos blockPos, ItemStack byItemStack = null)
        {
            base.OnBlockPlaced(world, blockPos, byItemStack);
            //world.BlockAccessor.SetBlock(0, blockPos);

            //TryPlaceBlockForWorldGen(world.BlockAccessor, blockPos, BlockFacing.UP);
        }


        public override string GetHeldItemName(ItemStack itemStack)
        {
            if (itemStack?.Attributes?.GetBool("spawnOnlyAfterImport", false) == true)
            {
                return base.GetHeldItemName(itemStack) + " " + Lang.Get("(delayed spawn)");
            }

            return base.GetHeldItemName(itemStack);
        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

            if (inSlot.Itemstack?.Attributes?.GetBool("spawnOnlyAfterImport", false) == true)
            {
                dsc.AppendLine(Lang.Get("Spawns locust nests only after import/world generation"));
            }
        }


        public override float OnGettingBroken(IPlayer player, BlockSelection blockSel, ItemSlot itemslot, float remainingResistance, float dt, int counter)
        {
            BlockEntityLocustNest bel = api.World.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityLocustNest;
            bel?.OnBlockBreaking();

            return base.OnGettingBroken(player, blockSel, itemslot, remainingResistance, dt, counter);
        }

        public override bool TryPlaceBlockForWorldGen(IBlockAccessor blockAccessor, BlockPos pos, BlockFacing onBlockFace, LCGRandom worldGenRand)
        {
            if (blockAccessor.GetBlockId(pos.X, pos.Y, pos.Z) != 0) return false;

            int surfaceY = blockAccessor.GetTerrainMapheightAt(pos);
            if (surfaceY - pos.Y < 30 || pos.Y < 25) return false;

            BlockPos cavepos = getSemiLargeCavePos(blockAccessor, pos);
            if (cavepos == null) return false;

            int dy = 0;
            while (dy < 15 && !blockAccessor.IsSideSolid(cavepos.X, cavepos.Y + dy, cavepos.Z, BlockFacing.UP))
            {
                dy++;
            }
            if (dy >= 15) return false;
            blockAccessor.SetBlock(BlockId, cavepos.AddCopy(0,dy,0));
            if (EntityClass != null)
            {
                blockAccessor.SpawnBlockEntity(EntityClass, cavepos.AddCopy(0, dy, 0));
            }

            BlockPos tmppos = new BlockPos();
            int tries = 55 + worldGenRand.NextInt(55);
            while (tries-- > 0)
            {
                int offX = worldGenRand.NextInt(15) - 7;
                int offY = worldGenRand.NextInt(15) - 7;
                int offZ = worldGenRand.NextInt(15) - 7;

                if (worldGenRand.NextDouble() < 0.4)
                {
                    tryPlaceDecoUp(tmppos.Set(cavepos.X + offX, cavepos.Y + offY, cavepos.Z + offZ), blockAccessor, worldGenRand);
                } else
                {
                    tryPlaceDecoDown(tmppos.Set(cavepos.X + offX, cavepos.Y + offY, cavepos.Z + offZ), blockAccessor, worldGenRand);
                }

                
            }

            return true;
        }

        private void tryPlaceDecoDown(BlockPos blockPos, IBlockAccessor blockAccessor, LCGRandom worldGenRand)
        {
            if (blockAccessor.GetBlock(blockPos).Id != 0) return;

            int tries = 7;
            while (tries-- > 0)
            {
                blockPos.Y--;
                Block block = blockAccessor.GetBlock(blockPos);
                if (block.SideSolid[BlockFacing.UP.Index])
                {
                    blockPos.Y++;
                    blockAccessor.SetBlock(DecoBlocksFloor[worldGenRand.NextInt(DecoBlocksFloor.Length)].BlockId, blockPos);
                    return;
                }
            }
        }

        private void tryPlaceDecoUp(BlockPos blockPos, IBlockAccessor blockAccessor, LCGRandom worldgenRand)
        {
            if (blockAccessor.GetBlock(blockPos).Id != 0) return;

            int tries = 7;
            while (tries-- > 0)
            {
                blockPos.Y++;
                Block block = blockAccessor.GetBlock(blockPos);
                if (block.SideSolid[BlockFacing.DOWN.Index])
                {
                    blockPos.Y--;
                    Block placeblock = DecoBlocksCeiling[worldgenRand.NextInt(DecoBlocksCeiling.Length)];
                    blockAccessor.SetBlock(placeblock.BlockId, blockPos);
                    return;
                }
            }
        }


        private BlockPos getSemiLargeCavePos(IBlockAccessor blockAccessor, BlockPos pos)
        {
            BlockPos outpos = pos.Copy();

            int maxY = pos.Y;
            int minY = pos.Y;

            int minX = pos.X;
            int maxX = pos.X;

            int minZ = pos.Z;
            int maxZ = pos.Z;

            while (pos.Y - minY < 12 && blockAccessor.GetBlockId(pos.X, minY - 1, pos.Z) == 0)
            {
                minY--;
            }
            while (maxY - pos.Y < 12 && blockAccessor.GetBlockId(pos.X, maxY + 1, pos.Z) == 0)
            {
                maxY++;
            }

            outpos.Y = (maxY + minY) / 2;
            if (maxY - minY < 4 || maxY - minY >= 10) return null;

            while (pos.X - minX < 12 && blockAccessor.GetBlockId(minX - 1, pos.Y, pos.Z) == 0)
            {
                minX--;
            }
            while (maxX - pos.X < 12 && blockAccessor.GetBlockId(maxX + 1, pos.Y, pos.Z) == 0)
            {
                maxX++;
            }

            if (maxX - minX < 3) return null;
            outpos.X = (maxX + minX) / 2;

            while (pos.Z - minZ < 12 && blockAccessor.GetBlockId(pos.X, pos.Y, minZ - 1) == 0)
            {
                minZ--;
            }
            while (maxZ - pos.Z < 12 && blockAccessor.GetBlockId(pos.X, pos.Y, maxZ + 1) == 0)
            {
                maxZ++;
            }

            if (maxZ - minZ < 3) return null;
            outpos.Z = (maxZ + minZ) / 2;

            return outpos;
        }
    }
}
