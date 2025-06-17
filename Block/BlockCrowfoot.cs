using System;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent;

public class BlockCrowfoot : BlockSeaweed
{
    public override void OnLoaded(ICoreAPI api)
    {
        base.OnLoaded(api);

        blocks = new Block[]
        {
            api.World.BlockAccessor.GetBlock(CodeWithParts("section")),
            api.World.BlockAccessor.GetBlock(CodeWithParts("tip")),
            api.World.BlockAccessor.GetBlock(CodeWithParts("top")),
        };
    }

    public override bool TryPlaceBlockForWorldGenUnderwater(IBlockAccessor blockAccessor, BlockPos pos, BlockFacing onBlockFace, IRandom worldGenRand, int minWaterDepth, int maxWaterDepth, BlockPatchAttributes attributes = null)
    {
        var belowPos = pos.DownCopy();
        var height = attributes?.Height ?? NatFloat.createGauss(2, 2);
        var flowChance = attributes != null && attributes.FlowerChance != -1 ? attributes.FlowerChance : 0.7f;

        Block block;

        // start above water so that's why we start 2 depth when using the Underwater placement
        var depth = 1;
        while (depth < maxWaterDepth)
        {
            belowPos.Down();
            block = blockAccessor.GetBlock(belowPos);

            if (block.Fertility > 0)
            {
                PlaceCrowfoot(blockAccessor, belowPos, depth, worldGenRand, height, flowChance);
                return true;
            }

            if (block is BlockWaterPlant || !block.IsLiquid())
                return false;
            // Prevent placing Crowfoot over Crowfoot or seaweed (for example might result on a 3-deep plant placed on top of a 5-deep plant's existing position, giving a plant with 2 tops at positions 3 and 5)

            depth++;
        }

        return false;
    }


    internal void PlaceCrowfoot(IBlockAccessor blockAccessor, BlockPos pos, int depth, IRandom random, NatFloat heightNatFloat, float flowChance)
    {
        var height = Math.Min(depth, (int)heightNatFloat.nextFloat(1f, random));
        var spawnFlower = random.NextFloat() < flowChance && height == depth ;
        while (height-- > 1)
        {
            pos.Up();
            blockAccessor.SetBlock(blocks[0].BlockId, pos); // section
        }

        pos.Up();
        if(spawnFlower)
        {
            blockAccessor.SetBlock(blocks[2].BlockId, pos); // top, flower
        }
        else
        {
            blockAccessor.SetBlock(blocks[1].BlockId, pos); // tip
        }
    }
}
