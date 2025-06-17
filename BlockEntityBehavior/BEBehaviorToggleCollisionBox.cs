using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent;

public class BlockToggleCollisionBox : BlockClutter
{
    public override Cuboidf[] GetCollisionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
    {
        var betcb = blockAccessor.GetBlockEntity(pos)?.GetBehavior<BEBehaviorToggleCollisionBox>();
        if (betcb?.Solid == true)
        {
            return betcb.CollisionBoxes;
        }
        return base.GetCollisionBoxes(blockAccessor, pos);
    }

    public override Cuboidf[] GetSelectionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
    {
        var betcb = blockAccessor.GetBlockEntity(pos)?.GetBehavior<BEBehaviorToggleCollisionBox>();
        if (betcb?.Solid == true)
        {
            return betcb.CollisionBoxes;
        }
        return base.GetCollisionBoxes(blockAccessor, pos);
    }
}

public class BEBehaviorToggleCollisionBox : BlockEntityBehavior
{
    public bool Solid;
    public Cuboidf[] CollisionBoxes;

    public BEBehaviorToggleCollisionBox(BlockEntity blockentity) : base(blockentity)
    {
    }

    public override void Initialize(ICoreAPI api, JsonObject properties)
    {
        base.Initialize(api, properties);
        CollisionBoxes = properties["collisionBoxes"].AsObject<Cuboidf[]>();
    }

    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
    {
        base.FromTreeAttributes(tree, worldAccessForResolve);

        Solid = tree.GetBool("solid");
    }

    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        base.ToTreeAttributes(tree);
        tree.SetBool("solid", Solid);
    }
}
