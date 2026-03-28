using Vintagestory.API.Common;

namespace Vintagestory.GameContent;

public class BlockBehaviorFruitingBushCutting : BlockBehavior
{
    public BlockBehaviorFruitingBushCutting(Block block) : base(block) { }

    public override bool CanPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref EnumHandling handling, ref string failureCode)
    {
        var block = world.BlockAccessor.GetBlock(blockSel.DidOffset ? blockSel.Position.AddCopy(blockSel.Face.Opposite) : blockSel.Position);
        if (!block.Variant.ContainsKey("fertility"))
        {
            handling = EnumHandling.PreventDefault;
            failureCode = "unsuitablesoil";
            return false;
        }

        return base.CanPlaceBlock(world, byPlayer, blockSel, ref handling, ref failureCode);
    }
}
