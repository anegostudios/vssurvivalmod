using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent;

public class BlockSlantedRoofingHalf : Block
{
    public override AssetLocation GetHorizontallyFlippedBlockCode(EnumAxis axis)
    {
        var codeParts = Code.Path.Split('-');
        var facingString = codeParts[codeParts.Length - 2];
        var facing = BlockFacing.FromCode(facingString);
        switch (codeParts[0])
        {
            case "slantedroofinghalfleft":
            {
                if (facing.Axis != axis)
                {
                    return new AssetLocation(Code.Path.Replace("left", "right"));
                }
                var r2 = CodeWithVariant("horizontalorientation", facing.Opposite.Code);
                return new AssetLocation(r2.Path.Replace("left", "right"));
            }
            case "slantedroofinghalfright":
            {
                if (facing.Axis != axis)
                {
                    return new AssetLocation(Code.Path.Replace("right", "left"));
                }
                var r2 = CodeWithVariant("horizontalorientation", facing.Opposite.Code);
                return new AssetLocation(r2.Path.Replace("right", "left"));
            }
            case "slantedroofingcornerinner":
            case "slantedroofingcornerouter":
            {
                if (facing.Axis != axis)
                {

                    return CodeWithVariant("horizontalorientation", BlockFacing.HORIZONTALS[(facing.Index + 3) % 4].Code);
                }
                return CodeWithVariant("horizontalorientation", BlockFacing.HORIZONTALS[(facing.Index + 1) % 4].Code);
            }
        }

        if (facing.Axis != axis)
        {
            return CodeWithVariant("horizontalorientation", facing.Opposite.Code);
        }

        return Code;
    }
}
