using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace Vintagestory.GameContent;
#nullable disable

public interface ISlotCountProvider
{
    int GetSlotCount(BlockEntity uninitializedBlockEntity);
}

public abstract class BEBehaviorContainer : BlockEntityBehavior
{
    protected InWorldContainer container;
    protected abstract string InventoryClassName { get; }
    public abstract InventoryBase Inventory { get; }

    public BEBehaviorContainer(BlockEntity blockentity) : base(blockentity)
    {
        container = new InWorldContainer(() => Inventory, "bhinventory");
    }    

    public override void Initialize(ICoreAPI api, JsonObject properties)
    {
        base.Initialize(api, properties);
        Inventory.Pos = Pos;
        Inventory.Api = api;
        Inventory.ResolveBlocksOrItems();
    }

    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
    {
        base.FromTreeAttributes(tree, worldAccessForResolve);
        container.FromTreeAttributes(tree, worldAccessForResolve);

        Inventory.Api = worldAccessForResolve.Api;
        Inventory.ResolveBlocksOrItems();
    }

    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        base.ToTreeAttributes(tree);
        container.ToTreeAttributes(tree);
    }

}
