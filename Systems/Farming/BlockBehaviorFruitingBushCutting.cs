using System;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

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

    public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
    {
        var traits = inSlot.Itemstack?.Attributes.GetString("traits")?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (traits != null) BEBehaviorFruitingBush.addTraits(dsc, traits);

        base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
    }

    public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos, ref EnumHandling handling)
    {
        var beBeh = block.GetBEBehavior<BEBehaviorFruitingBushCutting>(pos);
        if (beBeh != null)
        {
            handling = EnumHandling.PreventDefault;
            return beBeh.GetCuttingItemStack(pos);
        }

        return base.OnPickBlock(world, pos, ref handling);
    }

    public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, ref float dropChanceMultiplier, ref EnumHandling handling)
    {
        var beBeh = block.GetBEBehavior<BEBehaviorFruitingBushCutting>(pos);
        if (beBeh != null)
        {
            handling = EnumHandling.PreventDefault;
            return [beBeh.GetCuttingItemStack(pos)];
        }

        return base.GetDrops(world, pos, byPlayer, ref dropChanceMultiplier, ref handling);
    }
}
