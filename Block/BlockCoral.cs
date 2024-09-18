using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.Datastructures;

namespace Vintagestory.GameContent;

public class BlockCoral : BlockWaterPlant
{
    private Block saltwater;

    public override bool skipPlantCheck { get; set; } = true;

    public override void OnLoaded(ICoreAPI api)
    {
        base.OnLoaded(api);
        saltwater = api.World.BlockAccessor.GetBlock(new AssetLocation("saltwater-still-7"));
    }

    public override bool TryPlaceBlockForWorldGenUnderwater(IBlockAccessor blockAccessor, BlockPos pos, BlockFacing onBlockFace, IRandom worldGenRand,
        int minWaterDepth, int maxWaterDepth, BlockPatchAttributes attributes = null)
    {
        if(attributes == null) return false;
        var minStepsGrow = attributes.CoralMinSize ?? 800;
        var randomStepsGrow = attributes.CoralRandomSize ?? 400;
        var seaWeedChance = attributes.CoralPlantsChance ?? 0.03f;
        var replaceOtherPatches = attributes.CoralReplaceOtherPatches ?? 0.03f;

        var shape = new NaturalShape(worldGenRand);
        var grow = randomStepsGrow == 0 ? 0 : worldGenRand.NextInt(randomStepsGrow);
        shape.Grow(minStepsGrow + grow);

        Block block;
        // TODO use this for height?
        //blockAccessor.GetMapChunk(0,0).WorldGenTerrainHeightMap
        foreach (var tmpPos in shape.GetPositions(pos))
        {
            var depth = 1;
            while (depth < maxWaterDepth)
            {
                tmpPos.Down();
                block = blockAccessor.GetBlock(tmpPos);
                if (block is BlockCoral) break;
                if (block is BlockWaterPlant)
                {
                    var replacePlant = worldGenRand.NextFloat();
                    if (replacePlant < replaceOtherPatches)
                    {
                        do
                        {
                            blockAccessor.SetBlock(saltwater.BlockId, tmpPos);
                            tmpPos.Down();
                            block = blockAccessor.GetBlock(tmpPos);
                        } while (block is BlockWaterPlant);
                    }
                    else
                    {
                        break;
                    }

                } // prevent placing of other aquatic blocks on top of eachother

                if (!block.IsLiquid())
                {
                    if (depth >= minWaterDepth)
                    {
                        // spawn seaweed
                        if (attributes?.CoralPlants?.Count > 0)
                        {
                            var blockType = worldGenRand.NextFloat();
                            if (blockType <= seaWeedChance)
                            {
                                var coralBase = GetRandomBlock(worldGenRand, attributes.CoralBaseBlock);
                                blockAccessor.SetBlock(coralBase.BlockId, tmpPos);
                                if (depth + 1 >= minWaterDepth)
                                {
                                    SpawnSeaPlantWeighted(blockAccessor, worldGenRand, attributes, tmpPos, depth);
                                }

                                break;
                            }
                        }

                        PlaceCoral(blockAccessor, tmpPos, worldGenRand, depth, minWaterDepth, attributes);
                    }

                    break;
                }

                depth++;
            }
        }

        return true;
    }

    private static void SpawnSeaPlantWeighted(IBlockAccessor blockAccessor, IRandom worldGenRand, BlockPatchAttributes attributes, BlockPos tmpPos,
        int depth)
    {
        var totalWeight = attributes.CoralPlants.Sum(c => c.Value.Chance);
        var chancePlant = worldGenRand.NextFloat() * totalWeight;
        var chanceSum = 0f;
        foreach (var conf in attributes.CoralPlants.Values)
        {
            chanceSum += conf.Chance;
            if (chancePlant < chanceSum)
            {
                var nextInt = worldGenRand.NextInt(conf.Block.Length);
                var seaPlantBlock = conf.Block[nextInt];
                if (seaPlantBlock is BlockSeaweed swb)
                    swb.PlaceSeaweed(blockAccessor, tmpPos, depth - 1, worldGenRand, conf.Height);
                break;
            }
        }
    }

    public void PlaceCoral(IBlockAccessor blockAccessor, BlockPos pos, IRandom worldGenRand, int depth, int minDepth, BlockPatchAttributes attributes)
    {
        var verticalGrowChance = attributes?.CoralVerticalGrowChance ?? 0.6f;
        var shelveChance = attributes?.CoralShelveChance ?? 0.3f;
        var structureChance = attributes?.CoralStructureChance ?? 0.5f;
        var coralBaseHeight = attributes?.CoralBaseHeight ?? 2;

        // replace 0-x ground with coralblock (full)
        pos.Add(0, - (coralBaseHeight - 1), 0);
        for (var i = 0; i < coralBaseHeight; i++)
        {
            var coralBase = GetRandomBlock(worldGenRand, attributes.CoralBaseBlock);
            blockAccessor.SetBlock(coralBase.BlockId, pos);
            pos.Up();
        }

        depth--;
        if (depth <= 0) return;

        var chance = worldGenRand.NextFloat();
        var canSpawnOntop = true;
        // chance to spawn a shelf if we have a attachable side
        if (chance < shelveChance)
        {
            var sides = GetSolidSides(blockAccessor, pos);
            if (sides.Count > 0)
            {
                var nextInt = worldGenRand.NextInt(sides.Count);
                var shelfBlocks = GetRandomShelve(worldGenRand, attributes.CoralShelveBlock);
                ;
                GetRandomShelve(worldGenRand, attributes.CoralShelveBlock);
                blockAccessor.SetBlock(shelfBlocks[sides[nextInt]].BlockId, pos);
                canSpawnOntop = false;
            }
        }

        if (canSpawnOntop)
        {
            chance = worldGenRand.NextFloat();
            if (chance < structureChance)
            {
                var coralstructure = GetRandomBlock(worldGenRand, attributes.CoralStructureBlock);
                blockAccessor.SetBlock(coralstructure.BlockId, pos);
                pos.Up();
                depth--;
            }

            if (depth > 0)
            {
                // spawn a coral-[brain,fan,...] on top of structure or coralblock
                var coral = GetRandomBlock(worldGenRand, attributes.CoralBlock);
                blockAccessor.SetBlock(coral.BlockId, pos);
            }
        }

        if (depth - minDepth == 0) return;
        pos.Up();
        depth--;

        for (var i = 0; i < depth - minDepth; i++)
        {
            var skipChance = worldGenRand.NextFloat();
            if (skipChance > verticalGrowChance)
            {
                pos.Up();
                depth--;
                continue;
            }

            var sides = GetSolidSides(blockAccessor, pos);
            if (sides.Count == 0)
            {
                continue;
            }

            var nextInt = worldGenRand.NextInt(sides.Count);
            var shelfBlocksId = GetRandomShelve(worldGenRand, attributes.CoralShelveBlock);
            blockAccessor.SetBlock(shelfBlocksId[sides[nextInt]].BlockId, pos);
            pos.Up();
            depth--;
        }
    }

    private static Block[] GetRandomShelve(IRandom worldGenRand, Block[][] blocks)
    {
        return blocks[worldGenRand.NextInt(blocks.Length)];
    }

    private static Block GetRandomBlock(IRandom worldGenRand, Block[] blocks)
    {
        return blocks[worldGenRand.NextInt(blocks.Length)];
    }

    private static List<int> GetSolidSides(IBlockAccessor blockAccessor, BlockPos pos)
    {
        var sides = new List<int>();
        var tmpPos = pos.NorthCopy();
        var block = blockAccessor.GetBlock(tmpPos);
        if (block.SideSolid[BlockFacing.SOUTH.Index])
        {
            sides.Add(BlockFacing.NORTH.Index);
        }

        tmpPos.Z += 2;
        block = blockAccessor.GetBlock(tmpPos);
        if (block.SideSolid[BlockFacing.NORTH.Index])
        {
            sides.Add(BlockFacing.SOUTH.Index);
        }

        tmpPos.Z -= 1;
        tmpPos.X += 1;
        block = blockAccessor.GetBlock(tmpPos);
        if (block.SideSolid[BlockFacing.WEST.Index])
        {
            sides.Add(BlockFacing.EAST.Index);
        }

        tmpPos.X -= 2;
        block = blockAccessor.GetBlock(tmpPos);
        if (block.SideSolid[BlockFacing.EAST.Index])
        {
            sides.Add(BlockFacing.WEST.Index);
        }

        return sides;
    }
}
