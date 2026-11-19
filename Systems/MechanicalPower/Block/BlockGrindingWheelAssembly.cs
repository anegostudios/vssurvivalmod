using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent.Mechanics
{
    public class BlockGrindingWheelAssembly : BlockAxleAttachmentAssembly
    {
        TagSet grindstoneTag = TagSet.Empty;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            api.CollectibleTagRegistry.TryRegisterAndCreateTagSetAndLogIssues(out grindstoneTag, "grindstone");
        }

        public bool IsGrindstone(ItemStack? stack)
        {
            return stack?.Collectible?.GetTags(stack).Overlaps(grindstoneTag) == true;
        }

        public override bool CanHostSpurGear(BlockFacing face, float radius) => false;

        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1f)
        {
            ItemStack[] drops = base.GetDrops(world, pos, byPlayer, dropQuantityMultiplier);
            ItemStack? stone = GetBlockEntity<BlockEntityGrindingWheel>(pos)?.GetGrindstoneStack();
            return stone == null ? drops : [..drops, stone];
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            ItemSlot? slot = byPlayer.InventoryManager.ActiveHotbarSlot;
            if (slot == null) return false;
            if (IsGrindstone(slot.Itemstack))
            {
                if (!world.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak))
                {
                    slot.MarkDirty();
                    return false;
                }
                if (GetBlockEntity<BlockEntityGrindingWheel>(blockSel.Position)?.SetGrindstone(slot.Itemstack) == true && byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative)
                {
                    slot.TakeOut(1);
                    slot.MarkDirty();
                }
                return true;
            }

            return GetBlockEntity<BlockEntityGrindingWheel>(blockSel.Position)?.OnInteractStart(byPlayer, blockSel) == true;
        }

        public override bool OnBlockInteractStep(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            return GetBlockEntity<BlockEntityGrindingWheel>(blockSel.Position)?.OnInteractStep(secondsUsed, byPlayer, blockSel) == true;
        }

        public override void OnBlockInteractStop(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            GetBlockEntity<BlockEntityGrindingWheel>(blockSel.Position)?.OnInteractStop(secondsUsed, byPlayer, blockSel);
            base.OnBlockInteractStop(secondsUsed, world, byPlayer, blockSel);
        }

        public override bool OnBlockInteractCancel(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, EnumItemUseCancelReason cancelReason)
        {
            GetBlockEntity<BlockEntityGrindingWheel>(blockSel.Position)?.OnInteractStop(secondsUsed, byPlayer, blockSel);
            return base.OnBlockInteractCancel(secondsUsed, world, byPlayer, blockSel, cancelReason);
        }
    }
}
