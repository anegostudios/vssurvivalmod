using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace Vintagestory.GameContent;
#nullable disable

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

    public override void OnLoadCollectibleMappings(IWorldAccessor world, Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping, int schematicSeed, bool resolveImports)
    {
        base.OnLoadCollectibleMappings(world, oldBlockIdMapping, oldItemIdMapping, schematicSeed, resolveImports);
        foreach (var slot in Inventory)
        {
            if (slot.Itemstack == null) continue;
            if (!slot.Itemstack.FixMapping(oldBlockIdMapping, oldItemIdMapping, world))
            {
                slot.Itemstack = null;
            }
            else
            {
                slot.Itemstack.Collectible.OnLoadCollectibleMappings(world, slot, oldBlockIdMapping, oldItemIdMapping, resolveImports);
            }
        }
    }

    public override void OnStoreCollectibleMappings(Dictionary<int, AssetLocation> blockIdMapping, Dictionary<int, AssetLocation> itemIdMapping)
    {
        foreach (var slot in Inventory)
        {
            slot.Itemstack?.Collectible.OnStoreCollectibleMappings(Api.World, slot, blockIdMapping, itemIdMapping);
        }
    }
}
