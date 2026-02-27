using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

#nullable disable

namespace Vintagestory.GameContent.Mechanics
{
    public class BlockBehaviorCoverable : BlockBehavior
    {
        public BlockBehaviorCoverable(Block block) : base(block)
        {
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref EnumHandling handling)
        {
            var hslot = byPlayer.InventoryManager.ActiveHotbarSlot;
            if (byPlayer.Entity.Controls.Sprint && byPlayer.InventoryManager.OffhandHotbarSlot.Itemstack?.Collectible.GetTool(byPlayer.InventoryManager.OffhandHotbarSlot) == EnumTool.Wrench)
            {
                if (BlockEntityBehaviorCoverable.SuitableMaterial(hslot))
                {
                    block.GetBEBehavior<BlockEntityBehaviorCoverable>(blockSel.Position).TryAddMaterial(byPlayer, blockSel);
                } else
                {
                    if (hslot.Empty)
                    {
                        (world.Api as ICoreClientAPI)?.TriggerIngameError(this, "unsuitablematerial", Lang.Get("Put suitable block material in your hands"));
                    } else
                    {
                        (world.Api as ICoreClientAPI)?.TriggerIngameError(this, "unsuitablematerial", Lang.Get("Unsuitable block material for axle coverage"));
                    }
                        
                }

                handling = EnumHandling.PreventDefault;
                return true;
            }

            return base.OnBlockInteractStart(world, byPlayer, blockSel, ref handling);
        }

        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer, ref EnumHandling handling)
        {
            if (BlockBehaviorWrenchOrientable.wrenchItems == null) BlockBehaviorWrenchOrientable.loadWrenchItems(world);

            return [
                new WorldInteraction() {
                    HotKeyCode = "ctrl",
                    MouseButton = EnumMouseButton.Right,
                    Itemstacks = BlockBehaviorWrenchOrientable.wrenchItems,
                    GetMatchingStacks = (wi, bs, es) => BlockBehaviorWrenchOrientable.wrenchItems,
                    ActionLangCode = "Add block covering"
                }
            ];
        }
    }
}
