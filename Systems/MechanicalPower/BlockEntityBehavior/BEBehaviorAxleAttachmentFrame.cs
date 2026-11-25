using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace Vintagestory.GameContent.Mechanics
{
    public class BEBehaviorAxleAttachmentFrame(BlockEntity blockentity) : BlockEntityBehavior(blockentity)
    {
        ItemStack? frameStack;

        public ItemStack? GetFrameStack() => frameStack?.Clone();

        public void SetFrameStack(ItemStack stack)
        {
            frameStack = stack.Clone();
            frameStack.StackSize = 1;
            Blockentity.MarkDirty(true);
        }

        public override void OnBlockPlaced(ItemStack? byItemStack = null)
        {
            base.OnBlockPlaced(byItemStack);
            if (byItemStack?.Block is BlockAxleAttachment) SetFrameStack(byItemStack);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);
            frameStack = tree.GetItemstack("frameStack");
            frameStack?.ResolveBlockOrItem(worldAccessForResolve);
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            if (frameStack == null) tree.RemoveAttribute("frameStack");
            else tree.SetItemstack("frameStack", frameStack);
        }

        public override void OnLoadCollectibleMappings(IWorldAccessor worldForNewMappings, Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping, int schematicSeed, bool resolveImports)
        {
            base.OnLoadCollectibleMappings(worldForNewMappings, oldBlockIdMapping, oldItemIdMapping, schematicSeed, resolveImports);
            if (frameStack == null) return;

            if (!frameStack.FixMapping(oldBlockIdMapping, oldItemIdMapping, worldForNewMappings) || frameStack.Block is not BlockAxleAttachment)
            {
                frameStack = null;
                return;
            }
            frameStack.Collectible.OnLoadCollectibleMappings(worldForNewMappings, new DummySlot(frameStack), oldBlockIdMapping, oldItemIdMapping, resolveImports);
        }

        public override void OnStoreCollectibleMappings(Dictionary<int, AssetLocation> blockIdMapping, Dictionary<int, AssetLocation> itemIdMapping)
        {
            base.OnStoreCollectibleMappings(blockIdMapping, itemIdMapping);
            frameStack?.Collectible.OnStoreCollectibleMappings(Api.World, new DummySlot(frameStack), blockIdMapping, itemIdMapping);
        }
    }
}
